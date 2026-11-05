using Shouldly;
using TheSpanishWay.Domain.Games.Aggregates;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Games;

public sealed class GameSessionCreationTests
{
    private static readonly PartyId PlayerParty =
        PartyId.From(Guid.Parse("0000000a-0000-0000-0000-000000000001"));

    private static readonly PartyId RivalParty =
        PartyId.From(Guid.Parse("0000000a-0000-0000-0000-000000000002"));

    [Fact]
    public void A_player_party_absent_from_the_scenario_is_rejected()
    {
        var result = GameSession.Create(
            GameId.From(Guid.NewGuid()),
            OwnerId.From(Guid.NewGuid()),
            "Test",
            PlayerParty,
            new ScenarioVersionRef(ScenarioId.From(Guid.NewGuid()), 1),
            SimulationVersion.Current,
            RandomSeed.FromText("x"),
            GameDate.From(2027, 3, 1),
            [RivalParty]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("game.player_party_not_in_scenario");
    }

    [Fact]
    public void A_game_needs_a_name_and_an_owner()
    {
        Should.Throw<InvalidOperationException>(() => new GameSessionBuilder().WithName("  ").Build());
        Should.Throw<InvalidOperationException>(() => new GameSessionBuilder().WithOwner(OwnerId.Empty).Build());
    }

    [Fact]
    public void There_is_no_way_to_reassign_the_player_party()
    {
        typeof(GameSession)
            .GetProperty(nameof(GameSession.PlayerPartyId))!
            .SetMethod
            .ShouldBeNull();
    }

    [Fact]
    public void The_seed_is_fixed_for_the_life_of_the_game()
    {
        typeof(GameSession).GetProperty(nameof(GameSession.Seed))!.SetMethod.ShouldBeNull();
    }

    [Fact]
    public void A_game_pins_a_published_scenario_version_not_a_bare_id()
    {
        var session = new GameSessionBuilder().Build();

        session.Scenario.Version.ShouldBe(1);
        typeof(GameSession).GetProperty(nameof(GameSession.Scenario))!
            .PropertyType.ShouldBe(typeof(ScenarioVersionRef));
    }

    [Fact]
    public void An_unpublished_scenario_version_is_rejected()
    {
        ScenarioVersionRef.Create(ScenarioId.From(Guid.NewGuid()), 0)
            .Error.Code.ShouldBe("scenario_version.not_published");
    }

    [Fact]
    public void The_session_stores_no_date_or_phase_of_its_own()
    {
        // Both live in GameState and are read through, so no pair of fields can disagree.
        typeof(GameSession).GetProperty(nameof(GameSession.CurrentDate))!.SetMethod.ShouldBeNull();
        typeof(GameSession).GetProperty(nameof(GameSession.Phase))!.SetMethod.ShouldBeNull();
    }

    [Fact]
    public void The_date_and_phase_read_through_to_state()
    {
        var session = new GameSessionBuilder().StartingOn(GameDate.From(2027, 6, 15)).BuildRunning();

        session.CurrentDate.ShouldBe(session.State.CurrentDate);
        session.Phase.ShouldBe(session.State.Phase);
    }

    [Fact]
    public void Versions_are_named_so_none_reads_like_a_concurrency_token()
    {
        var session = new GameSessionBuilder().Build();

        session.SimulationVersion.ShouldBe(SimulationVersion.Current);
        session.State.SchemaVersion.ShouldBe(SchemaVersion.Current);
        session.Revision.ShouldBe(Revision.Initial);
        typeof(GameSession).GetProperty("StateVersion").ShouldBeNull();
    }

    [Fact]
    public void A_restored_game_records_nothing()
    {
        var original = new GameSessionBuilder().BuildRunning();

        var restored = GameSession.Restore(
            original.Id, original.Owner, original.Name, original.PlayerPartyId,
            original.Scenario, original.SimulationVersion, original.Seed,
            GameStatus.Running, new Revision(7), original.State);

        restored.DomainEvents.ShouldBeEmpty("loading a game is not a domain fact");
        restored.Revision.Value.ShouldBe(7);
    }
}
