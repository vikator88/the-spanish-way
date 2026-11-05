using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Content.ValueObjects;

/// <summary>
/// A reusable description of something that can happen: a corruption scandal, a strike, a
/// commodity shock.
/// <para>
/// There is no C# class per event. Thirty definitions are thirty rows of content, which is
/// what the role bindings, conditions and effects above exist to make possible.
/// </para>
/// </summary>
public sealed record GameEventDefinition(
    EventDefinitionId Id,
    string Key,
    string Name,
    string DescriptionTemplate,
    EventCategory Category,
    decimal BaseProbability,
    ConditionGroup TriggerConditions,
    IReadOnlyList<RoleDeclaration> Roles,
    IReadOnlyList<EffectDefinition> Effects,
    IReadOnlyList<ActionDefinitionId> AvailableActions,
    IReadOnlyList<EventDefinitionId> FollowUpEvents,
    bool IsBlocking,
    string? DefaultOptionKey,
    int? DurationDays,
    int? CooldownDays,
    IReadOnlySet<string> Tags)
{
    public static Result<GameEventDefinition> Create(
        EventDefinitionId id,
        string key,
        EventCategory category,
        decimal baseProbability,
        ConditionGroup? triggerConditions = null,
        IReadOnlyList<RoleDeclaration>? roles = null,
        IReadOnlyList<EffectDefinition>? effects = null,
        bool isBlocking = false,
        string? defaultOptionKey = null,
        int? durationDays = null,
        int? cooldownDays = null,
        IReadOnlyList<EventDefinitionId>? followUps = null,
        IReadOnlySet<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<GameEventDefinition>(
                new Error("event_definition.empty_key", "An event definition needs a key."));
        }

        if (baseProbability is < 0m or > 1m)
        {
            return Result.Failure<GameEventDefinition>(
                new Error(
                    "event_definition.invalid_probability",
                    $"Base probability {baseProbability} for '{key}' is not between 0 and 1."));
        }

        if (cooldownDays is < 0)
        {
            return Result.Failure<GameEventDefinition>(
                new Error("event_definition.negative_cooldown", $"Cooldown for '{key}' cannot be negative."));
        }

        // A non-blocking event lapses on its own, so it needs to know what to do when it does.
        if (!isBlocking && durationDays is not null && defaultOptionKey is null && effects is { Count: > 0 })
        {
            return Result.Failure<GameEventDefinition>(
                new Error(
                    "event_definition.missing_default_option",
                    $"Non-blocking event '{key}' expires unresolved and needs a default option."));
        }

        return Result.Success(
            new GameEventDefinition(
                id,
                key.Trim().ToLowerInvariant(),
                key,
                string.Empty,
                category,
                baseProbability,
                triggerConditions ?? ConditionGroup.Always,
                roles ?? [],
                effects ?? [],
                [],
                followUps ?? [],
                isBlocking,
                defaultOptionKey,
                durationDays,
                cooldownDays,
                tags ?? new HashSet<string>()));
    }

    /// <summary>A chained event is never rolled for; it only arrives as another's follow-up.</summary>
    public bool CanFireSpontaneously => Category != EventCategory.Chained;
}
