namespace TheSpanishWay.Domain.Simulation.Enumerations;

/// <summary>
/// How an effect changes a value. The order these combine in is fixed and documented,
/// because <see cref="Add"/> and <see cref="Multiply"/> do not commute.
/// </summary>
public enum EffectOperation
{
    /// <summary>Overrides the base outright.</summary>
    Set = 0,

    /// <summary>Added to the running value.</summary>
    Add = 1,

    /// <summary>Scales the running value, applied after every addition.</summary>
    Multiply = 2,
}
