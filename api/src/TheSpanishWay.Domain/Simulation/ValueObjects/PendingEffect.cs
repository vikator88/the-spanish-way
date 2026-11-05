using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>How often a queued effect repeats, and how many applications are left.</summary>
public sealed record Recurrence(int IntervalDays, int RemainingApplications)
{
    public bool IsExhausted => RemainingApplications <= 0;

    public Recurrence AfterApplication() => this with { RemainingApplications = RemainingApplications - 1 };
}

/// <summary>
/// An effect waiting for its date: delayed, gradual or recurring.
/// <para>
/// Its <see cref="Value"/> is already resolved. A declaration of "+1 to +3" is rolled when
/// the effect is <em>created</em>, not when it applies, so a saved game explains itself and
/// reloading cannot reroll it into a different outcome. See design D5.
/// </para>
/// </summary>
public sealed record PendingEffect(
    PendingEffectId Id,
    AttributeRef Target,
    EffectOperation Operation,
    decimal Value,
    GameDate ApplyOn,
    int? DurationDays,
    Recurrence? Recurrence,
    StackKey StackKey,
    StackingPolicy Policy,
    Provenance Provenance,
    PendingEffectStatus Status)
{
    /// <summary>Permanent effects mutate the base; temporary ones become modifiers.</summary>
    public bool IsPermanent => DurationDays is null;

    public bool IsDueOn(GameDate date) =>
        Status is PendingEffectStatus.Scheduled or PendingEffectStatus.Recurring && date >= ApplyOn;

    /// <summary>
    /// The next occurrence, or null when this was the last. A recurring effect reschedules
    /// itself rather than the queue tracking repetition separately.
    /// </summary>
    public PendingEffect? Next()
    {
        if (Recurrence is null)
        {
            return null;
        }

        var remaining = Recurrence.AfterApplication();

        return remaining.IsExhausted
            ? null
            : this with
            {
                ApplyOn = ApplyOn.AddDays(Recurrence.IntervalDays),
                Recurrence = remaining,
                Status = PendingEffectStatus.Recurring,
            };
    }
}
