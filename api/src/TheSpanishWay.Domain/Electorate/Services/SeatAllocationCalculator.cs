using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Electorate.Services;

/// <summary>
/// D'Hondt, applied per constituency.
/// <para>
/// Pure and deterministic. The only randomness is drawing lots for a seat that is still tied
/// after total votes have been compared, and that draw comes from the seeded <c>ties</c>
/// stream so a replay resolves it the same way.
/// </para>
/// </summary>
public static class SeatAllocationCalculator
{
    /// <summary>
    /// Allocates a constituency's seats.
    /// <para>
    /// Parties below the threshold are excluded first. The threshold is a share of
    /// <em>valid</em> votes, which includes blank ballots — so blanks make it marginally
    /// harder for a small party to qualify, exactly as they do in reality.
    /// </para>
    /// </summary>
    public static SeatAllocation Allocate(
        ProvincialResult result,
        int seats,
        decimal thresholdOfValidVotes,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(random);

        if (seats <= 0 || result.ValidVotes <= 0L)
        {
            return new SeatAllocation(result.Province, new Dictionary<PartyId, int>(), []);
        }

        var minimumVotes = (decimal)result.ValidVotes * thresholdOfValidVotes;

        var qualifying = result.VotesByParty
            .Where(p => p.Value > 0L && p.Value >= minimumVotes)
            .OrderBy(p => p.Key)
            .ToDictionary(p => p.Key, p => p.Value);

        var excluded = result.VotesByParty
            .Where(p => !qualifying.ContainsKey(p.Key))
            .Select(p => p.Key)
            .OrderBy(p => p)
            .ToList();

        if (qualifying.Count == 0)
        {
            return new SeatAllocation(result.Province, new Dictionary<PartyId, int>(), excluded);
        }

        var awarded = qualifying.ToDictionary(p => p.Key, _ => 0);

        for (var seat = 0; seat < seats; seat++)
        {
            var winner = NextSeat(qualifying, awarded, random);

            awarded[winner]++;
        }

        return new SeatAllocation(result.Province, awarded, excluded);
    }

    /// <summary>
    /// The party with the highest quotient <c>votes / (seats already held + 1)</c>.
    /// <para>
    /// Ties are broken by total votes first, as the law requires, and only then by lot. The
    /// quotient comparison stays in <c>decimal</c>: floating point could make two genuinely
    /// equal quotients compare unequal and silently skip the tie rule.
    /// </para>
    /// </summary>
    private static PartyId NextSeat(
        Dictionary<PartyId, long> votes,
        Dictionary<PartyId, int> awarded,
        IRandomSource random)
    {
        var quotients = votes
            .Select(p => (Party: p.Key, Quotient: (decimal)p.Value / (awarded[p.Key] + 1), Votes: p.Value))
            .OrderBy(q => q.Party)
            .ToList();

        var best = quotients.Max(q => q.Quotient);
        var leaders = quotients.Where(q => q.Quotient == best).ToList();

        if (leaders.Count == 1)
        {
            return leaders[0].Party;
        }

        var mostVotes = leaders.Max(l => l.Votes);
        var stillTied = leaders.Where(l => l.Votes == mostVotes).ToList();

        if (stillTied.Count == 1)
        {
            return stillTied[0].Party;
        }

        // Genuinely indistinguishable: the law draws lots, and so do we — reproducibly.
        return random.Pick(RandomStream.Ties, [.. stillTied.Select(t => t.Party)]);
    }

    /// <summary>Seats summed across every constituency, which is what forms a government.</summary>
    public static IReadOnlyDictionary<PartyId, int> NationalSeats(
        IReadOnlyList<SeatAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);

        var totals = new Dictionary<PartyId, int>();

        foreach (var allocation in allocations.OrderBy(a => a.Province))
        {
            foreach (var (party, seats) in allocation.SeatsByParty.OrderBy(p => p.Key))
            {
                totals[party] = (totals.TryGetValue(party, out var running) ? running : 0) + seats;
            }
        }

        return totals;
    }
}
