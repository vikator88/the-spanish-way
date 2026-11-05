using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Content.ValueObjects;

/// <summary>
/// An authored instruction to change state, before anything about it is decided.
/// <para>
/// Its target may name a role, its value may be a range, and it may be conditional. Turning
/// it into an <see cref="EffectInstruction"/> binds the role, rolls the value and fixes the
/// dates — after which it is a fact about this game rather than a rule about any game.
/// </para>
/// </summary>
public sealed record EffectDefinition(
    AttributeRef Target,
    EffectOperation Operation,
    ValueExpression Value,
    int? DelayDays,
    int? DurationDays,
    Recurrence? Recurrence,
    StackingPolicy Policy,
    string? StackKeyOverride,
    ConditionGroup Conditions)
{
    public static EffectDefinition Immediate(
        AttributeRef target,
        EffectOperation operation,
        decimal value) =>
        new(target, operation, new ValueExpression.Fixed(value), null, null, null,
            StackingPolicy.RefreshDuration, null, ConditionGroup.Always);

    public static EffectDefinition Temporary(
        AttributeRef target,
        EffectOperation operation,
        ValueExpression value,
        int durationDays,
        StackingPolicy policy = StackingPolicy.RefreshDuration) =>
        new(target, operation, value, null, durationDays, null, policy, null, ConditionGroup.Always);

    public bool ReferencesRole => RoleName.IsRoleToken(Target.Instance);
}
