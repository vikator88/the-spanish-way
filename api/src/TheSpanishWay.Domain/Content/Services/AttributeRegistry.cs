using System.Collections.Frozen;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.Domain.World.Enumerations;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Content.Services;

/// <summary>
/// The single code-side authority on what content is allowed to address.
/// <para>
/// Every condition and effect is checked against this when a scenario loads. An unknown
/// attribute is a validation failure, never a silent no-op and never a condition that
/// quietly evaluates true — v0.1 is explicit that unsupported attributes must fail
/// configuration validation.
/// </para>
/// <para>
/// It is also where the authored/computed boundary is enforced: perceptions, preferences,
/// turnout and seats are registered so that naming them produces a clear refusal rather
/// than an unhelpful "unknown attribute".
/// </para>
/// </summary>
public sealed class AttributeRegistry
{
    private static readonly IReadOnlySet<EffectOperation> AllOperations =
        new HashSet<EffectOperation> { EffectOperation.Set, EffectOperation.Add, EffectOperation.Multiply };

    private static readonly IReadOnlySet<EffectOperation> AdditiveOnly =
        new HashSet<EffectOperation> { EffectOperation.Set, EffectOperation.Add };

    private readonly FrozenDictionary<string, AttributeDefinition> _definitions;

    private AttributeRegistry(IEnumerable<AttributeDefinition> definitions) =>
        _definitions = definitions.ToFrozenDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<AttributeDefinition> Definitions =>
        [.. _definitions.Values.OrderBy(d => d.Key, StringComparer.Ordinal)];

    /// <summary>
    /// The attributes this build understands. World variables are addressed generically —
    /// an indicator is content, so adding one needs no code change — while its three
    /// addressable fields are fixed.
    /// </summary>
    public static AttributeRegistry Default() =>
        new(
        [
            // A world variable's value, and separately where it is heading and how fast.
            // Retargeting is a different act from moving, and content needs both.
            new AttributeDefinition(
                TargetType.WorldVariable, "current", decimal.MinValue, decimal.MaxValue,
                VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.WorldVariable, "target", decimal.MinValue, decimal.MaxValue,
                VariablePolarity.Neutral, AdditiveOnly),
            new AttributeDefinition(
                TargetType.WorldVariable, "evolutionrate", 0m, 1m,
                VariablePolarity.Neutral, AdditiveOnly),

            new AttributeDefinition(
                TargetType.Party, "cohesion", 0m, 100m, VariablePolarity.HigherIsBetter, AllOperations),
            new AttributeDefinition(
                TargetType.Party, "credibility", 0m, 100m, VariablePolarity.HigherIsBetter, AllOperations),
            new AttributeDefinition(
                TargetType.Party, "budget", 0m, decimal.MaxValue, VariablePolarity.HigherIsBetter, AllOperations),

            new AttributeDefinition(
                TargetType.Character, "popularity", 0m, 100m, VariablePolarity.HigherIsBetter, AllOperations),
            new AttributeDefinition(
                TargetType.Character, "charisma", 0m, 100m, VariablePolarity.HigherIsBetter, AllOperations),
            new AttributeDefinition(
                TargetType.Character, "loyalty", 0m, 100m, VariablePolarity.HigherIsBetter, AllOperations),
            new AttributeDefinition(
                TargetType.Character, "ambition", 0m, 100m, VariablePolarity.Neutral, AllOperations),

            new AttributeDefinition(
                TargetType.PartyRelation, "affinity", 0m, 100m, VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.PartyRelation, "trust", 0m, 100m, VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.CharacterRelation, "affinity", 0m, 100m, VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.CharacterRelation, "trust", 0m, 100m, VariablePolarity.Neutral, AllOperations),

            new AttributeDefinition(
                TargetType.AgendaTopic, "relevance", 0m, 100m, VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.PartyResource, "amount", 0m, decimal.MaxValue, VariablePolarity.HigherIsBetter, AllOperations),
            new AttributeDefinition(
                TargetType.ProvincePresence, "organisationalstrength", 0m, 100m,
                VariablePolarity.HigherIsBetter, AllOperations),

            // Computed. Registered so naming one gives a clear refusal rather than a
            // confusing "unknown attribute", and so the boundary is visible in one place.
            new AttributeDefinition(
                TargetType.SegmentPerception, "trust", 0m, 100m, VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.SegmentPerception, "respect", 0m, 100m, VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.ElectoralPreference, "share", 0m, 1m, VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.Turnout, "rate", 0m, 1m, VariablePolarity.Neutral, AllOperations),
            new AttributeDefinition(
                TargetType.Seats, "count", 0m, 350m, VariablePolarity.Neutral, AllOperations),
        ]);

    public Result<AttributeDefinition> Lookup(AttributeRef target)
    {
        if (!_definitions.TryGetValue(target.RegistryKey, out var definition))
        {
            return Result.Failure<AttributeDefinition>(
                new Error(
                    "attribute_registry.unknown_attribute",
                    $"No attribute '{target.Attribute}' is defined for {target.Target}."));
        }

        return Result.Success(definition);
    }

    /// <summary>
    /// Checks that content may write this attribute with this operation. Separate from
    /// <see cref="Lookup"/> because the engine itself writes computed attributes legitimately
    /// — only authored content is forbidden to.
    /// </summary>
    public Result ValidateAuthoredWrite(AttributeRef target, EffectOperation operation)
    {
        var definition = Lookup(target);

        if (definition.IsFailure)
        {
            return Result.Failure(definition.Error);
        }

        if (definition.Value.IsComputed)
        {
            return Result.Failure(
                new Error(
                    "attribute_registry.computed_attribute",
                    $"{target.Target}.{target.Attribute} is computed by the simulation. "
                    + "Content influences it by emitting a stimulus, not by writing it."));
        }

        if (!definition.Value.Permits(operation))
        {
            return Result.Failure(
                new Error(
                    "attribute_registry.operation_not_permitted",
                    $"Operation {operation} is not permitted on {target.Target}.{target.Attribute}."));
        }

        return Result.Success();
    }
}
