using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// A held election: what was cast, and what it returned.
/// <para>
/// Append-only history. Written in the same transaction as the snapshot, but never read back
/// to compute the next state — the snapshot alone has to be enough to carry on.
/// </para>
/// </summary>
public sealed record ElectionRecord(
    ElectionId Id,
    GameDate HeldOn,
    IReadOnlyList<ProvincialResult> ProvincialResults,
    IReadOnlyList<SeatAllocation> Allocations,
    IReadOnlyDictionary<PartyId, int> NationalSeats,
    IReadOnlyDictionary<PartyId, decimal> NationalShares,
    bool WasRepeatElection)
{
    public int TotalSeats => NationalSeats.Values.Sum();

    public long TotalValidVotes => ProvincialResults.Sum(r => r.ValidVotes);

    public decimal NationalTurnout
    {
        get
        {
            var electorate = ProvincialResults.Sum(r => r.Electorate);

            return electorate <= 0L ? 0m : (decimal)TotalValidVotes / electorate;
        }
    }

    public int SeatsFor(PartyId party) => NationalSeats.TryGetValue(party, out var seats) ? seats : 0;
}
