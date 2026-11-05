using Shouldly;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.Events;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Games;

/// <summary>
/// Every permitted transition, and the refusal of everything else. The status machine is
/// what stops a finished game accepting commands, so it is worth covering exhaustively.
/// </summary>
public sealed class GameSessionStatusTests
{
    private static readonly EventOccurrenceId Scandal =
        EventOccurrenceId.From(Guid.Parse("0000000c-0000-0000-0000-000000000001"));

    [Fact]
    public void A_new_game_is_created_not_running()
    {
        var session = new GameSessionBuilder().Build();

        session.Status.ShouldBe(GameStatus.Created);
        session.Revision.Value.ShouldBe(0);
        session.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<GameCreated>();
    }

    [Fact]
    public void Starting_moves_a_created_game_to_running()
    {
        var session = new GameSessionBuilder().Build();
        session.ClearDomainEvents();

        session.Start().IsSuccess.ShouldBeTrue();

        session.Status.ShouldBe(GameStatus.Running);
        session.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<GameStarted>();
    }

    [Fact]
    public void Starting_twice_is_refused()
    {
        var session = new GameSessionBuilder().BuildRunning();

        var result = session.Start();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("game.invalid_status_transition");
    }

    [Fact]
    public void A_blocking_event_moves_a_running_game_to_awaiting_decision()
    {
        var session = new GameSessionBuilder().BuildRunning();

        session.RaiseDecision(Scandal).IsSuccess.ShouldBeTrue();

        session.Status.ShouldBe(GameStatus.AwaitingDecision);
        session.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DecisionRequired>();
    }

    [Fact]
    public void Resolving_the_last_blocking_decision_returns_to_running()
    {
        var session = new GameSessionBuilder().BuildRunning();
        session.RaiseDecision(Scandal);

        session.ResolveDecision(Scandal).IsSuccess.ShouldBeTrue();

        session.Status.ShouldBe(GameStatus.Running);
    }

    [Fact]
    public void Resolving_one_of_several_decisions_stays_awaiting()
    {
        var other = EventOccurrenceId.From(Guid.Parse("0000000c-0000-0000-0000-000000000002"));
        var session = new GameSessionBuilder().BuildRunning();
        session.RaiseDecision(Scandal);
        session.RaiseDecision(other);

        session.ResolveDecision(Scandal);

        session.Status.ShouldBe(GameStatus.AwaitingDecision);
    }

    [Fact]
    public void Resolving_a_decision_that_is_not_open_is_refused()
    {
        var session = new GameSessionBuilder().BuildRunning();
        session.RaiseDecision(Scandal);

        var result = session.ResolveDecision(
            EventOccurrenceId.From(Guid.Parse("0000000c-0000-0000-0000-00000000000f")));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("game.decision_not_open");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pausing_is_allowed_from_either_active_status(bool withOpenDecision)
    {
        var session = new GameSessionBuilder().BuildRunning();

        if (withOpenDecision)
        {
            session.RaiseDecision(Scandal);
        }

        session.Pause().IsSuccess.ShouldBeTrue();
        session.Status.ShouldBe(GameStatus.Paused);
    }

    [Fact]
    public void Resuming_with_nothing_pending_returns_to_running()
    {
        var session = new GameSessionBuilder().BuildRunning();
        session.Pause();

        session.Resume().IsSuccess.ShouldBeTrue();

        session.Status.ShouldBe(GameStatus.Running);
    }

    [Fact]
    public void Resuming_with_a_decision_still_open_returns_to_awaiting_decision()
    {
        // A game paused mid-decision must not quietly resume as though it had been answered.
        var session = new GameSessionBuilder().BuildRunning();
        session.RaiseDecision(Scandal);
        session.Pause();

        session.Resume();

        session.Status.ShouldBe(GameStatus.AwaitingDecision);
        session.DomainEvents.OfType<GameResumed>().ShouldHaveSingleItem()
            .ResumedInto.ShouldBe(GameStatus.AwaitingDecision);
    }

    [Fact]
    public void Pause_and_awaiting_decision_are_different_states()
    {
        var paused = new GameSessionBuilder().BuildRunning();
        paused.Pause();

        var awaiting = new GameSessionBuilder().BuildRunning();
        awaiting.RaiseDecision(Scandal);

        paused.Status.ShouldNotBe(awaiting.Status);
    }

    [Fact]
    public void Abandoning_is_allowed_from_running_awaiting_and_paused()
    {
        foreach (var arrange in new Action<TheSpanishWay.Domain.Games.Aggregates.GameSession>[]
                 {
                     _ => { },
                     s => s.RaiseDecision(Scandal),
                     s => s.Pause(),
                 })
        {
            var session = new GameSessionBuilder().BuildRunning();
            arrange(session);

            session.Abandon().IsSuccess.ShouldBeTrue();
            session.Status.ShouldBe(GameStatus.Abandoned);
        }
    }

    [Fact]
    public void A_game_that_never_started_cannot_be_abandoned()
    {
        var session = new GameSessionBuilder().Build();

        session.Abandon().IsFailure.ShouldBeTrue();
        session.Status.ShouldBe(GameStatus.Created);
    }

    [Fact]
    public void Ending_finishes_the_game_and_sets_the_game_over_phase()
    {
        var session = new GameSessionBuilder().BuildRunning();

        session.End("player party dissolved").IsSuccess.ShouldBeTrue();

        session.Status.ShouldBe(GameStatus.Finished);
        session.Phase.ShouldBe(GamePhase.GameOver);
        session.DomainEvents.OfType<GameEnded>().ShouldHaveSingleItem()
            .Reason.ShouldBe("player party dissolved");
    }

    [Theory]
    [InlineData(GameStatus.Finished)]
    [InlineData(GameStatus.Abandoned)]
    public void A_terminal_game_accepts_nothing_further(GameStatus terminal)
    {
        var session = new GameSessionBuilder().BuildRunning();

        if (terminal == GameStatus.Finished)
        {
            session.End("over");
        }
        else
        {
            session.Abandon();
        }

        session.Start().IsFailure.ShouldBeTrue();
        session.Pause().IsFailure.ShouldBeTrue();
        session.Resume().IsFailure.ShouldBeTrue();
        session.Abandon().IsFailure.ShouldBeTrue();
        session.End("again").IsFailure.ShouldBeTrue();
        session.RaiseDecision(Scandal).IsFailure.ShouldBeTrue();
        session.Status.ShouldBe(terminal);
    }

    [Fact]
    public void A_created_game_cannot_be_paused_or_resumed()
    {
        var session = new GameSessionBuilder().Build();

        session.Pause().IsFailure.ShouldBeTrue();
        session.Resume().IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Phase_and_status_vary_independently()
    {
        var session = new GameSessionBuilder().BuildRunning();
        session.ChangePhase(GamePhase.ElectionCampaign);

        session.Pause();

        session.Status.ShouldBe(GameStatus.Paused);
        session.Phase.ShouldBe(GamePhase.ElectionCampaign);
    }

    [Fact]
    public void Changing_phase_records_the_transition()
    {
        var session = new GameSessionBuilder().BuildRunning();

        session.ChangePhase(GamePhase.ElectionCampaign);

        var changed = session.DomainEvents.OfType<PhaseChanged>().ShouldHaveSingleItem();
        changed.From.ShouldBe(GamePhase.Normal);
        changed.To.ShouldBe(GamePhase.ElectionCampaign);
    }

    [Fact]
    public void Changing_to_the_current_phase_records_nothing()
    {
        var session = new GameSessionBuilder().BuildRunning();

        session.ChangePhase(GamePhase.Normal).IsSuccess.ShouldBeTrue();

        session.DomainEvents.OfType<PhaseChanged>().ShouldBeEmpty();
    }
}
