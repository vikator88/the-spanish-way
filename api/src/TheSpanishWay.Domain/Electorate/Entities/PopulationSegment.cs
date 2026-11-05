using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Electorate.Entities;

/// <summary>
/// A statistical slice of the electorate: young people, retirees, the working class.
/// <para>
/// An aggregate, never an agent. No individual citizen is simulated — a segment represents
/// an average and a spread, and its parameters are configuration rather than anything
/// inferred from a left-or-right label. "All right-wing voters ignore corruption" is exactly
/// the kind of rule this model refuses to let anyone write.
/// </para>
/// </summary>
public sealed class PopulationSegment : Entity<SegmentId>
{
    private PopulationSegment(
        SegmentId id,
        string name,
        IdeologicalProfile ideology,
        AxisWeights axisWeights,
        IssueWeights issueWeights,
        SegmentBehaviour behaviour)
        : base(id)
    {
        Name = name;
        Ideology = ideology;
        AxisWeights = axisWeights;
        IssueWeights = issueWeights;
        Behaviour = behaviour;
    }

    public string Name { get; }

    /// <summary>The segment's national position. A province may shift it.</summary>
    public IdeologicalProfile Ideology { get; }

    /// <summary>How much each axis counts when measuring distance to a party.</summary>
    public AxisWeights AxisWeights { get; }

    /// <summary>How much each issue counts when something happens.</summary>
    public IssueWeights IssueWeights { get; }

    public SegmentBehaviour Behaviour { get; }

    public static Result<PopulationSegment> Create(
        SegmentId id,
        string name,
        IdeologicalProfile ideology,
        AxisWeights axisWeights,
        IssueWeights issueWeights,
        SegmentBehaviour behaviour)
    {
        ArgumentNullException.ThrowIfNull(ideology);
        ArgumentNullException.ThrowIfNull(axisWeights);
        ArgumentNullException.ThrowIfNull(issueWeights);
        ArgumentNullException.ThrowIfNull(behaviour);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<PopulationSegment>(
                new Error("segment.name_required", "A population segment needs a name."));
        }

        return Result.Success(
            new PopulationSegment(id, name, ideology, axisWeights.Normalised(), issueWeights, behaviour));
    }

    /// <summary>
    /// This segment's ideology as it is in a given province. Without per-province offsets,
    /// provinces differ only by composition, and no amount of composition produces a Basque
    /// or Catalan regionalist vote — which is what decides governments under this system.
    /// </summary>
    public IdeologicalProfile IdeologyIn(ProvincialIdeologyOffset? offset) =>
        offset is null ? Ideology : Ideology.ShiftedBy(offset.Offsets);
}
