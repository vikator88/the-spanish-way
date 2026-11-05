using Shouldly;
using TheSpanishWay.Domain.Actions.Entities;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Actions.Enumerations;
using TheSpanishWay.Domain.Actions.ValueObjects;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.TestKit;
using ContentEffect = TheSpanishWay.Domain.Content.ValueObjects.EffectDefinition;

namespace TheSpanishWay.Domain.UnitTests.Actions;

public sealed class ActionExecutionTests
{
    private static readonly ActionDefinitionId SpeechId =
        ActionDefinitionId.From(Guid.Parse("0000000e-0000-0000-0000-00000000000a"));

    private static AttributeRef Inflation(string attribute = "current") =>
        AttributeRef.From(TargetType.WorldVariable, "inflation", attribute);

    private static ActionDefinition Speech(
        ActionCost? cost = null,
        int? cooldown = null,
        bool countsAgainstAllowance = true,
        IReadOnlyList<ContentEffect>? effects = null,
        IReadOnlySet<string>? parameters = null) =>
        ActionDefinition.Create(
            SpeechId, "press-conference", ActionCategory.PublicPosition,
            cost, effects: effects ?? [ContentEffect.Immediate(Inflation(), EffectOperation.Add, 1m)],
            cooldownDays: cooldown, countsAgainstAllowance: countsAgainstAllowance,
            parameterSchema: parameters).Value;

    private static Result<Domain.Actions.Services.ActionOutcome> Run(
        ActionDefinition definition,
        Domain.Actions.Services.ActionAvailabilityContext context,
        EffectScenario scenario,
        Domain.Actions.Services.ActionExecutionService service,
        IReadOnlyDictionary<string, string>? parameters = null) =>
        service.Execute(
            definition, context, scenario.Context,
            parameters ?? new Dictionary<string, string>(),
            ActionExecutionId.From(Guid.NewGuid()), new ScriptedRandom());

    [Fact]
    public void A_permitted_action_applies_its_effects()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();

        var outcome = Run(Speech(), PoliticsFixtures.Context(), scenario, service);

        outcome.IsSuccess.ShouldBeTrue();
        scenario.BaseValue().ShouldBe(6m);
        outcome.Value.Execution.Status.ShouldBe(ActionExecutionStatus.Executed);
    }

    [Fact]
    public void A_failed_action_leaves_the_world_byte_identical()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();
        var party = PoliticsFixtures.Party(budget: 100m);
        var before = Fingerprint(scenario, party);

        // Too expensive, so nothing may be paid, applied or queued.
        var outcome = Run(
            Speech(cost: ActionCost.Create(1_000_000m).Value),
            PoliticsFixtures.Context(party), scenario, service);

        outcome.IsFailure.ShouldBeTrue();
        outcome.Error.Code.ShouldBe("action.insufficient_budget");
        Fingerprint(scenario, party).ShouldBe(before);
    }

    [Fact]
    public void An_invalid_effect_rejects_the_action_without_charging_for_it()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();
        var party = PoliticsFixtures.Party(budget: 500_000m);

        var broken = Speech(
            cost: ActionCost.Create(1000m).Value,
            effects: [ContentEffect.Immediate(Inflation("no-such-field"), EffectOperation.Add, 1m)]);

        Run(broken, PoliticsFixtures.Context(party), scenario, service).IsFailure.ShouldBeTrue();

        party.Budget.ShouldBe(500_000m, "the party must not pay for an action that never happened");
    }

    [Fact]
    public void Costs_and_effects_commit_together()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();
        var party = PoliticsFixtures.Party(budget: 500_000m, cohesion: 60m);

        Run(
            Speech(cost: ActionCost.Create(50_000m, cohesion: 5m).Value),
            PoliticsFixtures.Context(party), scenario, service).IsSuccess.ShouldBeTrue();

        party.Budget.ShouldBe(450_000m);
        party.Cohesion.Value.ShouldBe(55m);
        scenario.BaseValue().ShouldBe(6m);
    }

    [Fact]
    public void A_cooldown_blocks_a_repeat_then_lapses()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();
        var ledger = ActionLedger.Empty();
        var definition = Speech(cooldown: 10, countsAgainstAllowance: false);

        Run(definition, PoliticsFixtures.Context(ledger: ledger), scenario, service).IsSuccess.ShouldBeTrue();

        var tooSoon = new Domain.Actions.Services.ActionAvailabilityContext(
            PoliticsFixtures.Party(), PoliticsFixtures.Today.AddDays(5), PoliticsFixtures.Today,
            GameStatus.Running, ledger);

        Run(definition, tooSoon, scenario, service).Error.Code.ShouldBe("action.on_cooldown");

        var later = tooSoon with { Today = PoliticsFixtures.Today.AddDays(10) };

        Run(definition, later, scenario, service).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void The_weekly_allowance_limits_voluntary_actions()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();
        var ledger = ActionLedger.Empty();
        var context = PoliticsFixtures.Context(ledger: ledger);

        for (var i = 0; i < ledger.WeeklyAllowance; i++)
        {
            Run(Speech(), context, scenario, service).IsSuccess.ShouldBeTrue();
        }

        Run(Speech(), context, scenario, service).Error.Code.ShouldBe("action.allowance_exhausted");
    }

    [Fact]
    public void The_allowance_refreshes_the_following_week()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();
        var ledger = ActionLedger.Empty();
        var context = PoliticsFixtures.Context(ledger: ledger);

        for (var i = 0; i < ledger.WeeklyAllowance; i++)
        {
            Run(Speech(), context, scenario, service);
        }

        var nextWeek = context with { Today = PoliticsFixtures.Today.AddDays(7) };

        Run(Speech(), nextWeek, scenario, service).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(GameStatus.Running, true)]
    [InlineData(GameStatus.AwaitingDecision, true)]
    [InlineData(GameStatus.Created, false)]
    [InlineData(GameStatus.Paused, false)]
    [InlineData(GameStatus.Finished, false)]
    [InlineData(GameStatus.Abandoned, false)]
    public void Actions_are_permitted_only_while_the_game_is_live(GameStatus status, bool permitted)
    {
        // Answering a scandal with a press conference is a voluntary action, so
        // AwaitingDecision permits actions even while the calendar is blocked.
        var (service, scenario) = PoliticsFixtures.ExecutionService();

        Run(Speech(), PoliticsFixtures.Context(status: status), scenario, service)
            .IsSuccess.ShouldBe(permitted);
    }

    [Fact]
    public void A_missing_required_parameter_is_rejected()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();

        var definition = Speech(parameters: new HashSet<string> { "topic" });

        Run(definition, PoliticsFixtures.Context(), scenario, service)
            .Error.Code.ShouldBe("action.missing_parameter");
    }

    [Fact]
    public void Stimuli_are_queued_rather_than_applied_immediately()
    {
        var (service, scenario) = PoliticsFixtures.ExecutionService();

        var definition = ActionDefinition.Create(
            SpeechId, "pensions-pledge", ActionCategory.PublicPosition,
            stimuli:
            [
                StimulusTemplate.Positional(
                    "$self", IssueId.From(Guid.NewGuid()), stance: -70m, intensity: 0.7m),
            ]).Value;

        var outcome = Run(definition, PoliticsFixtures.Context(), scenario, service);

        // Media and reactions run at one fixed point in the next day's loop, never at the
        // place that raised them.
        outcome.Value.QueuedStimuli.ShouldHaveSingleItem().Intensity.ShouldBe(0.7m);
    }

    [Fact]
    public void One_handler_per_category_not_one_class_per_action()
    {
        var perAction = typeof(ActionDefinition).Assembly.GetTypes()
            .Select(t => t.Name)
            .Where(n => n is "PublicPositionAction" or "ParliamentaryVoteAction" or "InternalPartyAction")
            .ToList();

        perAction.ShouldBeEmpty();
        Enum.GetValues<ActionCategory>().Length.ShouldBe(3);
    }

    private static string Fingerprint(EffectScenario scenario, Domain.Politics.Entities.PartyState party) =>
        string.Join("|", scenario.World.Variables.Select(v => $"{v.Key}={v.BaseValue}"))
        + $"||{party.Budget}:{party.Cohesion.Value}:{party.Credibility.Value}"
        + $"||{scenario.Modifiers.Count}:{scenario.Pending.Count}";
}
