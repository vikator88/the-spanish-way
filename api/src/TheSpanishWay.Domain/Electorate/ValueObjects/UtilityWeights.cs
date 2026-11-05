using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Electorate.Enumerations;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// How much each component counts for a segment. Weights sum to 1, which together with
/// centred components is what makes temperature interpretable.
/// </summary>
public sealed class UtilityWeights : ValueObject
{
    private static readonly UtilityComponent[] AllComponents = Enum.GetValues<UtilityComponent>();

    private readonly decimal[] _weights;

    private UtilityWeights(decimal[] weights) => _weights = weights;

    /// <summary>
    /// A starting point weighted toward ideology and trust. Placeholder values: the balance
    /// is a calibration decision for seed sweeps, not an argument to be won here.
    /// </summary>
    public static UtilityWeights Default { get; } = Build(new Dictionary<UtilityComponent, decimal>
    {
        [UtilityComponent.IdeologicalFit] = 0.35m,
        [UtilityComponent.Trust] = 0.20m,
        [UtilityComponent.Respect] = 0.10m,
        [UtilityComponent.Performance] = 0.15m,
        [UtilityComponent.MediaPerception] = 0.05m,
        [UtilityComponent.LeaderEvaluation] = 0.10m,
        [UtilityComponent.ProvincialPresence] = 0.05m,
    });

    public decimal For(UtilityComponent component) => _weights[(int)component];

    public decimal Total => _weights.Sum();

    public static Result<UtilityWeights> Create(IReadOnlyDictionary<UtilityComponent, decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (weights.Values.Any(w => w < 0m))
        {
            return Result.Failure<UtilityWeights>(
                new Error("utility_weights.negative", "A utility weight is negative."));
        }

        if (weights.Values.Sum() <= 0m)
        {
            return Result.Failure<UtilityWeights>(
                new Error(
                    "utility_weights.unnormalisable",
                    "Utility weights sum to zero and cannot be normalised."));
        }

        return Result.Success(Build(weights).Normalised());
    }

    /// <summary>Scaled to sum to 1. Utility is always computed from normalised weights.</summary>
    public UtilityWeights Normalised()
    {
        var total = Total;

        return new UtilityWeights([.. _weights.Select(w => w / total)]);
    }

    private static UtilityWeights Build(IReadOnlyDictionary<UtilityComponent, decimal> weights)
    {
        var values = new decimal[AllComponents.Length];

        foreach (var component in AllComponents)
        {
            values[(int)component] = weights.TryGetValue(component, out var w) ? w : 0m;
        }

        return new UtilityWeights(values);
    }

    protected override IEnumerable<object?> GetEqualityComponents() => _weights.Cast<object?>();
}
