using TheSpanishWay.Domain.Shared.Enumerations;

namespace TheSpanishWay.Domain.Shared.Services;

/// <summary>
/// Every random decision in the Domain goes through this. Platform randomness is banned
/// outright — <c>new Random()</c> is not reproducible across runs, and even a seeded
/// <c>Random</c> uses an algorithm .NET treats as an implementation detail. An architecture
/// test enforces the ban.
/// </summary>
public interface IRandomSource
{
    /// <summary>A value in <c>[minInclusive, maxExclusive)</c>.</summary>
    int NextInt(RandomStream stream, int minInclusive, int maxExclusive);

    /// <summary>A value in <c>[0, 1)</c>.</summary>
    decimal NextUnitInterval(RandomStream stream);

    /// <summary>True with the given probability, which is clamped to <c>[0, 1]</c>.</summary>
    bool Chance(RandomStream stream, decimal probability);

    /// <summary>
    /// Picks one item. The collection must already be in a stable order — iteration order
    /// is part of the determinism contract, so callers sort by identifier first.
    /// </summary>
    T Pick<T>(RandomStream stream, IReadOnlyList<T> orderedCandidates);
}
