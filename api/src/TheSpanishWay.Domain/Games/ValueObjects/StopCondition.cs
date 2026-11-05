using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Games.ValueObjects;

/// <summary>
/// How far <c>AdvanceUntil</c> should run. The player never advances one day at a time: a
/// legislature is 1,461 days, and one command per day would mean 1,461 clicks and 1,461
/// snapshot writes. See design D7.
/// </summary>
public abstract record StopCondition
{
    private StopCondition()
    {
    }

    /// <summary>Run a fixed number of days unless something interrupts.</summary>
    public sealed record AfterDays(int Days) : StopCondition;

    /// <summary>Run until a given date unless something interrupts.</summary>
    public sealed record OnDate(GameDate Date) : StopCondition;

    /// <summary>Run until the player is actually needed.</summary>
    public sealed record NextDecision : StopCondition;

    /// <summary>Run until the political cycle moves on.</summary>
    public sealed record PhaseChange : StopCondition;
}
