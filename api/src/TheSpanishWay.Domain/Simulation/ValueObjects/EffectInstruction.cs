using TheSpanishWay.Domain.Simulation.Enumerations;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>
/// One effect, ready to apply: roles bound to concrete ids, and any random value already
/// rolled.
/// <para>
/// Distinct from the authored <c>EffectDefinition</c>, which may say "+1 to +3 to a party
/// with credibility below 60". By the time it is an instruction, both the party and the
/// number are decided — so a saved game explains itself and reloading cannot reroll it.
/// </para>
/// </summary>
public sealed record EffectInstruction(
    AttributeRef Target,
    EffectOperation Operation,
    decimal Value,
    int? DelayDays,
    int? DurationDays,
    Recurrence? Recurrence,
    StackKey StackKey,
    StackingPolicy Policy,
    Provenance Provenance)
{
    /// <summary>Applies at once, permanently, to the base value.</summary>
    public static EffectInstruction Immediate(
        AttributeRef target,
        EffectOperation operation,
        decimal value,
        Provenance provenance,
        StackKey? stackKey = null) =>
        new(target, operation, value, null, null, null,
            stackKey ?? StackKey.Named($"{provenance.SourceId}@{target}"),
            StackingPolicy.RefreshDuration, provenance);

    /// <summary>Applies at once as a modifier that expires after <paramref name="durationDays"/>.</summary>
    public static EffectInstruction Temporary(
        AttributeRef target,
        EffectOperation operation,
        decimal value,
        int durationDays,
        Provenance provenance,
        StackingPolicy policy = StackingPolicy.RefreshDuration,
        StackKey? stackKey = null) =>
        new(target, operation, value, null, durationDays, null,
            stackKey ?? StackKey.Named($"{provenance.SourceId}@{target}"), policy, provenance);

    /// <summary>Queued now, applied on a later date.</summary>
    public static EffectInstruction Delayed(
        AttributeRef target,
        EffectOperation operation,
        decimal value,
        int delayDays,
        Provenance provenance,
        int? durationDays = null,
        StackKey? stackKey = null) =>
        new(target, operation, value, delayDays, durationDays, null,
            stackKey ?? StackKey.Named($"{provenance.SourceId}@{target}"),
            StackingPolicy.RefreshDuration, provenance);

    public bool IsDelayed => DelayDays is > 0;

    public bool IsTemporary => DurationDays is not null;
}
