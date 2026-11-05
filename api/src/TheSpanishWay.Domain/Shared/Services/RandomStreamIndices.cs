using System.Collections.Frozen;
using TheSpanishWay.Domain.Shared.Enumerations;

namespace TheSpanishWay.Domain.Shared.Services;

/// <summary>
/// How many draws each stream has taken on the current game day. This, plus the seed, is
/// the entirety of what a save must persist about randomness; indices reset each day
/// because the day number is itself a draw coordinate.
/// </summary>
public sealed class RandomStreamIndices
{
    private static readonly RandomStream[] AllStreams = Enum.GetValues<RandomStream>();

    private readonly Dictionary<RandomStream, int> _indices;

    private RandomStreamIndices(Dictionary<RandomStream, int> indices) => _indices = indices;

    public static RandomStreamIndices Empty() =>
        new(AllStreams.ToDictionary(stream => stream, _ => 0));

    public static RandomStreamIndices Restore(IReadOnlyDictionary<RandomStream, int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);

        return new RandomStreamIndices(
            AllStreams.ToDictionary(
                stream => stream,
                stream => indices.TryGetValue(stream, out var value) ? value : 0));
    }

    public int IndexOf(RandomStream stream) => _indices[stream];

    /// <summary>Returns the index to use for the next draw and advances the stream.</summary>
    internal int Advance(RandomStream stream) => _indices[stream]++;

    /// <summary>Called when the simulation moves to a new day.</summary>
    public void ResetForNewDay()
    {
        foreach (var stream in AllStreams)
        {
            _indices[stream] = 0;
        }
    }

    /// <summary>A snapshot for persistence, ordered so serialisation is byte-stable.</summary>
    public FrozenDictionary<RandomStream, int> ToSnapshot() =>
        _indices
            .OrderBy(pair => pair.Key)
            .ToFrozenDictionary(pair => pair.Key, pair => pair.Value);
}
