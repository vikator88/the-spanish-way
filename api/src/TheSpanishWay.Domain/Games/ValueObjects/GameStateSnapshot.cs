using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Games.ValueObjects;

/// <summary>
/// A game's whole mutable state as plain data, and the unit of persistence.
/// <para>
/// This is a Domain type with no serialisation attributes: Infrastructure decides that it
/// becomes a <c>jsonb</c> column. Keeping it a record also means a round-trip can be asserted
/// by value equality, which is what the save-and-resume tests rely on.
/// </para>
/// <para>
/// It must be sufficient on its own to resume a game. Nothing may be reconstructed from
/// history records, and nothing may depend on process memory or a server clock.
/// </para>
/// </summary>
public sealed record GameStateSnapshot(
    SchemaVersion SchemaVersion,
    GameDate StartDate,
    GameDate CurrentDate,
    GamePhase Phase,
    IReadOnlyDictionary<RandomStream, int> RandomStreamIndices,
    IReadOnlyList<EventOccurrenceId> OpenBlockingDecisions)
{
    /// <summary>
    /// Value equality over the whole snapshot, which a positional record does not give us
    /// because its collection members compare by reference.
    /// </summary>
    public bool Matches(GameStateSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return SchemaVersion == other.SchemaVersion
            && StartDate == other.StartDate
            && CurrentDate == other.CurrentDate
            && Phase == other.Phase
            && RandomStreamIndices.Count == other.RandomStreamIndices.Count
            && RandomStreamIndices.All(pair =>
                other.RandomStreamIndices.TryGetValue(pair.Key, out var value) && value == pair.Value)
            && OpenBlockingDecisions.SequenceEqual(other.OpenBlockingDecisions);
    }
}
