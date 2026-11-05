using Shouldly;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Shared.ValueObjects;

public sealed class BoundedTests
{
    [Fact]
    public void Adding_past_the_ceiling_stops_at_the_ceiling()
    {
        var value = Bounded.From(98m, 0m, 100m);

        value.Add(7m).Result.Value.ShouldBe(100m);
    }

    [Fact]
    public void Subtracting_past_the_floor_stops_at_the_floor()
    {
        var value = Bounded.From(3m, 0m, 100m);

        value.Subtract(7m).Result.Value.ShouldBe(0m);
    }

    [Fact]
    public void Truncation_is_reported_not_discarded()
    {
        var value = Bounded.From(98m, 0m, 100m);

        var change = value.Add(7m);

        change.WasTruncated.ShouldBeTrue();
        change.Truncated.ShouldBe(5m, "7 was attempted but only 2 fitted");
    }

    [Fact]
    public void A_change_that_fits_reports_no_truncation()
    {
        var change = Bounded.From(50m, 0m, 100m).Add(7m);

        change.WasTruncated.ShouldBeFalse();
        change.Truncated.ShouldBe(0m);
        change.Result.Value.ShouldBe(57m);
    }

    [Fact]
    public void Bounds_survive_a_change()
    {
        var change = Bounded.From(50m, 10m, 90m).Add(100m);

        change.Result.Minimum.ShouldBe(10m);
        change.Result.Maximum.ShouldBe(90m);
        change.Result.Value.ShouldBe(90m);
    }

    [Fact]
    public void An_inverted_range_is_rejected()
    {
        var result = Bounded.Create(5m, minimum: 100m, maximum: 0m);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("bounded.invalid_range");
    }

    [Fact]
    public void Construction_clamps_an_out_of_range_starting_value()
    {
        Bounded.From(150m, 0m, 100m).Value.ShouldBe(100m);
        Bounded.From(-150m, 0m, 100m).Value.ShouldBe(0m);
    }

    [Fact]
    public void Named_ranges_match_the_model_vocabulary()
    {
        Bounded.Percentage(50m).Maximum.ShouldBe(100m);
        Bounded.AxisPosition(0m).Minimum.ShouldBe(-100m);
        Bounded.UnitInterval(0.5m).Maximum.ShouldBe(1m);
    }

    [Fact]
    public void Edges_are_reported()
    {
        Bounded.From(100m, 0m, 100m).IsAtMaximum.ShouldBeTrue();
        Bounded.From(0m, 0m, 100m).IsAtMinimum.ShouldBeTrue();
    }
}
