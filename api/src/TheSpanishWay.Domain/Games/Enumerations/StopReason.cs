namespace TheSpanishWay.Domain.Games.Enumerations;

/// <summary>Why <c>AdvanceUntil</c> stopped, so the caller can tell a decision point from a quiet run.</summary>
public enum StopReason
{
    /// <summary>The requested number of days elapsed without interruption.</summary>
    RequestedDaysElapsed = 0,

    /// <summary>The requested date was reached.</summary>
    RequestedDateReached = 1,

    /// <summary>A blocking event needs the player's answer.</summary>
    DecisionRequired = 2,

    /// <summary>The phase changed, for example a campaign opened or an election was held.</summary>
    PhaseChanged = 3,

    /// <summary>A terminal condition was met and the game is over.</summary>
    GameEnded = 4,

    /// <summary>The safety limit on days per command was reached.</summary>
    MaximumDaysReached = 5,
}
