namespace TheSpanishWay.Domain.Games.Enumerations;

/// <summary>
/// Where a game sits in its lifecycle, which decides what commands it will accept.
/// <para>
/// <see cref="Paused"/> and <see cref="AwaitingDecision"/> are deliberately separate. v0.1
/// folds both into one "in progress" value, but "the player chose to stop" and "the engine
/// cannot continue without an answer" permit different commands and need different handling
/// on resume. See domain/game-session.
/// </para>
/// </summary>
public enum GameStatus
{
    /// <summary>Created from a scenario but not yet started. Accepts only <c>Start</c>.</summary>
    Created = 0,

    /// <summary>Advancing normally.</summary>
    Running = 1,

    /// <summary>Halted at a decision point: a blocking event needs the player's answer.</summary>
    AwaitingDecision = 2,

    /// <summary>Halted because the player chose to stop.</summary>
    Paused = 3,

    /// <summary>A terminal condition was met. Nothing further is accepted.</summary>
    Finished = 4,

    /// <summary>Given up by the player. Nothing further is accepted.</summary>
    Abandoned = 5,
}
