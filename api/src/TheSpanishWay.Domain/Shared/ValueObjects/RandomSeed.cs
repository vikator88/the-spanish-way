namespace TheSpanishWay.Domain.Shared.ValueObjects;

/// <summary>
/// A game's seed, fixed at creation and never changed. Together with the scenario version,
/// the simulation version and the ordered command log, it reproduces the whole game.
/// </summary>
public readonly record struct RandomSeed(ulong Value)
{
    public static RandomSeed From(ulong value) => new(value);

    /// <summary>Derives a seed from text, so a scenario or test can name its seed readably.</summary>
    public static RandomSeed FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // FNV-1a, 64-bit. Chosen because it is specified rather than implementation-defined:
        // string.GetHashCode is randomised per process and would break reproducibility.
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;

        foreach (var c in text)
        {
            hash ^= c;
            hash *= prime;
        }

        return new RandomSeed(hash);
    }

    public override string ToString() => Value.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
}
