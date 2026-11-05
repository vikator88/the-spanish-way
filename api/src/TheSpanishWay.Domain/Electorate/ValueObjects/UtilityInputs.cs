using TheSpanishWay.Domain.Electorate.Enumerations;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// The raw components of one segment's judgement of one party in one province, each on the
/// familiar 0–100 scale. The calculator centres them before weighting.
/// </summary>
public sealed record UtilityInputs(
    decimal IdeologicalFit,
    decimal Trust,
    decimal Respect,
    decimal Performance,
    decimal MediaPerception,
    decimal LeaderEvaluation,
    decimal ProvincialPresence)
{
    /// <summary>Every component indifferent, which should produce zero utility.</summary>
    public static UtilityInputs Neutral { get; } = new(50m, 50m, 50m, 50m, 50m, 50m, 50m);

    public decimal this[UtilityComponent component] => component switch
    {
        UtilityComponent.IdeologicalFit => IdeologicalFit,
        UtilityComponent.Trust => Trust,
        UtilityComponent.Respect => Respect,
        UtilityComponent.Performance => Performance,
        UtilityComponent.MediaPerception => MediaPerception,
        UtilityComponent.LeaderEvaluation => LeaderEvaluation,
        UtilityComponent.ProvincialPresence => ProvincialPresence,
        _ => 50m,
    };
}
