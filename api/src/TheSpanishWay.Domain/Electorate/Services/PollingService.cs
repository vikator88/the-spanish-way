using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Electorate.Services;

/// <summary>
/// Turns hidden preferences into a published poll.
/// <para>
/// Three sources of difference from the truth: sampling error from the sample size, the
/// pollster's standing house effect, and the fact that the poll measures intention rather
/// than behaviour. All reproducible from the seed, so a replay reads the same headlines.
/// </para>
/// </summary>
public static class PollingService
{
    /// <summary>
    /// Publishes a poll from turnout-weighted hidden preferences.
    /// </summary>
    /// <param name="turnoutWeightedShares">
    /// Preferences already weighted by how likely each segment is to vote — polls of likely
    /// voters, not of the whole population.
    /// </param>
    public static Poll Publish(
        Pollster pollster,
        IReadOnlyDictionary<PartyId, decimal> turnoutWeightedShares,
        int sampleSize,
        GameDate on,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(pollster);
        ArgumentNullException.ThrowIfNull(turnoutWeightedShares);
        ArgumentNullException.ThrowIfNull(random);

        var estimates = new Dictionary<PartyId, decimal>();

        foreach (var (party, share) in turnoutWeightedShares.OrderBy(p => p.Key))
        {
            // Standard error of a proportion: sqrt(p(1−p)/n). Small parties are measured
            // proportionally less precisely, which is why minor-party polling is so noisy.
            var standardError = Math.Sqrt((double)share * (1d - (double)share) / Math.Max(sampleSize, 1));

            // A draw centred on zero, scaled so most samples fall within one standard error.
            var noise = ((double)random.NextUnitInterval(RandomStream.Polls) - 0.5d) * 2d * standardError;

            var estimate = (double)share + noise + ((double)pollster.HouseEffectFor(party) / 100d);

            estimates[party] = Precision.ToStored(Math.Clamp(estimate, 0d, 1d));
        }

        var total = estimates.Values.Sum();

        if (total > 0m)
        {
            foreach (var party in estimates.Keys.ToList())
            {
                estimates[party] = Precision.ToStored(estimates[party] / total);
            }
        }

        return new Poll(
            pollster.Id, on, sampleSize,
            MarginOfError: Precision.ToStored(1.96d / Math.Sqrt(Math.Max(sampleSize, 1))),
            estimates);
    }
}
