using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Electorate.Services;

/// <summary>
/// How close a segment feels a party is to it, on a 0–100 scale.
/// <para>
/// Pure: the same inputs always give the same answer, with no state and no randomness.
/// </para>
/// <para>
/// Crucially it measures distance to the party's <em>perceived</em> position, not its
/// declared one, and the result is read exactly once by the utility function. Storing an
/// affinity figure as well would feed ideology into the calculation twice. See design D9.
/// </para>
/// </summary>
public static class IdeologicalAffinityCalculator
{
    private static readonly IdeologicalAxis[] AllAxes = Enum.GetValues<IdeologicalAxis>();

    /// <summary>
    /// Weighted mean absolute distance across the six axes, mapped onto 0–100.
    /// <para>
    /// <c>D = Σ w(axis) · |segment − party| / Σ w</c>, then <c>affinity = 100 − D / 2</c>.
    /// Each axis spans 200, so the largest possible distance is 200 and the halving puts the
    /// result back on a 0–100 scale.
    /// </para>
    /// </summary>
    public static decimal Between(
        IdeologicalProfile segmentPosition,
        IdeologicalProfile partyPosition,
        AxisWeights weights)
    {
        ArgumentNullException.ThrowIfNull(segmentPosition);
        ArgumentNullException.ThrowIfNull(partyPosition);
        ArgumentNullException.ThrowIfNull(weights);

        var weightedDistance = 0m;
        var totalWeight = 0m;

        foreach (var axis in AllAxes)
        {
            var weight = weights.For(axis);

            weightedDistance += weight * Math.Abs(segmentPosition.On(axis) - partyPosition.On(axis));
            totalWeight += weight;
        }

        if (totalWeight <= 0m)
        {
            // Create() rejects this, so reaching here means a caller built weights another
            // way; an indifferent segment is the only sensible reading.
            return 50m;
        }

        return Precision.ToStored(100m - ((weightedDistance / totalWeight) / 2m));
    }

    /// <summary>
    /// Affinity as it actually enters the model: the segment's position in this province,
    /// against the party as this segment perceives it.
    /// </summary>
    public static decimal Perceived(
        IdeologicalProfile segmentPositionInProvince,
        IdeologicalProfile partyDeclaredPosition,
        AxisWeights weights,
        SegmentPartyPerception perception)
    {
        ArgumentNullException.ThrowIfNull(partyDeclaredPosition);
        ArgumentNullException.ThrowIfNull(perception);

        return Between(
            segmentPositionInProvince,
            partyDeclaredPosition.AsPerceived(perception.PositionDrift),
            weights);
    }
}
