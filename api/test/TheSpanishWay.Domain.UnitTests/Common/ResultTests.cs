using Shouldly;
using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.UnitTests.Common;

public sealed class ResultTests
{
    private static readonly Error Rejected = new("game.already_finished", "The game has finished.");

    [Fact]
    public void A_failure_carries_a_stable_machine_readable_code()
    {
        var result = Result.Failure(Rejected);

        result.IsFailure.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("game.already_finished");
    }

    [Fact]
    public void Reading_the_value_of_a_failure_throws()
    {
        var result = Result.Failure<int>(Rejected);

        // Asking for a value that was never produced is a programmer error, not a
        // runtime condition the caller should be invited to handle.
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void A_success_carries_its_value()
    {
        Result.Success(42).Value.ShouldBe(42);
    }

    [Fact]
    public void A_rule_violation_does_not_throw()
    {
        var thrown = Record.Exception(() => Result.Failure<int>(Rejected));

        thrown.ShouldBeNull();
    }

    [Fact]
    public void A_success_cannot_carry_an_error()
    {
        Should.Throw<InvalidOperationException>(() => new FakeResult(isSuccess: true, Rejected));
    }

    [Fact]
    public void A_failure_cannot_be_errorless()
    {
        Should.Throw<InvalidOperationException>(() => new FakeResult(isSuccess: false, Error.None));
    }

    [Fact]
    public void First_failure_wins_over_later_ones()
    {
        var first = new Error("a", "first");

        var result = Result.FirstFailureOr(
            Result.Success(),
            Result.Failure(first),
            Result.Failure(new Error("b", "second")));

        result.Error.ShouldBe(first);
    }

    [Fact]
    public void All_successes_succeed()
    {
        Result.FirstFailureOr(Result.Success(), Result.Success()).IsSuccess.ShouldBeTrue();
    }

    private sealed class FakeResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
