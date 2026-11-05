using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Electorate.Services;

/// <summary>
/// Converts utilities into the probability of choosing each option.
/// <para>
/// A numerically stable softmax: the maximum utility is subtracted before exponentiating,
/// so a large utility cannot overflow <c>exp</c> and a small one cannot underflow to zero.
/// The subtraction cancels out mathematically and changes nothing about the result.
/// </para>
/// </summary>
public static class ChoiceProbabilityCalculator
{
    /// <summary>
    /// <c>Pr(p) = exp((U(p) − Umax) / T) / Σ exp((U(j) − Umax) / T)</c> over the options
    /// standing here. A party that does not stand in this constituency is simply absent from
    /// the denominator and receives nothing.
    /// </summary>
    /// <param name="options">The parties standing here, plus blank.</param>
    /// <param name="temperature">
    /// How decisively the segment chooses. Low temperature concentrates the vote on the best
    /// option; high temperature spreads it. With centred utilities, 0.1–0.3 is the useful band.
    /// </param>
    public static Result<ChoiceProbabilities> Compute(
        IReadOnlyList<ChoiceOption> options,
        decimal temperature)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
        {
            return Result.Failure<ChoiceProbabilities>(
                new Error("choice.no_options", "A segment needs something to choose between."));
        }

        if (temperature <= 0m)
        {
            return Result.Failure<ChoiceProbabilities>(
                new Error(
                    "choice.invalid_temperature",
                    "Temperature must be positive; at zero the softmax becomes a step function."));
        }

        var t = (double)temperature;
        var maximum = options.Max(o => (double)o.Utility);

        // Subtracting the maximum keeps every exponent at or below zero, so exp() stays in
        // a range doubles represent precisely.
        var weights = options.Select(o => Math.Exp(((double)o.Utility - maximum) / t)).ToList();
        var total = weights.Sum();

        if (total <= 0d || double.IsNaN(total))
        {
            return Result.Failure<ChoiceProbabilities>(
                new Error("choice.degenerate", "The softmax produced no usable distribution."));
        }

        var byParty = new Dictionary<PartyId, decimal>();
        var blank = 0m;

        for (var index = 0; index < options.Count; index++)
        {
            var probability = Precision.ToStored(weights[index] / total);

            if (options[index].Party is { } party)
            {
                byParty[party] = byParty.TryGetValue(party, out var existing)
                    ? existing + probability
                    : probability;
            }
            else
            {
                blank += probability;
            }
        }

        return Result.Success(Normalise(new ChoiceProbabilities(byParty, blank)));
    }

    /// <summary>
    /// Corrects the rounding drift from returning to stored precision, so probabilities sum
    /// to exactly 1 rather than to 0.999999.
    /// </summary>
    private static ChoiceProbabilities Normalise(ChoiceProbabilities probabilities)
    {
        var total = probabilities.Total;

        if (total == 1m || total <= 0m)
        {
            return probabilities;
        }

        var scaled = probabilities.ByParty.ToDictionary(p => p.Key, p => p.Value / total);
        var blank = probabilities.Blank / total;

        // Absorb whatever is left into the largest share, which is the least distorted
        // place to put a rounding remainder.
        var remainder = 1m - (scaled.Values.Sum() + blank);

        if (remainder != 0m && scaled.Count > 0)
        {
            var largest = scaled.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
            scaled[largest] += remainder;
        }
        else
        {
            blank += remainder;
        }

        return new ChoiceProbabilities(scaled, blank);
    }
}
