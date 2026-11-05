using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// How much each issue matters to a segment — the second of the two weight vectors.
/// <para>
/// Separate from the axis weights because salience and ideology are different things. A
/// segment can weigh the economy axis heavily and still barely register a housing story.
/// An issue not listed here simply does not move this segment.
/// </para>
/// </summary>
public sealed class IssueWeights : ValueObject
{
    private readonly Dictionary<IssueId, decimal> _weights;

    private IssueWeights(Dictionary<IssueId, decimal> weights) => _weights = weights;

    public static IssueWeights None { get; } = new([]);

    public decimal For(IssueId issue) => _weights.TryGetValue(issue, out var weight) ? weight : 0m;

    public decimal Total => _weights.Values.Sum();

    public static Result<IssueWeights> Create(IReadOnlyDictionary<IssueId, decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (weights.Values.Any(w => w < 0m))
        {
            return Result.Failure<IssueWeights>(
                new Error("issue_weights.negative", "An issue weight is negative."));
        }

        return Result.Success(new IssueWeights(new Dictionary<IssueId, decimal>(weights)));
    }

    /// <summary>Weights scaled to sum to 1. An empty set stays empty rather than dividing by zero.</summary>
    public IssueWeights Normalised()
    {
        var total = Total;

        return total <= 0m
            ? this
            : new IssueWeights(_weights.ToDictionary(p => p.Key, p => p.Value / total));
    }

    protected override IEnumerable<object?> GetEqualityComponents() =>
        _weights.OrderBy(p => p.Key).Select(p => (object?)$"{p.Key}:{p.Value}");
}
