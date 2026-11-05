using Shouldly;
using TheSpanishWay.Domain.Content.Entities;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Simulation.Tests;

/// <summary>
/// A blocking event stops the simulation, and nothing advances until the player answers.
/// This is the join between the event scheduler and the session's status machine.
/// </summary>
public sealed class BlockingDecisionTests
{
    private static readonly GameDate Start = GameDate.From(2027, 3, 1);

    private static EventOccurrence FireBlockingScandal()
    {
        var world = new FakeWorld().WithCandidates(TargetType.Party, "party-a");
        var evaluator = new ConditionEvaluator(world);

        var scheduler = new EventScheduler(
            evaluator,
            new RoleBinder(world, evaluator),
            new SequentialIdentifierSource(RandomSeed.FromText("blocking")));

        var definition = GameEventDefinition.Create(
            EventDefinitionId.From(Guid.Parse("0000000e-0000-0000-0000-000000000001")),
            "scandal", EventCategory.Random, 1m, isBlocking: true).Value;

        return scheduler.Generate([definition], EventLedger.Empty(), Start, new ScriptedRandom(0m))
            .ShouldHaveSingleItem();
    }

    [Fact]
    public void A_blocking_occurrence_halts_the_game_at_the_decision_point()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        var occurrence = FireBlockingScandal();

        occurrence.IsBlocking.ShouldBeTrue();
        session.RaiseDecision(occurrence.Id).IsSuccess.ShouldBeTrue();

        session.Status.ShouldBe(GameStatus.AwaitingDecision);
        session.AdvanceUntil(new StopCondition.AfterDays(30), 30)
            .Error.Code.ShouldBe("game.decision_pending");
        session.CurrentDate.ShouldBe(Start, "not one day passes while a decision is open");
    }

    [Fact]
    public void Answering_the_decision_lets_the_simulation_continue()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        var occurrence = FireBlockingScandal();
        session.RaiseDecision(occurrence.Id);

        occurrence.Resolve("condemn", Start).IsSuccess.ShouldBeTrue();
        session.ResolveDecision(occurrence.Id).IsSuccess.ShouldBeTrue();

        session.Status.ShouldBe(GameStatus.Running);
        session.AdvanceUntil(new StopCondition.AfterDays(30), 30).IsSuccess.ShouldBeTrue();
        session.CurrentDate.ShouldBe(Start.AddDays(30));
    }

    [Fact]
    public void A_non_blocking_occurrence_does_not_halt_the_game()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        var definition = GameEventDefinition.Create(
            EventDefinitionId.From(Guid.NewGuid()), "rumour", EventCategory.Random, 1m,
            isBlocking: false, durationDays: 10, defaultOptionKey: "ignore").Value;

        definition.IsBlocking.ShouldBeFalse();

        // Nothing raises a decision, so the loop runs on.
        session.AdvanceUntil(new StopCondition.AfterDays(30), 30).IsSuccess.ShouldBeTrue();
        session.Status.ShouldBe(GameStatus.Running);
    }

    [Fact]
    public void A_game_paused_mid_decision_resumes_into_the_decision()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        var occurrence = FireBlockingScandal();
        session.RaiseDecision(occurrence.Id);
        session.Pause();

        session.Resume();

        session.Status.ShouldBe(GameStatus.AwaitingDecision);
    }

    [Fact]
    public void Several_blocking_decisions_all_have_to_be_answered()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        var first = EventOccurrenceId.From(Guid.Parse("0000000f-0000-0000-0000-000000000001"));
        var second = EventOccurrenceId.From(Guid.Parse("0000000f-0000-0000-0000-000000000002"));

        session.RaiseDecision(first);
        session.RaiseDecision(second);
        session.ResolveDecision(first);

        session.Status.ShouldBe(GameStatus.AwaitingDecision);

        session.ResolveDecision(second);

        session.Status.ShouldBe(GameStatus.Running);
    }
}
