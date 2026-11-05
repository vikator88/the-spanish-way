using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Shared.Services;

/// <summary>
/// The <see cref="IRandomSource"/> the simulation runs on. Holds the seed and the current
/// day, and advances a per-stream index on each draw.
/// </summary>
public sealed class CounterBasedRandomSource : IRandomSource
{
    private readonly RandomSeed _seed;

    public CounterBasedRandomSource(RandomSeed seed, GameDate currentDate, RandomStreamIndices indices)
    {
        ArgumentNullException.ThrowIfNull(indices);

        _seed = seed;
        CurrentDate = currentDate;
        Indices = indices;
    }

    public GameDate CurrentDate { get; private set; }

    public RandomStreamIndices Indices { get; }

    /// <summary>Moves to a new day and restarts every stream's index.</summary>
    public void AdvanceTo(GameDate date)
    {
        if (date < CurrentDate)
        {
            throw new ArgumentException(
                $"Game time cannot move backwards: {CurrentDate} to {date}.", nameof(date));
        }

        CurrentDate = date;
        Indices.ResetForNewDay();
    }

    public int NextInt(RandomStream stream, int minInclusive, int maxExclusive) =>
        CounterBasedRandom.ToInt(NextDraw(stream), minInclusive, maxExclusive);

    public decimal NextUnitInterval(RandomStream stream) =>
        CounterBasedRandom.ToUnitInterval(NextDraw(stream));

    public bool Chance(RandomStream stream, decimal probability)
    {
        // Consume a draw either way: a probability of 0 or 1 must not shift the stream
        // relative to a run where it was 0.5, or content tuning would change later draws.
        var roll = NextUnitInterval(stream);

        return probability switch
        {
            <= 0m => false,
            >= 1m => true,
            _ => roll < probability,
        };
    }

    public T Pick<T>(RandomStream stream, IReadOnlyList<T> orderedCandidates)
    {
        ArgumentNullException.ThrowIfNull(orderedCandidates);

        if (orderedCandidates.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty set.", nameof(orderedCandidates));
        }

        return orderedCandidates[NextInt(stream, 0, orderedCandidates.Count)];
    }

    private ulong NextDraw(RandomStream stream) =>
        CounterBasedRandom.Draw(_seed, stream, CurrentDate.DayNumber, Indices.Advance(stream));
}
