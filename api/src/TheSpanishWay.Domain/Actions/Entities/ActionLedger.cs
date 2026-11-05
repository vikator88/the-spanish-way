using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Actions.Entities;

/// <summary>
/// What each party has done lately: cooldowns and the weekly allowance.
/// <para>
/// The MVP action economy. Without it a player can take unlimited voluntary actions every
/// day, which is not a strategy game so much as a clicking contest. Review issue #16.
/// </para>
/// </summary>
public sealed class ActionLedger
{
    private readonly Dictionary<(PartyId Party, ActionDefinitionId Action), GameDate> _lastExecuted = [];
    private readonly Dictionary<PartyId, (GameDate WeekStart, int Used)> _allowance = [];

    /// <summary>Voluntary actions a party may take per week. A configuration placeholder.</summary>
    public int WeeklyAllowance { get; init; } = 3;

    public static ActionLedger Empty() => new();

    public GameDate? LastExecuted(PartyId party, ActionDefinitionId action) =>
        _lastExecuted.TryGetValue((party, action), out var date) ? date : null;

    public bool IsOffCooldown(PartyId party, ActionDefinitionId action, int? cooldownDays, GameDate today)
    {
        if (cooldownDays is not { } cooldown || LastExecuted(party, action) is not { } last)
        {
            return true;
        }

        return today.DaysSince(last) >= cooldown;
    }

    /// <summary>How many voluntary actions remain this week, counted from the game's start date.</summary>
    public int RemainingAllowance(PartyId party, GameDate today, GameDate gameStart)
    {
        var weekStart = StartOfWeek(today, gameStart);

        if (!_allowance.TryGetValue(party, out var current) || current.WeekStart != weekStart)
        {
            return WeeklyAllowance;
        }

        return Math.Max(0, WeeklyAllowance - current.Used);
    }

    public void RecordExecution(
        PartyId party,
        ActionDefinitionId action,
        GameDate today,
        GameDate gameStart,
        bool countsAgainstAllowance)
    {
        _lastExecuted[(party, action)] = today;

        if (!countsAgainstAllowance)
        {
            return;
        }

        var weekStart = StartOfWeek(today, gameStart);

        _allowance[party] =
            _allowance.TryGetValue(party, out var current) && current.WeekStart == weekStart
                ? (weekStart, current.Used + 1)
                : (weekStart, 1);
    }

    /// <summary>
    /// Weeks run from the game's start date rather than from a calendar Monday, matching the
    /// weekly cadence the rest of the simulation uses.
    /// </summary>
    private static GameDate StartOfWeek(GameDate today, GameDate gameStart) =>
        gameStart.AddDays(today.DaysSince(gameStart) / 7 * 7);
}
