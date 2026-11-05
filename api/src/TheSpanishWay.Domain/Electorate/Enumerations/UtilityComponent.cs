namespace TheSpanishWay.Domain.Electorate.Enumerations;

/// <summary>
/// What goes into a segment's judgement of a party. Every component is centred onto −1..+1
/// before weighting, so no one of them can dominate purely through its scale.
/// </summary>
public enum UtilityComponent
{
    /// <summary>How close the party's perceived position is. Counted exactly once.</summary>
    IdeologicalFit = 0,

    Trust = 1,
    Respect = 2,

    /// <summary>How the party is judged on its record, in government or in opposition.</summary>
    Performance = 3,

    MediaPerception = 4,
    LeaderEvaluation = 5,

    /// <summary>Local organisation. Modifies utility; never adds votes directly.</summary>
    ProvincialPresence = 6,
}
