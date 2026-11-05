using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Content.Entities;

/// <summary>
/// One firing of an event definition, in one game, with its roles bound.
/// <para>
/// Distinct from the definition, which is reusable content. The same scandal definition can
/// occur three times across a legislature, each time about a different party, and each
/// occurrence has its own life: it is resolved once and never again.
/// </para>
/// </summary>
public sealed class EventOccurrence : Entity<EventOccurrenceId>
{
    private EventOccurrence(
        EventOccurrenceId id,
        EventDefinitionId definitionId,
        RoleBindings bindings,
        GameDate triggeredOn,
        GameDate? expiresOn,
        bool isBlocking,
        string? defaultOptionKey)
        : base(id)
    {
        DefinitionId = definitionId;
        Bindings = bindings;
        TriggeredOn = triggeredOn;
        ExpiresOn = expiresOn;
        IsBlocking = isBlocking;
        DefaultOptionKey = defaultOptionKey;
        Status = EventOccurrenceStatus.Active;
    }

    public EventDefinitionId DefinitionId { get; }

    /// <summary>Who this occurrence turned out to be about. Stored, never recomputed.</summary>
    public RoleBindings Bindings { get; }

    public GameDate TriggeredOn { get; }

    public GameDate? ExpiresOn { get; }

    /// <summary>A blocking occurrence halts the simulation until the player answers.</summary>
    public bool IsBlocking { get; }

    public string? DefaultOptionKey { get; }

    public EventOccurrenceStatus Status { get; private set; }

    public string? ChosenOptionKey { get; private set; }

    public GameDate? ResolvedOn { get; private set; }

    public bool IsOpen => Status == EventOccurrenceStatus.Active;

    public static EventOccurrence Fire(
        EventOccurrenceId id,
        GameEventDefinition definition,
        RoleBindings bindings,
        GameDate on)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new EventOccurrence(
            id,
            definition.Id,
            bindings,
            on,
            definition.DurationDays is { } days ? on.AddDays(days) : null,
            definition.IsBlocking,
            definition.DefaultOptionKey);
    }

    /// <summary>The player chose. Only possible once, and only while the occurrence is live.</summary>
    public Result Resolve(string optionKey, GameDate on)
    {
        if (Status != EventOccurrenceStatus.Active)
        {
            return Result.Failure(
                new Error(
                    "occurrence.already_closed",
                    $"Occurrence {Id} is {Status} and cannot be resolved again."));
        }

        if (HasExpiredOn(on))
        {
            return Result.Failure(
                new Error(
                    "occurrence.expired",
                    $"Occurrence {Id} expired on {ExpiresOn} and no longer accepts a decision."));
        }

        Status = EventOccurrenceStatus.Resolved;
        ChosenOptionKey = optionKey;
        ResolvedOn = on;

        return Result.Success();
    }

    /// <summary>
    /// Its expiry passed. A non-blocking occurrence takes its default option; one with no
    /// default simply lapses.
    /// </summary>
    public Result Lapse(GameDate on)
    {
        if (Status != EventOccurrenceStatus.Active)
        {
            return Result.Failure(
                new Error("occurrence.already_closed", $"Occurrence {Id} is already {Status}."));
        }

        Status = DefaultOptionKey is null
            ? EventOccurrenceStatus.Expired
            : EventOccurrenceStatus.AutoResolved;

        ChosenOptionKey = DefaultOptionKey;
        ResolvedOn = on;

        return Result.Success();
    }

    /// <summary>Expiry is inclusive of its final day, matching modifier expiry.</summary>
    public bool HasExpiredOn(GameDate date) => ExpiresOn is { } expiry && date > expiry;
}
