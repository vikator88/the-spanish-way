using Shouldly;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Simulation.Tests;

/// <summary>
/// The loop's shape over an empty world: how far it runs, why it stops, and the fact that a
/// whole legislature costs one revision rather than 1,461.
/// </summary>
public sealed class AdvanceUntilTests
{
    /// <summary>Four years, which is one Spanish legislature and the span the loop must make cheap.</summary>
    private const int Legislature = 1461;

    private static readonly GameDate Start = GameDate.From(2027, 3, 1);

    [Fact]
    public void A_whole_legislature_advances_in_one_command_and_one_revision()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        var outcome = session.AdvanceUntil(new StopCondition.AfterDays(Legislature), Legislature);

        outcome.IsSuccess.ShouldBeTrue();
        outcome.Value.DaysAdvanced.ShouldBe(Legislature);
        outcome.Value.FinalDate.ShouldBe(Start.AddDays(Legislature));
        outcome.Value.Reason.ShouldBe(StopReason.RequestedDaysElapsed);

        // The point of AdvanceUntil: one write per player interaction, not one per day.
        session.Revision.Value.ShouldBe(1);
    }

    [Fact]
    public void Four_years_of_calendar_arithmetic_lands_on_the_right_date()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        session.AdvanceUntil(new StopCondition.AfterDays(Legislature), Legislature);

        // 2028 is a leap year, so this is 1461 days rather than 1460.
        session.CurrentDate.ShouldBe(GameDate.From(2031, 3, 1));
    }

    [Fact]
    public void Each_command_increments_the_revision_exactly_once()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        session.AdvanceUntil(new StopCondition.AfterDays(10), 10);
        session.AdvanceUntil(new StopCondition.AfterDays(10), 10);
        session.AdvanceUntil(new StopCondition.AfterDays(10), 10);

        session.Revision.Value.ShouldBe(3);
        session.CurrentDate.ShouldBe(Start.AddDays(30));
    }

    [Fact]
    public void Advancing_to_a_date_stops_on_that_date()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        var target = Start.AddDays(45);

        var outcome = session.AdvanceUntil(new StopCondition.OnDate(target), 1000);

        outcome.Value.Reason.ShouldBe(StopReason.RequestedDateReached);
        session.CurrentDate.ShouldBe(target);
    }

    [Fact]
    public void The_maximum_days_limit_bounds_a_run()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        var outcome = session.AdvanceUntil(new StopCondition.NextDecision(), maximumDays: 30);

        outcome.Value.Reason.ShouldBe(StopReason.MaximumDaysReached);
        outcome.Value.DaysAdvanced.ShouldBe(30);
    }

    [Fact]
    public void The_date_only_ever_moves_forward()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        session.AdvanceUntil(new StopCondition.AfterDays(100), 100);

        var result = session.State.MoveTo(Start);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("game_state.date_moved_backwards");
        session.CurrentDate.ShouldBe(Start.AddDays(100));
    }

    [Fact]
    public void A_finished_game_cannot_be_advanced()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        session.End("over");

        var result = session.AdvanceUntil(new StopCondition.AfterDays(1), 1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("game.not_advanceable");
    }

    [Fact]
    public void A_paused_game_cannot_be_advanced()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        session.Pause();

        session.AdvanceUntil(new StopCondition.AfterDays(1), 1).Error.Code.ShouldBe("game.not_advanceable");
    }

    [Fact]
    public void The_loop_will_not_run_past_an_unanswered_decision()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();
        session.RaiseDecision(EventOccurrenceId.From(Guid.Parse("0000000c-0000-0000-0000-000000000001")));

        var result = session.AdvanceUntil(new StopCondition.AfterDays(10), 10);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("game.decision_pending");
        session.CurrentDate.ShouldBe(Start, "not a single day may pass while a decision is open");
    }

    [Fact]
    public void A_zero_day_window_is_rejected()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        session.AdvanceUntil(new StopCondition.AfterDays(1), 0)
            .Error.Code.ShouldBe("game.invalid_advance_window");
    }

    [Fact]
    public void Cadence_flags_follow_the_calendar()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        session.AdvanceUntil(new StopCondition.AfterDays(7), 7);
        session.State.IsWeeklyBoundary().ShouldBeTrue();

        // 1 April 2027 is both a month and a quarter boundary.
        var quarterly = new GameSessionBuilder().StartingOn(GameDate.From(2027, 3, 31)).BuildRunning();
        quarterly.AdvanceUntil(new StopCondition.AfterDays(1), 1);

        quarterly.State.IsMonthlyBoundary().ShouldBeTrue();
        quarterly.State.IsQuarterlyBoundary().ShouldBeTrue();
    }

    [Fact]
    public void Random_stream_indices_restart_each_day()
    {
        var session = new GameSessionBuilder().StartingOn(Start).BuildRunning();

        session.AdvanceUntil(new StopCondition.AfterDays(3), 3);

        session.State.RandomStreams.IndexOf(RandomStream.Events).ShouldBe(0);
    }
}
