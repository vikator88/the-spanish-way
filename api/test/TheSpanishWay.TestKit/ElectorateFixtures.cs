using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Electorate.Entities;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.TestKit;

/// <summary>The six MVP segments, with deliberately different ideologies and behaviours.</summary>
public static class ElectorateFixtures
{
    public static IssueId Pensions { get; } = IssueId.From(Guid.Parse("0000000f-0000-0000-0000-00000000000a"));

    public static IssueId Corruption { get; } = IssueId.From(Guid.Parse("0000000f-0000-0000-0000-00000000000b"));

    /// <summary>Pensions: a stance on the economy axis, so ideology decides the reaction.</summary>
    public static IssueDefinition PensionsIssue { get; } =
        IssueDefinition.CreatePositional(
            Pensions, "pensions", [new AxisLoading(IdeologicalAxis.Economy, 1m)]).Value;

    /// <summary>Corruption: nobody is for it, so only blame decides the reaction.</summary>
    public static IssueDefinition CorruptionIssue { get; } =
        IssueDefinition.CreateValence(
            Corruption, "corruption",
            [Domain.World.ValueObjects.VariableKey.From("institutionaltrust")]).Value;

    public static PopulationSegment Segment(
        string name,
        decimal economy,
        decimal baseTurnout = 0.7m,
        decimal mediaSensitivity = 0.4m,
        decimal issueWeight = 0.5m,
        IssueId? issue = null)
    {
        var weights = IssueWeights.Create(
            new Dictionary<IssueId, decimal> { [issue ?? Pensions] = issueWeight }).Value;

        return PopulationSegment.Create(
            SegmentId.From(Guid.Parse($"0000000b-0000-0000-0000-{Math.Abs(name.GetHashCode(StringComparison.Ordinal)) % 1000000:D12}")),
            name,
            IdeologicalProfile.From(economy, 0m, 0m, 0m, 0m, 0m),
            AxisWeights.Uniform,
            weights,
            SegmentBehaviour.Default with
            {
                BaseTurnout = baseTurnout,
                MediaSensitivity = mediaSensitivity,
            }).Value;
    }

    /// <summary>The six MVP segments, each positioned differently on the economy.</summary>
    public static IReadOnlyList<PopulationSegment> SixSegments() =>
    [
        Segment("Young people", -40m, 0.55m, 0.60m, 0.30m),
        Segment("Retired people", -10m, 0.85m, 0.50m, 0.90m),
        Segment("Working class", -60m, 0.65m, 0.35m, 0.70m),
        Segment("Self-employed", 55m, 0.75m, 0.30m, 0.40m),
        Segment("Public sector", -45m, 0.80m, 0.45m, 0.60m),
        Segment("Rural population", 20m, 0.70m, 0.25m, 0.50m),
    ];
}
