using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;

namespace TheSpanishWay.Domain.Media.Entities;

/// <summary>
/// A newspaper, broadcaster or site, with its own interests and its own mind.
/// <para>
/// Not a conduit. Each outlet decides for itself whether something is worth covering and how
/// to frame it, so the same event can lead one bulletin and go unmentioned in another. More
/// exposure is not more support: wide hostile coverage is worse for a party than being
/// ignored.
/// </para>
/// </summary>
public sealed class MediaOutlet : Entity<MediaOutletId>
{
    private readonly Dictionary<PartyId, decimal> _partyAffinity;
    private readonly Dictionary<IssueId, decimal> _issueInterest;
    private readonly Dictionary<PartyId, decimal> _recentFramingBias = [];

    private MediaOutlet(
        MediaOutletId id,
        string name,
        EditorialOrientation orientation,
        Bounded<decimal> audience,
        Bounded<decimal> credibility,
        Bounded<decimal> sensationalism,
        Bounded<decimal> independence,
        Dictionary<PartyId, decimal> partyAffinity,
        Dictionary<IssueId, decimal> issueInterest)
        : base(id)
    {
        Name = name;
        Orientation = orientation;
        Audience = audience;
        Credibility = credibility;
        Sensationalism = sensationalism;
        Independence = independence;
        _partyAffinity = partyAffinity;
        _issueInterest = issueInterest;
    }

    public string Name { get; }

    public EditorialOrientation Orientation { get; }

    /// <summary>Reach, as a share of the population.</summary>
    public Bounded<decimal> Audience { get; }

    /// <summary>How much weight its coverage carries with those who see it.</summary>
    public Bounded<decimal> Credibility { get; }

    /// <summary>How readily it runs a story for the story's sake.</summary>
    public Bounded<decimal> Sensationalism { get; }

    public Bounded<decimal> Independence { get; }

    public static Result<MediaOutlet> Create(
        MediaOutletId id,
        string name,
        EditorialOrientation orientation,
        decimal audience = 20m,
        decimal credibility = 60m,
        decimal sensationalism = 40m,
        decimal independence = 50m,
        IReadOnlyDictionary<PartyId, decimal>? partyAffinity = null,
        IReadOnlyDictionary<IssueId, decimal>? issueInterest = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<MediaOutlet>(new Error("outlet.name_required", "An outlet needs a name."));
        }

        return Result.Success(
            new MediaOutlet(
                id, name, orientation,
                Bounded.Percentage(audience), Bounded.Percentage(credibility),
                Bounded.Percentage(sensationalism), Bounded.Percentage(independence),
                partyAffinity is null ? [] : new Dictionary<PartyId, decimal>(partyAffinity),
                issueInterest is null ? [] : new Dictionary<IssueId, decimal>(issueInterest)));
    }

    /// <summary>How well disposed the outlet is toward a party, 0–100, neutral by default.</summary>
    public decimal AffinityToward(PartyId party) =>
        _partyAffinity.TryGetValue(party, out var affinity) ? affinity : 50m;

    /// <summary>How much the outlet cares about an issue, 0–1. Indifferent by default.</summary>
    public decimal InterestIn(IssueId issue) =>
        _issueInterest.TryGetValue(issue, out var interest) ? interest : 0.3m;

    /// <summary>
    /// Which way the outlet has been leaning on a party lately.
    /// <para>
    /// Coverage is not independent day to day. An outlet that has spent a fortnight attacking
    /// a party does not wake up neutral, and a model without memory produces a jittery press
    /// that nobody would recognise.
    /// </para>
    /// </summary>
    public decimal RecentFramingBiasToward(PartyId party) =>
        _recentFramingBias.TryGetValue(party, out var bias) ? bias : 0m;

    public void RememberFraming(PartyId party, decimal toneBias)
    {
        var current = RecentFramingBiasToward(party);

        // An exponential blend: recent coverage dominates without erasing what came before.
        _recentFramingBias[party] = Math.Clamp((current * 0.7m) + (toneBias * 0.3m), -1m, 1m);
    }

    /// <summary>Called on a cadence boundary: memory fades when a story stops running.</summary>
    public void DecayMemory(decimal fraction)
    {
        foreach (var party in _recentFramingBias.Keys.OrderBy(p => p).ToList())
        {
            _recentFramingBias[party] *= 1m - fraction;
        }
    }
}
