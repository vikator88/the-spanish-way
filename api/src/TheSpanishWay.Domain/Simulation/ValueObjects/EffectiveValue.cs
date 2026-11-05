using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;

namespace TheSpanishWay.Domain.Simulation.ValueObjects;

/// <summary>
/// Combines a base value with its active modifiers. This is what every rule reads; the base
/// alone is only half the answer.
/// </summary>
public static class EffectiveValue
{
    /// <summary>
    /// Computes the value in one fixed order: base, then any <see cref="EffectOperation.Set"/>
    /// override, then the sum of additions, then the product of multipliers, then clamped.
    /// <para>
    /// The order is fixed and documented because addition and multiplication do not commute:
    /// <c>(10 + 5) * 2</c> and <c>10 + (5 * 2)</c> differ, and content authors must be able
    /// to predict which they get.
    /// </para>
    /// </summary>
    /// <param name="baseValue">The value permanent change has produced.</param>
    /// <param name="bounds">Clamped to these, exactly as the base is.</param>
    /// <param name="modifiers">Applied in <see cref="Modifier.Sequence"/> order for determinism.</param>
    public static decimal Compute(
        decimal baseValue,
        Bounded<decimal> bounds,
        IEnumerable<Modifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        var ordered = modifiers.OrderBy(m => m.Sequence).ThenBy(m => m.Id).ToList();

        // A later Set wins over an earlier one; both still lose to nothing at all.
        var value = baseValue;

        foreach (var modifier in ordered.Where(m => m.Operation == EffectOperation.Set))
        {
            value = modifier.Value;
        }

        foreach (var modifier in ordered.Where(m => m.Operation == EffectOperation.Add))
        {
            value += modifier.Value;
        }

        foreach (var modifier in ordered.Where(m => m.Operation == EffectOperation.Multiply))
        {
            value *= modifier.Value;
        }

        return Clamp(value, bounds);
    }

    private static decimal Clamp(decimal value, Bounded<decimal> bounds) =>
        value < bounds.Minimum ? bounds.Minimum
        : value > bounds.Maximum ? bounds.Maximum
        : value;
}
