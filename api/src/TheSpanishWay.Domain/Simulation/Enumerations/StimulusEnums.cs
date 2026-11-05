namespace TheSpanishWay.Domain.Simulation.Enumerations;

/// <summary>
/// Whether a stimulus is about a position or about competence.
/// <para>
/// Ideology decides a reaction to a stance on pensions and has nothing to say about a
/// corruption scandal — nobody is in favour of corruption. Mixing the two is what makes a
/// naive model treat a scandal as if voters disagreed about whether stealing is good.
/// </para>
/// </summary>
public enum StimulusKind
{
    /// <summary>Takes a stance. Reactions follow ideological distance.</summary>
    Positional = 0,

    /// <summary>Carries no stance, only credit or blame.</summary>
    Valence = 1,
}

/// <summary>
/// How far something travelled. Media pick-up depends on it: a backroom row is not a press
/// conference, and most internal business never reaches the public at all.
/// </summary>
public enum Visibility
{
    Internal = 0,
    Parliamentary = 1,
    Public = 2,
}

/// <summary>Which way a media outlet framed something.</summary>
public enum MediaTone
{
    Hostile = 0,
    Critical = 1,
    Neutral = 2,
    Favourable = 3,
    Supportive = 4,
}

/// <summary>How prominently an outlet ran a story.</summary>
public enum CoverageLevel
{
    NotCovered = 0,
    Passing = 1,
    Standard = 2,
    Prominent = 3,
    Saturation = 4,
}

/// <summary>Where an outlet sits editorially.</summary>
public enum EditorialOrientation
{
    Left = 0,
    CentreLeft = 1,
    Centre = 2,
    CentreRight = 3,
    Right = 4,
}
