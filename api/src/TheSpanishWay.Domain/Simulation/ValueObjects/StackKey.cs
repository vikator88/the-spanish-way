using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>
/// Decides which modifiers count as "the same effect again" for stacking purposes.
/// <para>
/// The default identity is the source definition, the effect's index within it, and the
/// attribute being changed — so the same scandal hitting the same party's credibility twice
/// collides, while two different scandals do not.
/// </para>
/// </summary>
public readonly record struct StackKey(string Value)
{
    public static StackKey Default(string sourceDefinitionId, int effectIndex, AttributeRef target) =>
        new($"{sourceDefinitionId}#{effectIndex}@{target}");

    /// <summary>An explicit key, letting content make unrelated effects collide on purpose.</summary>
    public static StackKey Named(string value) => new(value);

    public override string ToString() => Value;
}
