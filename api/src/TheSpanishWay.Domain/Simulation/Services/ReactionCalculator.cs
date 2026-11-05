using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Electorate.Entities;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Media.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;

namespace TheSpanishWay.Domain.Simulation.Services;

/// <summary>How one segment's view of one party moved, and why.</summary>
public sealed record PerceptionDelta(
    SegmentId Segment,
    PartyId Party,
    decimal TrustChange,
    decimal RespectChange,
    decimal DriftChange);

/// <summary>
/// Turns stimuli into changes of opinion.
/// <para>
/// The only thing in the entire model that writes a perception. Content cannot reach these
/// numbers at any price — it emits a stimulus and the pipeline decides who minds. That is
/// what makes six segments react by six different amounts to one authored line, and what
/// stops an author from quietly deciding an election.
/// </para>
/// </summary>
public static class ReactionCalculator
{
    /// <summary>
    /// How one segment reacts to one covered stimulus.
    /// <para>
    /// A positional stimulus is judged on how far its stance sits from the segment's own
    /// position on the issue's axes. A valence one carries no stance — there is no
    /// pro-corruption position — so it is judged on blame alone.
    /// </para>
    /// </summary>
    public static PerceptionDelta React(
        PopulationSegment segment,
        Stimulus stimulus,
        IssueDefinition issue,
        MediaCoverage coverage,
        decimal agendaRelevance,
        decimal outletCredibility,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(stimulus);
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(random);

        var party = stimulus.Actor ?? PartyId.Empty;

        // How much of this reaches the segment at all. An uncovered stimulus barely
        // registers publicly, however important it was.
        var exposure =
            coverage.Reach
            * (outletCredibility / 100m)
            * segment.Behaviour.MediaSensitivity;

        // How much the segment cares about this topic right now: its own weighting, times
        // how prominent the issue is in public debate.
        var salience = segment.IssueWeights.For(issue.Id) * (0.3m + (0.7m * agendaRelevance));

        if (salience <= 0m)
        {
            salience = 0.1m * (0.3m + (0.7m * agendaRelevance));
        }

        var magnitude = stimulus.Intensity * salience * Math.Max(exposure, 0.05m);

        var direction = issue.Kind == IssueKind.Positional
            ? PositionalDirection(segment, stimulus, issue)
            : ValenceDirection(coverage);

        var noise = (random.NextUnitInterval(RandomStream.Reactions) - 0.5m) * 0.2m;

        var trustChange = Precision.ToStored((double)(magnitude * (direction + noise) * 20m));

        // Respect moves with the sheer weight of coverage: a party can be respected for
        // commanding attention even when the news is bad for it.
        var respectChange = Precision.ToStored(
            (double)(magnitude * coverage.Prominence * direction * 10m));

        // Loud coverage makes a party look more like its caricature.
        var driftChange = Precision.ToStored(
            (double)(magnitude * coverage.Prominence * 0.05m));

        return new PerceptionDelta(segment.Id, party, trustChange, respectChange, driftChange);
    }

    /// <summary>
    /// Agreement or disagreement with a stance, from −1 to +1. A segment that holds the
    /// opposite position on an issue it cares about reacts as strongly against as a
    /// sympathiser reacts for.
    /// </summary>
    private static decimal PositionalDirection(
        PopulationSegment segment,
        Stimulus stimulus,
        IssueDefinition issue)
    {
        var stance = stimulus.Stance ?? 0m;
        var loadings = issue.NormalisedLoadings();

        if (loadings.Count == 0)
        {
            return 0m;
        }

        var segmentPosition = loadings.Sum(l => segment.Ideology.On(l.Axis) * l.Weight);

        // Distance of 0 means perfect agreement and scores +1; 200 apart scores −1.
        var distance = Math.Abs(segmentPosition - stance);

        return 1m - (distance / 100m);
    }

    /// <summary>
    /// A valence stimulus has no sides. What decides the reaction is how the story was
    /// framed, which is where blame gets attached.
    /// </summary>
    private static decimal ValenceDirection(MediaCoverage coverage) =>
        coverage.ToneFactor == 0m ? -0.3m : coverage.ToneFactor;

    /// <summary>
    /// Applies a delta to a perception, clamped. Separate from computing it so the pipeline
    /// can show the player why an opinion moved.
    /// </summary>
    public static SegmentPartyPerception Apply(SegmentPartyPerception current, PerceptionDelta delta)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(delta);

        return new SegmentPartyPerception(
            Math.Clamp(current.Trust + delta.TrustChange, 0m, 100m),
            Math.Clamp(current.Respect + delta.RespectChange, 0m, 100m),
            Math.Clamp(current.PositionDrift + delta.DriftChange, -1m, 1m));
    }
}
