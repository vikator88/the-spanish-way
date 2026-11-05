using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Games.ValueObjects;

namespace TheSpanishWay.Domain.Games.Services;

/// <summary>
/// Brings a stored snapshot up to the shape this build understands by running each
/// registered upcaster in version order.
/// <para>
/// A snapshot from the future is rejected outright rather than read partially: silently
/// dropping fields a newer build wrote would corrupt a game that is still playable
/// elsewhere.
/// </para>
/// </summary>
public sealed class SnapshotUpcasterChain
{
    private readonly Dictionary<SchemaVersion, ISnapshotUpcaster> _byFromVersion;

    public SnapshotUpcasterChain(IEnumerable<ISnapshotUpcaster> upcasters)
    {
        ArgumentNullException.ThrowIfNull(upcasters);

        _byFromVersion = [];

        foreach (var upcaster in upcasters.OrderBy(u => u.From.Value))
        {
            if (upcaster.To <= upcaster.From)
            {
                throw new ArgumentException(
                    $"Upcaster {upcaster.From} -> {upcaster.To} does not move forward.", nameof(upcasters));
            }

            if (!_byFromVersion.TryAdd(upcaster.From, upcaster))
            {
                throw new ArgumentException(
                    $"Two upcasters both claim to migrate from {upcaster.From}.", nameof(upcasters));
            }
        }
    }

    /// <summary>A chain for a build with nothing to migrate yet.</summary>
    public static SnapshotUpcasterChain Empty() => new([]);

    public Result<GameStateSnapshot> ToCurrent(GameStateSnapshot snapshot) =>
        To(snapshot, SchemaVersion.Current);

    public Result<GameStateSnapshot> To(GameStateSnapshot snapshot, SchemaVersion target)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.SchemaVersion > target)
        {
            return Result.Failure<GameStateSnapshot>(
                new Error(
                    "snapshot.from_the_future",
                    $"Snapshot is {snapshot.SchemaVersion} but this build understands {target}."));
        }

        var current = snapshot;

        while (current.SchemaVersion < target)
        {
            if (!_byFromVersion.TryGetValue(current.SchemaVersion, out var upcaster))
            {
                return Result.Failure<GameStateSnapshot>(
                    new Error(
                        "snapshot.no_upcaster",
                        $"No upcaster registered from {current.SchemaVersion}; cannot reach {target}."));
            }

            var upcast = upcaster.Upcast(current);

            if (upcast.SchemaVersion != upcaster.To)
            {
                return Result.Failure<GameStateSnapshot>(
                    new Error(
                        "snapshot.upcaster_misreported",
                        $"Upcaster claimed {upcaster.To} but produced {upcast.SchemaVersion}."));
            }

            current = upcast;
        }

        return Result.Success(current);
    }
}
