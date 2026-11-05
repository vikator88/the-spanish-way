using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Shared.Services;

/// <summary>
/// The draw function the whole simulation rests on: a pure hash of
/// <c>(seed, stream, game day, index)</c>.
/// <para>
/// Counter-based rather than sequential, so there is no generator state to carry. Resuming
/// a save needs only the seed and each stream's index; two processes given the same
/// coordinates produce the same value with no coordination. The mixing function is written
/// out here on purpose: a saved game must not depend on a BCL algorithm that is free to
/// change between runtime versions.
/// </para>
/// </summary>
public static class CounterBasedRandom
{
    // SplitMix64's finalizer. Chosen for being short enough to audit and fixed forever.
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private const ulong MixA = 0xBF58476D1CE4E5B9UL;
    private const ulong MixB = 0x94D049BB133111EBUL;

    /// <summary>A uniformly distributed 64-bit value for one coordinate.</summary>
    public static ulong Draw(RandomSeed seed, RandomStream stream, int dayNumber, int index)
    {
        // Each coordinate is folded in separately so that changing any one of them
        // changes the output everywhere, rather than only in its own bit range.
        var z = seed.Value;
        z = Mix(z ^ (GoldenGamma * ((ulong)stream + 1)));
        z = Mix(z ^ ((ulong)(uint)dayNumber * GoldenGamma));
        z = Mix(z ^ ((ulong)(uint)index + GoldenGamma));

        return z;
    }

    /// <summary>Maps a draw onto <c>[minInclusive, maxExclusive)</c> without modulo bias.</summary>
    public static int ToInt(ulong draw, int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentException(
                $"Empty range: [{minInclusive}, {maxExclusive}).", nameof(maxExclusive));
        }

        var range = (ulong)((long)maxExclusive - minInclusive);

        // Lemire's multiply-shift: the high 64 bits of a 128-bit product are uniform over
        // the range, which plain modulo is not.
        var scaled = (ulong)(((UInt128)draw * range) >> 64);

        return (int)((long)minInclusive + (long)scaled);
    }

    /// <summary>Maps a draw onto <c>[0, 1)</c> as a decimal with fixed precision.</summary>
    public static decimal ToUnitInterval(ulong draw)
    {
        // 2^53 keeps the quotient exactly representable and the result stable across
        // platforms; using the full 64 bits would round differently depending on scale.
        const ulong resolution = 1UL << 53;

        return (decimal)(draw >> 11) / resolution;
    }

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * MixA;
        z = (z ^ (z >> 27)) * MixB;

        return z ^ (z >> 31);
    }
}
