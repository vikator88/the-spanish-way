using TheSpanishWay.Domain.Games.Aggregates;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.TestKit;

/// <summary>
/// Builds a <see cref="GameSession"/> for tests, so a test that cares about one thing says
/// only that thing. Every default is fixed rather than generated: a test that depends on a
/// random seed or today's date is a test that fails on its own schedule.
/// </summary>
public sealed class GameSessionBuilder
{
    private static readonly PartyId DefaultPlayerParty =
        PartyId.From(Guid.Parse("0000000a-0000-0000-0000-000000000001"));

    private GameId _id = GameId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private OwnerId _owner = OwnerId.From(Guid.Parse("0000000b-0000-0000-0000-000000000011"));
    private string _name = "Test game";
    private PartyId _playerParty = DefaultPlayerParty;
    private List<PartyId> _scenarioParties = [DefaultPlayerParty];
    private ScenarioVersionRef _scenario =
        new(ScenarioId.From(Guid.Parse("00000000-0000-0000-0000-000000000002")), 1);
    private SimulationVersion _simulationVersion = SimulationVersion.Current;
    private RandomSeed _seed = RandomSeed.FromText("test");
    private GameDate _startDate = GameDate.From(2027, 3, 1);

    public GameSessionBuilder WithId(GameId id)
    {
        _id = id;
        return this;
    }

    public GameSessionBuilder WithOwner(OwnerId owner)
    {
        _owner = owner;
        return this;
    }

    public GameSessionBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public GameSessionBuilder WithPlayerParty(PartyId playerParty)
    {
        _playerParty = playerParty;
        return this;
    }

    public GameSessionBuilder WithScenarioParties(params PartyId[] parties)
    {
        _scenarioParties = [.. parties];
        return this;
    }

    public GameSessionBuilder WithScenario(ScenarioVersionRef scenario)
    {
        _scenario = scenario;
        return this;
    }

    public GameSessionBuilder WithSimulationVersion(SimulationVersion version)
    {
        _simulationVersion = version;
        return this;
    }

    public GameSessionBuilder WithSeed(RandomSeed seed)
    {
        _seed = seed;
        return this;
    }

    public GameSessionBuilder StartingOn(GameDate startDate)
    {
        _startDate = startDate;
        return this;
    }

    /// <summary>Builds a game in <c>Created</c>.</summary>
    public GameSession Build()
    {
        var result = GameSession.Create(
            _id, _owner, _name, _playerParty, _scenario, _simulationVersion, _seed,
            _startDate, _scenarioParties);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Builder produced an invalid game: {result.Error}");
    }

    /// <summary>Builds a game already started, which is what most tests actually want.</summary>
    public GameSession BuildRunning()
    {
        var session = Build();
        session.Start();
        session.ClearDomainEvents();

        return session;
    }
}
