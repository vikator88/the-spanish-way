using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Enumerations;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// How much each ideological axis matters to a segment.
/// <para>
/// One of the <em>two</em> weight vectors a segment carries. This one decides ideological
/// distance; the issue weights decide which topics it notices. v0.1 conflates them, which
/// leaves no way to say that retirees weigh the economy heavily but care about pensions
/// specifically — see design D9.
/// </para>
/// </summary>
public sealed class AxisWeights : ValueObject
{
    private static readonly IdeologicalAxis[] AllAxes = Enum.GetValues<IdeologicalAxis>();

    private readonly decimal[] _weights;

    private AxisWeights(decimal[] weights) => _weights = weights;

    /// <summary>Every axis weighted the same.</summary>
    public static AxisWeights Uniform { get; } =
        new([.. AllAxes.Select(_ => 1m / AllAxes.Length)]);

    public decimal For(IdeologicalAxis axis) => _weights[(int)axis];

    public decimal Total => _weights.Sum();

    public static Result<AxisWeights> Create(IReadOnlyDictionary<IdeologicalAxis, decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var values = new decimal[AllAxes.Length];

        foreach (var axis in AllAxes)
        {
            var weight = weights.TryGetValue(axis, out var value) ? value : 0m;

            if (weight < 0m)
            {
                return Result.Failure<AxisWeights>(
                    new Error("axis_weights.negative", $"Weight for {axis} is negative."));
            }

            values[(int)axis] = weight;
        }

        if (values.Sum() <= 0m)
        {
            return Result.Failure<AxisWeights>(
                new Error(
                    "axis_weights.unnormalisable",
                    "Axis weights sum to zero, so nothing can be weighted against anything."));
        }

        return Result.Success(new AxisWeights(values));
    }

    /// <summary>Weights scaled to sum to 1, so segments are comparable with one another.</summary>
    public AxisWeights Normalised()
    {
        var total = Total;

        return new AxisWeights([.. _weights.Select(w => w / total)]);
    }

    protected override IEnumerable<object?> GetEqualityComponents() => _weights.Cast<object?>();
}
