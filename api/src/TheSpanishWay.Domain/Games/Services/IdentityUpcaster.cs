using TheSpanishWay.Domain.Games.ValueObjects;

namespace TheSpanishWay.Domain.Games.Services;

/// <summary>
/// Moves a snapshot forward a version without changing its contents — the right upcaster
/// for a purely additive shape change, where new fields take their defaults and nothing
/// stored needs rewriting.
/// </summary>
public sealed class IdentityUpcaster(SchemaVersion from, SchemaVersion to) : ISnapshotUpcaster
{
    public SchemaVersion From { get; } = from;

    public SchemaVersion To { get; } = to;

    public GameStateSnapshot Upcast(GameStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot with { SchemaVersion = To };
    }
}
