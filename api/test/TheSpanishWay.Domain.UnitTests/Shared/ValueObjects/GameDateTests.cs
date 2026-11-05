using Shouldly;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Shared.ValueObjects;

public sealed class GameDateTests
{
    [Fact]
    public void Adding_a_day_crosses_a_month_boundary()
    {
        GameDate.From(2027, 1, 31).AddDays(1).ShouldBe(GameDate.From(2027, 2, 1));
    }

    [Fact]
    public void Adding_a_day_crosses_a_year_boundary()
    {
        GameDate.From(2027, 12, 31).AddDays(1).ShouldBe(GameDate.From(2028, 1, 1));
    }

    [Fact]
    public void Leap_years_are_handled()
    {
        GameDate.From(2028, 2, 28).AddDays(1).ShouldBe(GameDate.From(2028, 2, 29));
        GameDate.Create(2027, 2, 29).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Earlier_dates_compare_as_less()
    {
        var earlier = GameDate.From(2027, 3, 1);
        var later = GameDate.From(2027, 3, 2);

        (earlier < later).ShouldBeTrue();
        (later > earlier).ShouldBeTrue();
        earlier.CompareTo(later).ShouldBeLessThan(0);
    }

    [Fact]
    public void Days_since_counts_forwards_and_backwards()
    {
        var start = GameDate.From(2027, 1, 1);

        start.AddDays(30).DaysSince(start).ShouldBe(30);
        start.DaysSince(start.AddDays(30)).ShouldBe(-30);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    public void Weekly_boundaries_count_from_the_start_date(int elapsedDays, bool expected)
    {
        // Every seventh day from the game's start, not ISO Monday: the cadence must not
        // depend on which weekday a scenario happens to open on.
        var start = GameDate.From(2027, 3, 3);

        start.AddDays(elapsedDays).IsWeeklyBoundary(start).ShouldBe(expected);
    }

    [Fact]
    public void A_scenario_starting_on_a_different_weekday_keeps_the_same_cadence()
    {
        var monday = GameDate.From(2027, 3, 1);
        var thursday = GameDate.From(2027, 3, 4);

        monday.AddDays(7).IsWeeklyBoundary(monday).ShouldBeTrue();
        thursday.AddDays(7).IsWeeklyBoundary(thursday).ShouldBeTrue();
    }

    [Fact]
    public void Monthly_boundaries_are_the_first_of_a_month()
    {
        GameDate.From(2027, 5, 1).IsMonthlyBoundary().ShouldBeTrue();
        GameDate.From(2027, 5, 2).IsMonthlyBoundary().ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(7, true)]
    [InlineData(10, true)]
    [InlineData(2, false)]
    [InlineData(12, false)]
    public void Quarterly_boundaries_open_each_quarter(int month, bool expected)
    {
        GameDate.From(2027, month, 1).IsQuarterlyBoundary().ShouldBe(expected);
    }

    [Fact]
    public void Invalid_dates_are_rejected_rather_than_coerced()
    {
        GameDate.Create(2027, 13, 1).Error.Code.ShouldBe("game_date.invalid_month");
        GameDate.Create(2027, 4, 31).Error.Code.ShouldBe("game_date.invalid_day");
        GameDate.Create(0, 1, 1).Error.Code.ShouldBe("game_date.year_out_of_range");
    }
}
