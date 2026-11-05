using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;

namespace TheSpanishWay.Domain.Media.ValueObjects;

/// <summary>
/// One outlet's coverage of one thing: a structured fact, not an article.
/// <para>
/// The Domain produces facts; turning them into a headline is somebody else's job. Natural
/// language generation inside the simulation is explicitly deferred, and nothing here
/// depends on it.
/// </para>
/// </summary>
public sealed record MediaCoverage(
    MediaOutletId Outlet,
    PartyId? Subject,
    IssueId Issue,
    CoverageLevel Level,
    MediaTone Tone,
    decimal Reach,
    GameDate PublishedOn)
{
    public bool WasCovered => Level != CoverageLevel.NotCovered;

    /// <summary>
    /// Tone as a signed multiplier, −1 hostile to +1 supportive. What reaches a segment is
    /// reach × credibility × this × the segment's media sensitivity.
    /// </summary>
    public decimal ToneFactor => Tone switch
    {
        MediaTone.Hostile => -1m,
        MediaTone.Critical => -0.5m,
        MediaTone.Neutral => 0m,
        MediaTone.Favourable => 0.5m,
        MediaTone.Supportive => 1m,
        _ => 0m,
    };

    /// <summary>How loudly it ran, 0–1.</summary>
    public decimal Prominence => Level switch
    {
        CoverageLevel.NotCovered => 0m,
        CoverageLevel.Passing => 0.25m,
        CoverageLevel.Standard => 0.5m,
        CoverageLevel.Prominent => 0.75m,
        CoverageLevel.Saturation => 1m,
        _ => 0m,
    };
}
