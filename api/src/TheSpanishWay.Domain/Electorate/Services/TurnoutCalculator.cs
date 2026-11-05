using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Electorate.Services;

/// <summary>What is pushing a segment toward or away from the ballot box.</summary>
public sealed record TurnoutInfluences(
    decimal Enthusiasm,
    decimal Disengagement,
    decimal CampaignEffect)
{
    public static TurnoutInfluences None { get; } = new(0m, 0m, 0m);
}

/// <summary>
/// Whether a segment votes at all, decided entirely separately from whom it votes for.
/// <para>
/// This separation is the fix for v0.1 counting abstention twice. Turnout answers
/// <em>whether</em>; the softmax answers <em>whom</em>. Neither knows about the other.
/// </para>
/// </summary>
public static class TurnoutCalculator
{
    /// <summary>
    /// A logistic curve around the segment's baseline, so influences push turnout without
    /// ever pushing it outside 0–1 and with diminishing returns at the extremes — which a
    /// clamped sum would not give.
    /// </summary>
    public static decimal Compute(SegmentBehaviour behaviour, TurnoutInfluences influences)
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        ArgumentNullException.ThrowIfNull(influences);

        var baseline = (double)behaviour.BaseTurnout;

        // A baseline at either extreme has no logit, so short-circuit rather than
        // producing an infinity.
        if (baseline <= 0d)
        {
            return 0m;
        }

        if (baseline >= 1d)
        {
            return 1m;
        }

        var shift =
            (double)influences.Enthusiasm
            - (double)influences.Disengagement
            - (double)behaviour.AbstentionPropensity
            + (double)influences.CampaignEffect;

        var logit = Math.Log(baseline / (1d - baseline)) + shift;

        return Precision.ToStored(1d / (1d + Math.Exp(-logit)));
    }
}
