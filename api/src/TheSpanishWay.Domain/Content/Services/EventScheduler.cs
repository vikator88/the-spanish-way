using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Entities;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Content.Services;

/// <summary>Why a definition was not eligible, so content authors can tell "unlucky" from "impossible".</summary>
public enum IneligibilityReason
{
    None = 0,
    OnCooldown = 1,
    TriggerConditionsUnmet = 2,
    NoCandidateForRole = 3,
    FollowUpNotUnlocked = 4,
    DidNotRoll = 5,
}

/// <summary>One definition's verdict for one day.</summary>
public sealed record EligibilityVerdict(
    EventDefinitionId Definition,
    bool IsEligible,
    IneligibilityReason Reason,
    RoleBindings Bindings);

/// <summary>
/// Decides which events fire today.
/// <para>
/// Order matters for determinism: definitions are considered in key order, and each one
/// draws from the <c>events</c> stream in that order. Adding a definition shifts later draws
/// within that stream but cannot disturb media, reactions or polls, which have their own.
/// </para>
/// </summary>
public sealed class EventScheduler(
    ConditionEvaluator evaluator,
    RoleBinder binder,
    IIdentifierSource identifiers)
{
    private readonly ConditionEvaluator _evaluator = evaluator;
    private readonly RoleBinder _binder = binder;
    private readonly IIdentifierSource _identifiers = identifiers;

    /// <summary>
    /// Everything that fires on <paramref name="today"/>, in a stable order. Records each
    /// firing in the ledger and unlocks its follow-ups.
    /// </summary>
    public IReadOnlyList<EventOccurrence> Generate(
        IReadOnlyList<GameEventDefinition> definitions,
        EventLedger ledger,
        GameDate today,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(random);

        var fired = new List<EventOccurrence>();

        foreach (var definition in definitions.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            var verdict = Assess(definition, ledger, today, random);

            if (!verdict.IsEligible)
            {
                continue;
            }

            var occurrence = EventOccurrence.Fire(
                _identifiers.NextEventOccurrenceId(), definition, verdict.Bindings, today);

            ledger.RecordFired(definition.Id, today);

            // Follow-ups become possible, not inevitable: each still faces its own conditions.
            foreach (var followUp in definition.FollowUpEvents)
            {
                ledger.UnlockFollowUp(followUp);
            }

            fired.Add(occurrence);
        }

        return fired;
    }

    /// <summary>
    /// Whether one definition fires today, and why not when it does not. Exposed separately
    /// so content authoring can ask "why did this never happen?" without running a game.
    /// </summary>
    public EligibilityVerdict Assess(
        GameEventDefinition definition,
        EventLedger ledger,
        GameDate today,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(random);

        if (definition.Category == EventCategory.Chained && !ledger.IsFollowUpUnlocked(definition.Id))
        {
            return Fail(definition, IneligibilityReason.FollowUpNotUnlocked);
        }

        if (!ledger.IsOffCooldown(definition.Id, definition.CooldownDays, today))
        {
            return Fail(definition, IneligibilityReason.OnCooldown);
        }

        var triggers = _evaluator.Evaluate(definition.TriggerConditions, RoleBindings.Empty);

        // A trigger that cannot be read is treated as unmet, never as true: an unreadable
        // condition must not let an event through.
        if (triggers.IsFailure || !triggers.Value)
        {
            return Fail(definition, IneligibilityReason.TriggerConditionsUnmet);
        }

        var bindings = _binder.Bind(definition.Roles, random);

        if (bindings.IsFailure)
        {
            return Fail(definition, IneligibilityReason.NoCandidateForRole);
        }

        // The roll comes last, so a definition that could never fire does not consume draws
        // and shift the stream for everything after it.
        if (!random.Chance(RandomStream.Events, definition.BaseProbability))
        {
            return new EligibilityVerdict(
                definition.Id, false, IneligibilityReason.DidNotRoll, bindings.Value);
        }

        return new EligibilityVerdict(definition.Id, true, IneligibilityReason.None, bindings.Value);
    }

    private static EligibilityVerdict Fail(GameEventDefinition definition, IneligibilityReason reason) =>
        new(definition.Id, false, reason, RoleBindings.Empty);
}
