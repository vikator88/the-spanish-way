using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Electorate.Services;

/// <summary>Everything the aggregation needs about one segment in one province.</summary>
public sealed record SegmentVoteInputs(
    SegmentId Segment,
    long Population,
    decimal Turnout,
    ChoiceProbabilities Probabilities);

/// <summary>
/// Turns per-segment behaviour into a constituency result.
/// <para>
/// <c>Votes(p, k) = Σ_s Population(s, k) · Turnout(s, k) · Pr(p | s, k)</c>.
/// </para>
/// <para>
/// Built upward from each province's own composition. Nothing anywhere computes a national
/// share and apportions it downward, because that would make every province a scaled copy of
/// the country and erase exactly the regional variation that decides elections.
/// </para>
/// </summary>
public static class ProvincialVoteCalculator
{
    public static ProvincialResult Compute(
        ProvinceId province,
        IReadOnlyList<SegmentVoteInputs> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var votes = new Dictionary<PartyId, decimal>();
        var blank = 0m;
        var electorate = 0L;

        // Ordered so that two runs over the same inputs accumulate in the same sequence.
        foreach (var segment in segments.OrderBy(s => s.Segment))
        {
            electorate += segment.Population;

            var voters = segment.Population * segment.Turnout;

            foreach (var (party, probability) in segment.Probabilities.ByParty.OrderBy(p => p.Key))
            {
                votes[party] = (votes.TryGetValue(party, out var running) ? running : 0m)
                    + (voters * probability);
            }

            blank += voters * segment.Probabilities.Blank;
        }

        return new ProvincialResult(
            province,
            votes.ToDictionary(p => p.Key, p => (long)Math.Round(p.Value, MidpointRounding.AwayFromZero)),
            (long)Math.Round(blank, MidpointRounding.AwayFromZero),
            electorate);
    }

    /// <summary>
    /// National vote shares, aggregated from constituency results. An output of the model,
    /// never an input to it.
    /// </summary>
    public static IReadOnlyDictionary<PartyId, decimal> NationalShares(
        IReadOnlyList<ProvincialResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var totals = new Dictionary<PartyId, long>();
        var validVotes = 0L;

        foreach (var result in results.OrderBy(r => r.Province))
        {
            validVotes += result.ValidVotes;

            foreach (var (party, votes) in result.VotesByParty.OrderBy(p => p.Key))
            {
                totals[party] = (totals.TryGetValue(party, out var running) ? running : 0L) + votes;
            }
        }

        return validVotes <= 0L
            ? new Dictionary<PartyId, decimal>()
            : totals.ToDictionary(p => p.Key, p => (decimal)p.Value / validVotes);
    }
}
