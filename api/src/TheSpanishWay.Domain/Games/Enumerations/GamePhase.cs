namespace TheSpanishWay.Domain.Games.Enumerations;

/// <summary>
/// What part of the political cycle the game is in. Orthogonal to
/// <see cref="GameStatus"/>: a game can be paused during a campaign, so the two vary
/// independently and neither constrains the other.
/// </summary>
public enum GamePhase
{
    /// <summary>Ordinary political time between campaigns.</summary>
    Normal = 0,

    /// <summary>The fifteen days before polling, with raised volatility and media attention.</summary>
    ElectionCampaign = 1,

    /// <summary>Polling day itself.</summary>
    ElectionDay = 2,

    /// <summary>After an election, until a government is invested or a repeat election is called.</summary>
    GovernmentFormation = 3,

    /// <summary>A terminal condition has been met.</summary>
    GameOver = 4,
}
