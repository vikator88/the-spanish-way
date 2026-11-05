using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// How many people of each segment live in a province.
/// <para>
/// This, plus per-province ideology offsets, is what makes one constituency behave
/// differently from another. National vote share is an <em>output</em> of aggregating these;
/// it is never apportioned back down to them.
/// </para>
/// </summary>
public sealed record ProvincialComposition(
    ProvinceId Province,
    IReadOnlyDictionary<SegmentId, long> PopulationBySegment)
{
    public long TotalPopulation => PopulationBySegment.Values.Sum();

    public long PopulationOf(SegmentId segment) =>
        PopulationBySegment.TryGetValue(segment, out var population) ? population : 0L;

    public static Result<ProvincialComposition> Create(
        ProvinceId province,
        IReadOnlyDictionary<SegmentId, long> populationBySegment)
    {
        ArgumentNullException.ThrowIfNull(populationBySegment);

        if (populationBySegment.Values.Any(p => p < 0L))
        {
            return Result.Failure<ProvincialComposition>(
                new Error("composition.negative_population", "A segment population is negative."));
        }

        if (populationBySegment.Values.Sum() <= 0L)
        {
            return Result.Failure<ProvincialComposition>(
                new Error("composition.empty_province", "A province with no population cannot vote."));
        }

        return Result.Success(new ProvincialComposition(province, populationBySegment));
    }

    /// <summary>Each segment's share of this province, which is what "composition" means in the model.</summary>
    public IReadOnlyDictionary<SegmentId, decimal> NormalisedWeights()
    {
        var total = TotalPopulation;

        return PopulationBySegment.ToDictionary(p => p.Key, p => (decimal)p.Value / total);
    }
}
