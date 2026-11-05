using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.Shared.ValueObjects;

/// <summary>
/// A date on the game's calendar. All in-game time is expressed with this type; the
/// Domain never reads a server clock, so a saved game replays identically whenever it
/// is run. See domain/building-blocks, "Game calendar".
/// </summary>
public readonly record struct GameDate : IComparable<GameDate>
{
    private readonly DateOnly _value;

    private GameDate(DateOnly value) => _value = value;

    public int Year => _value.Year;

    public int Month => _value.Month;

    public int Day => _value.Day;

    /// <summary>A stable integer ordinal, used for date arithmetic and as a random-stream coordinate.</summary>
    public int DayNumber => _value.DayNumber;

    public static Result<GameDate> Create(int year, int month, int day)
    {
        if (year is < 1 or > 9999)
        {
            return Result.Failure<GameDate>(
                new Error("game_date.year_out_of_range", $"Year {year} is outside the supported range."));
        }

        if (month is < 1 or > 12)
        {
            return Result.Failure<GameDate>(
                new Error("game_date.invalid_month", $"Month {month} is not a calendar month."));
        }

        if (day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return Result.Failure<GameDate>(
                new Error(
                    "game_date.invalid_day",
                    $"Day {day} does not exist in {year:D4}-{month:D2}."));
        }

        return Result.Success(new GameDate(new DateOnly(year, month, day)));
    }

    /// <summary>For scenario and test construction where the date is known to be valid.</summary>
    public static GameDate From(int year, int month, int day)
    {
        var result = Create(year, month, day);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Message);
    }

    public GameDate AddDays(int days) => new(_value.AddDays(days));

    public GameDate AddMonths(int months) => new(_value.AddMonths(months));

    public GameDate AddYears(int years) => new(_value.AddYears(years));

    /// <summary>Days from <paramref name="origin"/> to this date; negative if this is earlier.</summary>
    public int DaysSince(GameDate origin) => DayNumber - origin.DayNumber;

    /// <summary>
    /// A weekly boundary is every seventh day counted from the game's start date, rather
    /// than an ISO Monday, so the cadence does not depend on which weekday a scenario opens on.
    /// The start date itself is day zero and so is a boundary; the loop computes flags only
    /// after advancing, so that day is never evaluated.
    /// </summary>
    public bool IsWeeklyBoundary(GameDate startDate) => DaysSince(startDate) % 7 == 0;

    public bool IsMonthlyBoundary() => Day == 1;

    public bool IsQuarterlyBoundary() => Day == 1 && Month is 1 or 4 or 7 or 10;

    public int CompareTo(GameDate other) => _value.CompareTo(other._value);

    public static bool operator <(GameDate left, GameDate right) => left.CompareTo(right) < 0;

    public static bool operator <=(GameDate left, GameDate right) => left.CompareTo(right) <= 0;

    public static bool operator >(GameDate left, GameDate right) => left.CompareTo(right) > 0;

    public static bool operator >=(GameDate left, GameDate right) => left.CompareTo(right) >= 0;

    public override string ToString() => _value.ToString("yyyy-MM-dd");
}
