using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Electorate.Services;

/// <summary>
/// Moves current preferences part of the way toward where the model now says they should be.
/// <para>
/// Voters do not jump to a newly computed ideal distribution the instant something happens,
/// and a model that let them would make every event feel like a switch being flipped.
/// </para>
/// <para>
/// Volatility is expressed <em>per 30 days</em> and converted to the actual elapsed step:
/// <c>λ(Δt) = 1 − (1 − λ₃₀)^(Δt / 30)</c>. Applying a fixed λ on every recalculation would
/// mean that changing the cadence from weekly to daily silently changed the whole game,
/// which is review issue #12.
/// </para>
/// </summary>
public static class PreferenceInertia
{
    /// <summary>The fraction of the gap closed over <paramref name="elapsedDays"/>.</summary>
    public static decimal LambdaFor(decimal monthlyVolatility, int elapsedDays)
    {
        if (elapsedDays <= 0)
        {
            return 0m;
        }

        if (monthlyVolatility <= 0m)
        {
            return 0m;
        }

        if (monthlyVolatility >= 1m)
        {
            return 1m;
        }

        var lambda = 1d - Math.Pow(1d - (double)monthlyVolatility, elapsedDays / 30d);

        return Precision.ToStored(lambda);
    }

    /// <summary>
    /// Blends current preferences toward the target and renormalises, so the result is still
    /// a distribution rather than a set of numbers that happen to be near one.
    /// </summary>
    public static IReadOnlyDictionary<PartyId, decimal> Blend(
        IReadOnlyDictionary<PartyId, decimal> current,
        IReadOnlyDictionary<PartyId, decimal> target,
        decimal monthlyVolatility,
        int elapsedDays)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);

        var lambda = LambdaFor(monthlyVolatility, elapsedDays);

        var parties = current.Keys.Union(target.Keys).OrderBy(p => p).ToList();
        var blended = new Dictionary<PartyId, decimal>();

        foreach (var party in parties)
        {
            var from = current.TryGetValue(party, out var c) ? c : 0m;
            var to = target.TryGetValue(party, out var t) ? t : 0m;

            blended[party] = ((1m - lambda) * from) + (lambda * to);
        }

        return Renormalise(blended);
    }

    private static Dictionary<PartyId, decimal> Renormalise(Dictionary<PartyId, decimal> shares)
    {
        var total = shares.Values.Sum();

        if (total <= 0m)
        {
            return shares;
        }

        return shares.ToDictionary(p => p.Key, p => Precision.ToStored(p.Value / total));
    }
}
