using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// What one segment currently makes of one party.
/// <para>
/// Deliberately holds no affinity. v0.1 stores affinity here <em>and</em> computes it from
/// ideological distance, which feeds the same component into utility twice and quietly
/// doubles the weight of ideology. Here, ideological fit is computed once from the party's
/// perceived position, and this record carries only what cannot be derived: trust, respect,
/// and how far the segment's picture of the party has drifted from what the party declares.
/// </para>
/// <para>
/// Writable only by the reaction pipeline. Content reaches this through stimuli or not at all.
/// </para>
/// </summary>
public sealed record SegmentPartyPerception(decimal Trust, decimal Respect, decimal PositionDrift)
{
    /// <summary>Neither trusted nor distrusted, and seen as it presents itself.</summary>
    public static SegmentPartyPerception Neutral { get; } = new(50m, 50m, 0m);

    public static Result<SegmentPartyPerception> Create(decimal trust, decimal respect, decimal positionDrift)
    {
        if (trust is < 0m or > 100m)
        {
            return Result.Failure<SegmentPartyPerception>(
                new Error("perception.trust_out_of_range", $"Trust {trust} is outside 0 to 100."));
        }

        if (respect is < 0m or > 100m)
        {
            return Result.Failure<SegmentPartyPerception>(
                new Error("perception.respect_out_of_range", $"Respect {respect} is outside 0 to 100."));
        }

        // A drift of −1 would read every position as dead centre; beyond that it would
        // start mirroring the party, which is not a thing a voter does.
        if (positionDrift is < -1m or > 1m)
        {
            return Result.Failure<SegmentPartyPerception>(
                new Error("perception.drift_out_of_range", $"Position drift {positionDrift} is outside −1 to +1."));
        }

        return Result.Success(new SegmentPartyPerception(trust, respect, positionDrift));
    }

    /// <summary>Decays toward a structural baseline, each dimension at its own half-life.</summary>
    public SegmentPartyPerception DecayedToward(
        SegmentPartyPerception baseline,
        decimal trustFraction,
        decimal respectFraction)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        return this with
        {
            Trust = Trust + ((baseline.Trust - Trust) * trustFraction),
            Respect = Respect + ((baseline.Respect - Respect) * respectFraction),
        };
    }
}
