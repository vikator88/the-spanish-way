using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// How a segment differs from its national self in one province.
/// <para>
/// The minimum fix for regional realism. With six national segments holding identical
/// ideology everywhere, provinces can differ only by composition — and no composition of
/// identical segments produces a Basque, Catalan or Canarian regionalist vote, which under
/// this electoral system is what decides who governs. An offset on the territory axis is
/// enough to make that possible. See design D9.
/// </para>
/// </summary>
public sealed record ProvincialIdeologyOffset(
    SegmentId Segment,
    ProvinceId Province,
    IReadOnlyDictionary<IdeologicalAxis, decimal> Offsets)
{
    public static Result<ProvincialIdeologyOffset> Create(
        SegmentId segment,
        ProvinceId province,
        IReadOnlyDictionary<IdeologicalAxis, decimal> offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);

        // An offset larger than the axis itself would let a province invert a segment
        // outright, which is a different mechanism from a regional tilt.
        if (offsets.Values.Any(o => o is < -100m or > 100m))
        {
            return Result.Failure<ProvincialIdeologyOffset>(
                new Error("province_offset.out_of_range", "A provincial offset exceeds the axis range."));
        }

        return Result.Success(new ProvincialIdeologyOffset(segment, province, offsets));
    }

    /// <summary>A tilt on the territory axis alone, the commonest regional case.</summary>
    public static ProvincialIdeologyOffset Territorial(
        SegmentId segment, ProvinceId province, decimal towardDecentralisation) =>
        new(segment, province,
            new Dictionary<IdeologicalAxis, decimal>
            {
                [IdeologicalAxis.Territory] = towardDecentralisation,
            });
}
