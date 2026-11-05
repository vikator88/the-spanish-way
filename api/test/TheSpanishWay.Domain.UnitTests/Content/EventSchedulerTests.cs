using Shouldly;
using TheSpanishWay.Domain.Content.Entities;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Content;

public sealed class EventSchedulerTests
{
    private static readonly GameDate Today = GameDate.From(2027, 3, 1);

    private static readonly EventDefinitionId ScandalId =
        EventDefinitionId.From(Guid.Parse("0000000e-0000-0000-0000-000000000001"));

    private static readonly EventDefinitionId FollowUpId =
        EventDefinitionId.From(Guid.Parse("0000000e-0000-0000-0000-000000000002"));

    private static readonly AttributeRef Inflation =
        AttributeRef.From(TargetType.WorldVariable, "inflation", "current");

    private static (EventScheduler Scheduler, FakeWorld World) Build(decimal inflation = 5m)
    {
        var world = new FakeWorld()
            .WithValue(TargetType.WorldVariable, "inflation", "current", inflation)
            .WithCandidates(TargetType.Party, "party-a");

        var evaluator = new ConditionEvaluator(world);

        return (
            new EventScheduler(
                evaluator,
                new RoleBinder(world, evaluator),
                new SequentialIdentifierSource(RandomSeed.FromText("events"))),
            world);
    }

    private static GameEventDefinition Definition(
        EventDefinitionId? id = null,
        string key = "scandal",
        decimal probability = 1m,
        EventCategory category = EventCategory.Random,
        ConditionGroup? triggers = null,
        int? cooldown = null,
        IReadOnlyList<EventDefinitionId>? followUps = null,
        bool blocking = false) =>
        GameEventDefinition.Create(
            id ?? ScandalId, key, category, probability, triggers,
            cooldownDays: cooldown, followUps: followUps, isBlocking: blocking).Value;

    [Fact]
    public void A_certain_event_fires()
    {
        var (scheduler, _) = Build();

        var fired = scheduler.Generate(
            [Definition()], EventLedger.Empty(), Today, new ScriptedRandom(0m));

        fired.ShouldHaveSingleItem().DefinitionId.ShouldBe(ScandalId);
    }

    [Fact]
    public void An_impossible_event_does_not_fire()
    {
        var (scheduler, _) = Build();

        scheduler.Generate(
            [Definition(probability: 0m)], EventLedger.Empty(), Today, new ScriptedRandom(0.5m))
            .ShouldBeEmpty();
    }

    [Fact]
    public void A_scripted_draw_decides_whether_a_chance_event_fires()
    {
        var (scheduler, _) = Build();
        var definition = Definition(probability: 0.3m);

        scheduler.Generate([definition], EventLedger.Empty(), Today,
            new ScriptedRandom().Script(RandomStream.Events, 0.2m)).Count.ShouldBe(1);

        scheduler.Generate([definition], EventLedger.Empty(), Today,
            new ScriptedRandom().Script(RandomStream.Events, 0.4m)).ShouldBeEmpty();
    }

    [Fact]
    public void Unmet_trigger_conditions_block_an_event()
    {
        var (scheduler, _) = Build(inflation: 2m);

        var definition = Definition(
            triggers: ConditionGroup.All(
                Condition.Compare(Inflation, ComparisonOperator.GreaterThan, 10m).Value));

        var verdict = scheduler.Assess(definition, EventLedger.Empty(), Today, new ScriptedRandom(0m));

        verdict.IsEligible.ShouldBeFalse();
        verdict.Reason.ShouldBe(IneligibilityReason.TriggerConditionsUnmet);
    }

    [Fact]
    public void A_cooldown_blocks_a_re_fire_then_lapses()
    {
        var (scheduler, _) = Build();
        var ledger = EventLedger.Empty();
        var definition = Definition(cooldown: 30);

        scheduler.Generate([definition], ledger, Today, new ScriptedRandom(0m)).Count.ShouldBe(1);

        scheduler.Assess(definition, ledger, Today.AddDays(10), new ScriptedRandom(0m))
            .Reason.ShouldBe(IneligibilityReason.OnCooldown);

        scheduler.Assess(definition, ledger, Today.AddDays(30), new ScriptedRandom(0m))
            .IsEligible.ShouldBeTrue();
    }

    [Fact]
    public void A_definition_can_occur_more_than_once_over_a_game()
    {
        var (scheduler, _) = Build();
        var ledger = EventLedger.Empty();
        var definition = Definition(cooldown: 10);

        var first = scheduler.Generate([definition], ledger, Today, new ScriptedRandom(0m));
        var second = scheduler.Generate([definition], ledger, Today.AddDays(11), new ScriptedRandom(0m));

        first.ShouldHaveSingleItem().Id.ShouldNotBe(second.ShouldHaveSingleItem().Id,
            "each firing is its own occurrence");
    }

    [Fact]
    public void A_chained_event_is_unreachable_until_unlocked()
    {
        var (scheduler, _) = Build();
        var ledger = EventLedger.Empty();
        var chained = Definition(id: FollowUpId, key: "aftermath", category: EventCategory.Chained);

        scheduler.Assess(chained, ledger, Today, new ScriptedRandom(0m))
            .Reason.ShouldBe(IneligibilityReason.FollowUpNotUnlocked);

        ledger.UnlockFollowUp(FollowUpId);

        scheduler.Assess(chained, ledger, Today, new ScriptedRandom(0m)).IsEligible.ShouldBeTrue();
    }

    [Fact]
    public void Firing_an_event_unlocks_its_follow_ups_without_firing_them()
    {
        var (scheduler, _) = Build();
        var ledger = EventLedger.Empty();

        var fired = scheduler.Generate(
            [Definition(followUps: [FollowUpId])], ledger, Today, new ScriptedRandom(0m));

        fired.Count.ShouldBe(1, "the follow-up becomes possible, not inevitable");
        ledger.IsFollowUpUnlocked(FollowUpId).ShouldBeTrue();
    }

    [Fact]
    public void A_definition_whose_role_has_no_candidate_is_ineligible()
    {
        var world = new FakeWorld().WithCandidates(TargetType.Party);
        var evaluator = new ConditionEvaluator(world);
        var scheduler = new EventScheduler(
            evaluator, new RoleBinder(world, evaluator),
            new SequentialIdentifierSource(RandomSeed.FromText("x")));

        var definition = GameEventDefinition.Create(
            ScandalId, "scandal", EventCategory.Random, 1m,
            roles: [RoleDeclaration.Create(RoleName.From("$accused"), TargetType.Party).Value]).Value;

        scheduler.Assess(definition, EventLedger.Empty(), Today, new ScriptedRandom(0m))
            .Reason.ShouldBe(IneligibilityReason.NoCandidateForRole);
    }

    [Fact]
    public void Definitions_are_considered_in_a_stable_order()
    {
        var (scheduler, _) = Build();

        var definitions = new[]
        {
            Definition(id: FollowUpId, key: "zebra"),
            Definition(id: ScandalId, key: "alpha"),
        };

        var fired = scheduler.Generate(definitions, EventLedger.Empty(), Today, new ScriptedRandom(0m));

        fired.Select(o => o.DefinitionId).ShouldBe([ScandalId, FollowUpId]);
    }

    [Fact]
    public void An_invalid_probability_is_rejected_at_definition()
    {
        GameEventDefinition.Create(ScandalId, "x", EventCategory.Random, 1.5m)
            .Error.Code.ShouldBe("event_definition.invalid_probability");
    }
}
