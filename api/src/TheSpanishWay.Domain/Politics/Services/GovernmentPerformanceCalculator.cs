using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;

namespace TheSpanishWay.Domain.Politics.Services;

/// <summary>How much of one indicator's movement a government is held responsible for.</summary>
public sealed record AttributionShare(string VariableKey, decimal PolicyShare)
{
    /// <summary>
    /// A change the world imposed rather than the government chose. Blamed at a third of the
    /// rate, so an incumbent is not held fully responsible for an oil shock — but is not let
    /// off entirely either, because voters rarely are that generous.
    /// </summary>
    public decimal ExternalShare => PolicyShare / 3m;
}

/// <summary>
/// How well a government is judged to be doing.
/// <para>
/// Reads the <see cref="ChangeOrigin"/> tag that the effect engine stamps on every change,
/// which is what makes attribution possible at all: without provenance there is no way to
/// tell a policy outcome from a commodity price.
/// </para>
/// </summary>
public static class GovernmentPerformanceCalculator
{
    /// <summary>Per-indicator attribution, as placeholder configuration.</summary>
    public static IReadOnlyList<AttributionShare> DefaultShares { get; } =
    [
        new("inflation", 0.5m),
        new("employment", 0.6m),
        new("economicgrowth", 0.5m),
        new("publicdebt", 0.4m),
        new("publicdeficit", 0.5m),
    ];

    /// <summary>
    /// Scores performance on 0–100 from the changes recorded since a government took office.
    /// A government that presided over improvement it caused scores highest; one that
    /// presided over decline it caused scores lowest.
    /// </summary>
    public static decimal Score(
        IReadOnlyList<(string VariableKey, decimal Change, decimal Polarity, ChangeOrigin Origin)> changes,
        IReadOnlyList<AttributionShare>? shares = null)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var attribution = (shares ?? DefaultShares).ToDictionary(s => s.VariableKey, s => s);
        var score = 0d;

        foreach (var change in changes.OrderBy(c => c.VariableKey, StringComparer.Ordinal))
        {
            if (!attribution.TryGetValue(change.VariableKey, out var share))
            {
                continue;
            }

            var weight = change.Origin == ChangeOrigin.External ? share.ExternalShare : share.PolicyShare;

            // Polarity turns "it went up" into "that was good" or "that was bad".
            score += (double)(change.Change * change.Polarity * weight);
        }

        // Centre on 50 so an uneventful term is judged neither well nor badly.
        return Precision.ToStored(Math.Clamp(50d + score, 0d, 100d));
    }
}
