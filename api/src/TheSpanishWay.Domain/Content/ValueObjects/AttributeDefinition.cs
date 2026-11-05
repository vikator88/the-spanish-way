using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.World.Enumerations;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Content.ValueObjects;

/// <summary>
/// What the code says about one addressable attribute: its range, what a high value means,
/// and which operations may touch it.
/// <para>
/// Content is validated against these. An effect naming an attribute that has no definition
/// fails at load rather than evaluating to nothing at runtime, which is the difference
/// between a typo caught in CI and a scenario that silently does less than its author
/// believed.
/// </para>
/// </summary>
public sealed record AttributeDefinition(
    TargetType Target,
    string Attribute,
    decimal Minimum,
    decimal Maximum,
    VariablePolarity Polarity,
    IReadOnlySet<EffectOperation> PermittedOperations)
{
    public string Key => $"{Target}.{Attribute}";

    public bool Permits(EffectOperation operation) => PermittedOperations.Contains(operation);

    /// <summary>True for attributes only the simulation may write, never content.</summary>
    public bool IsComputed => Target.IsComputed();
}
