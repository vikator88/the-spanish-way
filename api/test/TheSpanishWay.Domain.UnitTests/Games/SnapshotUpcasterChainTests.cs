using Shouldly;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.Services;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Games;

/// <summary>
/// Changing the stored shape must not strand saved games, and must stay distinct from
/// changing the rules. See infrastructure/persistence-contracts.
/// </summary>
public sealed class SnapshotUpcasterChainTests
{
    private static GameStateSnapshot SnapshotAt(SchemaVersion version) =>
        new(
            version,
            GameDate.From(2027, 3, 1),
            GameDate.From(2027, 4, 1),
            GamePhase.Normal,
            new Dictionary<RandomStream, int> { [RandomStream.Events] = 3 },
            []);

    [Fact]
    public void A_current_snapshot_passes_through_untouched()
    {
        var snapshot = SnapshotAt(SchemaVersion.Current);

        var result = SnapshotUpcasterChain.Empty().ToCurrent(snapshot);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(snapshot);
    }

    [Fact]
    public void Upcasters_run_in_version_order()
    {
        var chain = new SnapshotUpcasterChain(
        [
            // Deliberately registered out of order: the chain must sort them.
            new IdentityUpcaster(new SchemaVersion(2), new SchemaVersion(3)),
            new IdentityUpcaster(new SchemaVersion(1), new SchemaVersion(2)),
        ]);

        var result = chain.To(SnapshotAt(new SchemaVersion(1)), new SchemaVersion(3));

        result.IsSuccess.ShouldBeTrue();
        result.Value.SchemaVersion.ShouldBe(new SchemaVersion(3));
    }

    [Fact]
    public void An_identity_upcast_preserves_every_field()
    {
        var original = SnapshotAt(new SchemaVersion(1));
        var chain = new SnapshotUpcasterChain([new IdentityUpcaster(new SchemaVersion(1), new SchemaVersion(2))]);

        var upcast = chain.To(original, new SchemaVersion(2)).Value;

        upcast.CurrentDate.ShouldBe(original.CurrentDate);
        upcast.StartDate.ShouldBe(original.StartDate);
        upcast.RandomStreamIndices[RandomStream.Events].ShouldBe(3);
    }

    [Fact]
    public void A_snapshot_from_a_newer_build_is_refused_rather_than_read_partially()
    {
        // Dropping fields a newer build wrote would corrupt a game that is still playable
        // elsewhere, so this fails loudly instead.
        var result = SnapshotUpcasterChain.Empty()
            .To(SnapshotAt(new SchemaVersion(9)), new SchemaVersion(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("snapshot.from_the_future");
    }

    [Fact]
    public void A_gap_in_the_chain_is_reported()
    {
        var chain = new SnapshotUpcasterChain([new IdentityUpcaster(new SchemaVersion(1), new SchemaVersion(2))]);

        var result = chain.To(SnapshotAt(new SchemaVersion(1)), new SchemaVersion(4));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("snapshot.no_upcaster");
    }

    [Fact]
    public void An_upcaster_that_does_not_move_forward_is_rejected_at_registration()
    {
        Should.Throw<ArgumentException>(() =>
            new SnapshotUpcasterChain([new IdentityUpcaster(new SchemaVersion(2), new SchemaVersion(2))]));
    }

    [Fact]
    public void Two_upcasters_claiming_the_same_source_version_are_rejected()
    {
        Should.Throw<ArgumentException>(() =>
            new SnapshotUpcasterChain(
            [
                new IdentityUpcaster(new SchemaVersion(1), new SchemaVersion(2)),
                new IdentityUpcaster(new SchemaVersion(1), new SchemaVersion(3)),
            ]));
    }

    [Fact]
    public void A_schema_change_is_not_a_rules_change()
    {
        // SchemaVersion and SimulationVersion move for different reasons and are different
        // types, so one cannot be passed where the other is meant.
        typeof(SchemaVersion).ShouldNotBe(typeof(SimulationVersion));
    }
}
