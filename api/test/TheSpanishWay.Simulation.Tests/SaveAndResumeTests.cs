using Shouldly;
using TheSpanishWay.Domain.Games.Aggregates;
using TheSpanishWay.Domain.Games.Entities;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.Services;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Simulation.Tests;

/// <summary>
/// The snapshot must be enough, on its own, to carry on. No process memory, no history
/// records, no server clock — otherwise a game cannot be resumed and a replay cannot be
/// trusted. See domain/game-state, "Snapshot self-sufficiency".
/// </summary>
public sealed class SaveAndResumeTests
{
    private const int Legislature = 1461;

    private static readonly GameDate Start = GameDate.From(2027, 3, 1);

    /// <summary>Simulates the real boundary: everything crosses as plain snapshot data.</summary>
    private static GameSession ReloadThroughSnapshot(GameSession session)
    {
        var snapshot = session.ToSnapshot();

        var migrated = SnapshotUpcasterChain.Empty().ToCurrent(snapshot);
        migrated.IsSuccess.ShouldBeTrue();

        return GameSession.Restore(
            session.Id, session.Owner, session.Name, session.PlayerPartyId,
            session.Scenario, session.SimulationVersion, session.Seed,
            session.Status, session.Revision,
            GameState.FromSnapshot(migrated.Value));
    }

    [Fact]
    public void Advancing_through_a_reload_matches_an_uninterrupted_run()
    {
        var uninterrupted = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        uninterrupted.AdvanceUntil(new StopCondition.AfterDays(Legislature), Legislature);

        var interrupted = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        interrupted.AdvanceUntil(new StopCondition.AfterDays(700), 700);
        var resumed = ReloadThroughSnapshot(interrupted);
        resumed.AdvanceUntil(new StopCondition.AfterDays(Legislature - 700), Legislature);

        resumed.CurrentDate.ShouldBe(uninterrupted.CurrentDate);
        resumed.ToSnapshot().Matches(uninterrupted.ToSnapshot()).ShouldBeTrue();
    }

    [Fact]
    public void A_snapshot_round_trip_changes_nothing()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        session.AdvanceUntil(new StopCondition.AfterDays(400), 400);

        var reloaded = ReloadThroughSnapshot(session);

        reloaded.ToSnapshot().Matches(session.ToSnapshot()).ShouldBeTrue();
        reloaded.CurrentDate.ShouldBe(session.CurrentDate);
        reloaded.Phase.ShouldBe(session.Phase);
        reloaded.Revision.ShouldBe(session.Revision);
    }

    [Fact]
    public void An_open_decision_survives_a_reload()
    {
        var occurrence = EventOccurrenceId.From(Guid.Parse("0000000c-0000-0000-0000-000000000001"));
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        session.RaiseDecision(occurrence);
        session.Pause();

        var reloaded = ReloadThroughSnapshot(session);
        reloaded.Resume();

        // The decision is still open, so the game must resume into AwaitingDecision.
        reloaded.Status.ShouldBe(GameStatus.AwaitingDecision);
        reloaded.State.OpenBlockingDecisions.ShouldContain(occurrence);
    }

    [Fact]
    public void A_snapshot_carries_the_start_date_so_cadence_survives_a_reload()
    {
        // Weekly boundaries count from the scenario's start, so losing it would silently
        // shift every weekly subsystem after a reload.
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        session.AdvanceUntil(new StopCondition.AfterDays(14), 14);

        var reloaded = ReloadThroughSnapshot(session);

        reloaded.State.StartDate.ShouldBe(Start);
        reloaded.State.IsWeeklyBoundary().ShouldBeTrue();
    }

    [Fact]
    public void A_snapshot_carries_the_random_stream_indices()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        // Draw through the real source, which is what advances the indices in a live game.
        var random = new CounterBasedRandomSource(
            session.Seed, session.CurrentDate, session.State.RandomStreams);
        random.NextUnitInterval(RandomStream.Events);
        random.NextUnitInterval(RandomStream.Events);

        var reloaded = ReloadThroughSnapshot(session);

        reloaded.State.RandomStreams.IndexOf(RandomStream.Events).ShouldBe(2);
    }

    [Fact]
    public void Resuming_needs_no_wall_clock()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        session.AdvanceUntil(new StopCondition.AfterDays(50), 50);

        var snapshot = session.ToSnapshot();

        snapshot.GetType().GetProperties()
            .Select(p => p.PropertyType)
            .ShouldNotContain(typeof(DateTime));
        snapshot.GetType().GetProperties()
            .Select(p => p.PropertyType)
            .ShouldNotContain(typeof(DateTimeOffset));
    }
}
