using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// What one constituency produced: votes, blanks, and who cleared the threshold.
/// </summary>
public sealed record ProvincialResult(
    ProvinceId Province,
    IReadOnlyDictionary<PartyId, long> VotesByParty,
    long BlankVotes,
    long Electorate)
{
    /// <summary>
    /// Votes that count toward the threshold. Blank ballots are included, because the
    /// Spanish 3% is 3% of <em>valid</em> votes and a blank vote is valid.
    /// </summary>
    public long ValidVotes => VotesByParty.Values.Sum() + BlankVotes;

    public decimal Turnout => Electorate <= 0L ? 0m : (decimal)ValidVotes / Electorate;

    public decimal ShareOf(PartyId party) =>
        ValidVotes <= 0L
            ? 0m
            : (decimal)(VotesByParty.TryGetValue(party, out var votes) ? votes : 0L) / ValidVotes;
}

/// <summary>Seats won in one constituency.</summary>
public sealed record SeatAllocation(
    ProvinceId Province,
    IReadOnlyDictionary<PartyId, int> SeatsByParty,
    IReadOnlyList<PartyId> ExcludedByThreshold)
{
    public int TotalSeats => SeatsByParty.Values.Sum();

    public int SeatsFor(PartyId party) => SeatsByParty.TryGetValue(party, out var seats) ? seats : 0;
}
