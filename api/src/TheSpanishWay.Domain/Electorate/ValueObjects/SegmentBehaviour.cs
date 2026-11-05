using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// How a segment behaves, as distinct from what it believes.
/// <para>
/// Every one of these is a configuration placeholder. The point is that two segments with
/// identical ideology can still behave completely differently — one loyal and hard to move,
/// the other volatile and easily swayed by a leader.
/// </para>
/// </summary>
public sealed record SegmentBehaviour(
    decimal BaseTurnout,
    decimal ElectoralLoyalty,
    decimal MonthlyVolatility,
    decimal LeaderSensitivity,
    decimal MediaSensitivity,
    decimal AbstentionPropensity,
    decimal ChoiceTemperature)
{
    /// <summary>
    /// A middling segment. Temperature sits in the interpretable 0.1–0.3 band that centred
    /// utility components make possible.
    /// </summary>
    public static SegmentBehaviour Default { get; } =
        new(0.70m, 0.60m, 0.25m, 0.40m, 0.40m, 0.20m, 0.20m);

    public static Result<SegmentBehaviour> Create(
        decimal baseTurnout,
        decimal electoralLoyalty,
        decimal monthlyVolatility,
        decimal leaderSensitivity,
        decimal mediaSensitivity,
        decimal abstentionPropensity,
        decimal choiceTemperature)
    {
        foreach (var (label, value) in new[]
                 {
                     ("base turnout", baseTurnout),
                     ("electoral loyalty", electoralLoyalty),
                     ("monthly volatility", monthlyVolatility),
                     ("leader sensitivity", leaderSensitivity),
                     ("media sensitivity", mediaSensitivity),
                     ("abstention propensity", abstentionPropensity),
                 })
        {
            if (value is < 0m or > 1m)
            {
                return Result.Failure<SegmentBehaviour>(
                    new Error("segment_behaviour.out_of_range", $"The {label} of {value} is not a fraction."));
            }
        }

        if (choiceTemperature <= 0m)
        {
            return Result.Failure<SegmentBehaviour>(
                new Error(
                    "segment_behaviour.invalid_temperature",
                    "Temperature must be positive; at zero the softmax becomes a step function."));
        }

        return Result.Success(
            new SegmentBehaviour(
                baseTurnout, electoralLoyalty, monthlyVolatility, leaderSensitivity,
                mediaSensitivity, abstentionPropensity, choiceTemperature));
    }
}
