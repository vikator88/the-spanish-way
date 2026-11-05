namespace TheSpanishWay.Domain.Shared.Services;

/// <summary>
/// The boundary between stored state and the electoral maths.
/// <para>
/// v0.1 says to use <c>decimal</c> throughout, but <c>decimal</c> has no <c>Exp</c>,
/// <c>Log</c> or <c>Sqrt</c>, and softmax, a sigmoid turnout curve and poll noise all need
/// them. The policy is therefore: <c>decimal</c> for stored state, <c>double</c> inside the
/// calculators, and a deliberate rounding at the edge so that what gets stored is stable.
/// </para>
/// <para>
/// This matters for replay. <c>Math.Exp</c> is not bit-exact across CPU architectures, so
/// rounding at the boundary keeps small platform differences from accumulating — and the
/// determinism guarantee is stated as "same platform, same simulation version" rather than
/// as something universal.
/// </para>
/// </summary>
public static class Precision
{
    /// <summary>Decimal places kept when a calculated value re-enters stored state.</summary>
    public const int Places = 6;

    /// <summary>Rounds a calculated value back to stored precision.</summary>
    public static decimal ToStored(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "A calculator produced a value that is not a number.");
        }

        return Math.Round((decimal)value, Places, MidpointRounding.ToEven);
    }

    /// <summary>Rounds a stored value to the same precision, for comparing like with like.</summary>
    public static decimal ToStored(decimal value) => Math.Round(value, Places, MidpointRounding.ToEven);

    /// <summary>Centres a 0-100 attribute onto -1..+1, which is how utility components combine.</summary>
    public static double Centre(decimal zeroToOneHundred) => ((double)zeroToOneHundred - 50d) / 50d;

    /// <summary>The inverse of <see cref="Centre"/>.</summary>
    public static decimal Uncentre(double centred) => ToStored((centred * 50d) + 50d);
}
