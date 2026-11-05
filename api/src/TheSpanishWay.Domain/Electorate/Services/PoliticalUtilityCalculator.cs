using TheSpanishWay.Domain.Electorate.Enumerations;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Electorate.Services;

/// <summary>
/// Turns what a segment thinks of a party into a single number.
/// <para>
/// Every component is centred onto −1..+1 first: a 0–100 value <c>x</c> becomes
/// <c>(x − 50) / 50</c>. Without that, feeding raw 0–100 values into a softmax makes the
/// temperature meaningless — a difference of 10 between two parties means something quite
/// different at scale 100 than at scale 1. Centring is what puts temperature into an
/// interpretable 0.1–0.3 band. See design D9.
/// </para>
/// </summary>
public static class PoliticalUtilityCalculator
{
    private static readonly UtilityComponent[] AllComponents = Enum.GetValues<UtilityComponent>();

    /// <summary>
    /// The weighted sum of centred components. Ranges from −1 (every component at its worst)
    /// to +1, and sits at 0 when the segment is indifferent on everything.
    /// </summary>
    public static decimal Compute(UtilityInputs inputs, UtilityWeights weights)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(weights);

        var normalised = weights.Normalised();
        var utility = 0d;

        foreach (var component in AllComponents)
        {
            utility += (double)normalised.For(component) * Precision.Centre(inputs[component]);
        }

        return Precision.ToStored(utility);
    }
}
