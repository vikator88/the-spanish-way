using TheSpanishWay.Domain.Actions.Entities;
using TheSpanishWay.Domain.Actions.Services;
using TheSpanishWay.Domain.Actors.Entities;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Politics.Enumerations;
using TheSpanishWay.Domain.Politics.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Services;

namespace TheSpanishWay.TestKit;

/// <summary>Fixed parties and characters, so a test says only what it is about.</summary>
public static class PoliticsFixtures
{
    public static readonly GameDate Today = GameDate.From(2027, 3, 1);

    public static PartyId PartyA { get; } = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000a"));

    public static PartyId PartyB { get; } = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000b"));

    public static CharacterId Leader { get; } = CharacterId.From(Guid.Parse("0000000c-0000-0000-0000-00000000000a"));

    public static CharacterId Deputy { get; } = CharacterId.From(Guid.Parse("0000000c-0000-0000-0000-00000000000b"));

    public static PartyState Party(
        PartyId? id = null,
        decimal cohesion = 60m,
        decimal credibility = 55m,
        decimal budget = 1_000_000m,
        bool withLeader = true)
    {
        var party = PartyState.Create(
            id ?? PartyA, "Test Party",
            IdeologicalProfile.From(-20m, -10m, 0m, 0m, 10m, 0m),
            cohesion, credibility, budget).Value;

        if (withLeader)
        {
            party.AddMember(PartyMembership.Create(Leader, PartyRole.Leader, Today).Value);
            party.AddMember(PartyMembership.Create(Deputy, PartyRole.Member, Today).Value);
        }

        return party;
    }

    public static CharacterState Character(CharacterId? id = null, decimal popularity = 60m, decimal charisma = 55m) =>
        CharacterState.Create(
            id ?? Leader, "Test Politician",
            IdeologicalProfile.From(-20m, -10m, 0m, 0m, 10m, 0m),
            popularity, charisma).Value;

    public static ActionAvailabilityContext Context(
        PartyState? party = null,
        GameStatus status = GameStatus.Running,
        ActionLedger? ledger = null) =>
        new(party ?? Party(), Today, Today, status, ledger ?? ActionLedger.Empty());

    /// <summary>An execution service wired to a world with the given variable.</summary>
    public static (ActionExecutionService Service, EffectScenario Scenario) ExecutionService(
        FakeWorld? world = null)
    {
        var fake = world ?? new FakeWorld();
        var evaluator = new ConditionEvaluator(fake);
        var scenario = EffectScenario.WithVariable(value: 5m, maximum: 100m);

        var service = new ActionExecutionService(
            new ActionAvailabilityPolicy(evaluator),
            new EffectInstantiator(evaluator),
            scenario.Engine);

        return (service, scenario);
    }
}
