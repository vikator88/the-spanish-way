using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Politics.Entities;

/// <summary>
/// Who holds the seats.
/// <para>
/// Seats live here, not on parties. They are an election outcome, and the chamber is the one
/// place that knows the whole picture — which is what a majority test needs. v0.1 puts a
/// seat count on each party, where nothing stops the sum drifting from the chamber size.
/// </para>
/// </summary>
public sealed class Parliament
{
    private readonly Dictionary<PartyId, int> _seats;

    private Parliament(int chamberSize, Dictionary<PartyId, int> seats)
    {
        ChamberSize = chamberSize;
        _seats = seats;
    }

    public int ChamberSize { get; }

    /// <summary>More than half the chamber: 176 of 350.</summary>
    public int AbsoluteMajority => (ChamberSize / 2) + 1;

    public static Parliament Empty(int chamberSize = 350) => new(chamberSize, []);

    public static Parliament From(int chamberSize, IReadOnlyDictionary<PartyId, int> seats)
    {
        ArgumentNullException.ThrowIfNull(seats);

        return new Parliament(chamberSize, new Dictionary<PartyId, int>(seats));
    }

    public IReadOnlyDictionary<PartyId, int> Seats =>
        _seats.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);

    public int SeatsFor(PartyId party) => _seats.TryGetValue(party, out var seats) ? seats : 0;

    public int AllocatedSeats => _seats.Values.Sum();

    /// <summary>Replaces the whole distribution. Only an election does this.</summary>
    public Result ReplaceWith(IReadOnlyDictionary<PartyId, int> seats)
    {
        ArgumentNullException.ThrowIfNull(seats);

        var total = seats.Values.Sum();

        if (total > ChamberSize)
        {
            return Result.Failure(
                new Error(
                    "parliament.too_many_seats",
                    $"{total} seats allocated in a {ChamberSize}-seat chamber."));
        }

        _seats.Clear();

        foreach (var (party, count) in seats.OrderBy(p => p.Key))
        {
            _seats[party] = count;
        }

        return Result.Success();
    }

    /// <summary>Whether a set of parties together command an absolute majority.</summary>
    public bool CommandsAbsoluteMajority(IReadOnlyCollection<PartyId> parties)
    {
        ArgumentNullException.ThrowIfNull(parties);

        return parties.Sum(SeatsFor) >= AbsoluteMajority;
    }

    /// <summary>Whether more votes are cast for than against, which is the second investiture test.</summary>
    public bool CommandsSimpleMajority(
        IReadOnlyCollection<PartyId> inFavour,
        IReadOnlyCollection<PartyId> against)
    {
        ArgumentNullException.ThrowIfNull(inFavour);
        ArgumentNullException.ThrowIfNull(against);

        return inFavour.Sum(SeatsFor) > against.Sum(SeatsFor);
    }

    public PartyId? LargestParty =>
        _seats.Count == 0
            ? null
            : _seats.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
}
