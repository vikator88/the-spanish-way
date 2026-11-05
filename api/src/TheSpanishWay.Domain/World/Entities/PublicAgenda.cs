using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.World.Entities;

/// <summary>One topic's standing in public debate.</summary>
public sealed record AgendaTopic(IssueId Issue, decimal Relevance, decimal Trend, decimal MediaAttention)
{
    public static AgendaTopic Dormant(IssueId issue) => new(issue, 0m, 0m, 0m);
}

/// <summary>
/// What the country is currently arguing about.
/// <para>
/// Dominance is emergent, not enforced. v0.1 says "2–4 topics dominate", which reads like an
/// invariant but is really an observation about how a normalised, decaying distribution
/// behaves. Capping the count would mean deciding in code what ought to follow from the
/// simulation. Review issue #20.
/// </para>
/// <para>
/// This is what gates political attention: a proposal on an issue nobody is thinking about
/// moves almost nothing, however good it is.
/// </para>
/// </summary>
public sealed class PublicAgenda
{
    private readonly Dictionary<IssueId, AgendaTopic> _topics;

    private PublicAgenda(Dictionary<IssueId, AgendaTopic> topics) => _topics = topics;

    public static PublicAgenda Empty() => new([]);

    public static PublicAgenda From(IEnumerable<AgendaTopic> topics)
    {
        ArgumentNullException.ThrowIfNull(topics);

        return new PublicAgenda(topics.ToDictionary(t => t.Issue));
    }

    public IReadOnlyList<AgendaTopic> Topics => [.. _topics.Values.OrderBy(t => t.Issue)];

    /// <summary>Topics by relevance, which is what "dominating the agenda" means.</summary>
    public IReadOnlyList<AgendaTopic> Dominant(int count) =>
        [.. _topics.Values.OrderByDescending(t => t.Relevance).ThenBy(t => t.Issue).Take(count)];

    public decimal RelevanceOf(IssueId issue) =>
        _topics.TryGetValue(issue, out var topic) ? topic.Relevance : 0m;

    /// <summary>
    /// Pushes an issue up the agenda. Everything else is diluted by renormalisation, which is
    /// how one story crowding out another happens without anybody coding it.
    /// </summary>
    public void Raise(IssueId issue, decimal amount)
    {
        var current = _topics.TryGetValue(issue, out var topic) ? topic : AgendaTopic.Dormant(issue);

        _topics[issue] = current with
        {
            Relevance = Math.Max(0m, current.Relevance + amount),
            Trend = amount,
            MediaAttention = Math.Clamp(current.MediaAttention + amount, 0m, 1m),
        };

        Normalise();
    }

    /// <summary>
    /// Fades every topic toward nothing, then renormalises. A story nobody is still telling
    /// stops mattering.
    /// </summary>
    public void Decay(decimal fraction)
    {
        foreach (var issue in _topics.Keys.OrderBy(i => i).ToList())
        {
            var topic = _topics[issue];

            _topics[issue] = topic with
            {
                Relevance = topic.Relevance * (1m - fraction),
                MediaAttention = topic.MediaAttention * (1m - fraction),
                Trend = -topic.Relevance * fraction,
            };
        }

        Normalise();
    }

    /// <summary>Relevance always sums to 1, so attention is genuinely finite.</summary>
    private void Normalise()
    {
        var total = _topics.Values.Sum(t => t.Relevance);

        if (total <= 0m)
        {
            return;
        }

        foreach (var issue in _topics.Keys.OrderBy(i => i).ToList())
        {
            var topic = _topics[issue];

            _topics[issue] = topic with { Relevance = Precision.ToStored(topic.Relevance / total) };
        }
    }
}
