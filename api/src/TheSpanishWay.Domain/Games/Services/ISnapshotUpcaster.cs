using TheSpanishWay.Domain.Games.ValueObjects;

namespace TheSpanishWay.Domain.Games.Services;

/// <summary>
/// Migrates a stored snapshot one schema version forward. Changing the stored shape is not
/// the same as changing the rules, so running one of these never touches
/// <see cref="SimulationVersion"/>.
/// </summary>
public interface ISnapshotUpcaster
{
    SchemaVersion From { get; }

    SchemaVersion To { get; }

    GameStateSnapshot Upcast(GameStateSnapshot snapshot);
}
