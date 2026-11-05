using TheSpanishWay.Domain.Actors.Entities;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Actors.Services;

/// <summary>
/// How a particular segment rates a particular leader.
/// <para>
/// Derived per segment rather than taken as one national popularity figure. A leader who
/// plays well with retirees and badly with the young is the normal case, and a single number
/// cannot express it. Review issue #19.
/// </para>
/// </summary>
public static class LeaderEvaluation
{
    /// <summary>
    /// Combines national standing, charisma and how close the leader is to this segment,
    /// weighted by how much the segment cares about leaders at all.
    /// </summary>
    public static decimal For(
        CharacterState leader,
        IdeologicalProfile segmentPosition,
        AxisWeights segmentWeights,
        SegmentBehaviour behaviour)
    {
        ArgumentNullException.ThrowIfNull(leader);
        ArgumentNullException.ThrowIfNull(behaviour);

        var fit = IdeologicalAffinityCalculator.Between(segmentPosition, leader.Ideology, segmentWeights);

        // Charisma travels further with segments that pay attention to leaders; the
        // ideological fit is what remains when they do not.
        var sensitivity = (double)behaviour.LeaderSensitivity;

        var evaluation =
            ((double)leader.Popularity.Value * 0.4d)
            + ((double)leader.Charisma.Value * 0.3d * sensitivity)
            + ((double)fit * (0.3d + (0.3d * (1d - sensitivity))));

        return Precision.ToStored(Math.Clamp(evaluation, 0d, 100d));
    }
}
