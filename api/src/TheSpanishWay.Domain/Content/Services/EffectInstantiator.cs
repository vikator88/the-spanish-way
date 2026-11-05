using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.ValueObjects;

namespace TheSpanishWay.Domain.Content.Services;

/// <summary>
/// Turns an authored <see cref="EffectDefinition"/> into a concrete
/// <see cref="EffectInstruction"/>: roles bound, value rolled, stack key settled.
/// <para>
/// Everything uncertain is decided here, once. After this the effect is a fact about this
/// game rather than a rule about any game, which is what lets a saved game explain itself.
/// </para>
/// </summary>
public sealed class EffectInstantiator(ConditionEvaluator evaluator)
{
    private readonly ConditionEvaluator _evaluator = evaluator;

    /// <summary>
    /// Instantiates every effect whose own conditions hold. Effects are considered in order,
    /// and each draws at most once from the <c>events</c> stream, so the sequence is stable.
    /// </summary>
    public Result<IReadOnlyList<EffectInstruction>> Instantiate(
        IReadOnlyList<EffectDefinition> definitions,
        RoleBindings bindings,
        Provenance provenance,
        IRandomSource random,
        string sourceDefinitionKey)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(random);

        var instructions = new List<EffectInstruction>();

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];

            var applies = _evaluator.Evaluate(definition.Conditions, bindings);

            if (applies.IsFailure)
            {
                return Result.Failure<IReadOnlyList<EffectInstruction>>(applies.Error);
            }

            if (!applies.Value)
            {
                continue;
            }

            var target = ResolveTarget(definition.Target, bindings);

            if (target.IsFailure)
            {
                return Result.Failure<IReadOnlyList<EffectInstruction>>(target.Error);
            }

            var stackKey = definition.StackKeyOverride is { } named
                ? StackKey.Named(named)
                : StackKey.Default(sourceDefinitionKey, index, target.Value);

            instructions.Add(
                new EffectInstruction(
                    target.Value,
                    definition.Operation,
                    definition.Value.Resolve(random, RandomStream.Events),
                    definition.DelayDays,
                    definition.DurationDays,
                    definition.Recurrence,
                    stackKey,
                    definition.Policy,
                    provenance with { EffectIndex = index }));
        }

        return Result.Success<IReadOnlyList<EffectInstruction>>(instructions);
    }

    private static Result<AttributeRef> ResolveTarget(AttributeRef target, RoleBindings bindings)
    {
        var instance = bindings.ResolveInstance(target.Instance);

        return instance.IsFailure
            ? Result.Failure<AttributeRef>(instance.Error)
            : AttributeRef.Create(target.Target, instance.Value, target.Attribute);
    }
}
