using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>One constituency, and how many seats it returns.</summary>
public sealed record ConstituencyDefinition(ProvinceId Province, string Name, long Population, int MinimumSeats)
{
    /// <summary>Seats assigned after the distribution runs. Zero until then.</summary>
    public int Seats { get; init; }
}

/// <summary>
/// The election law a scenario runs under. All of it is configuration: chamber size,
/// constituencies, minimum seats and threshold are per-scenario values, not constants
/// scattered through the rules.
/// </summary>
public sealed record ElectoralSystem(
    int ChamberSize,
    decimal ThresholdOfValidVotes,
    IReadOnlyList<ConstituencyDefinition> Constituencies)
{
    /// <summary>More than half the chamber: 176 of 350.</summary>
    public int AbsoluteMajority => (ChamberSize / 2) + 1;

    public static Result<ElectoralSystem> Create(
        int chamberSize,
        decimal thresholdOfValidVotes,
        IReadOnlyList<ConstituencyDefinition> constituencies)
    {
        ArgumentNullException.ThrowIfNull(constituencies);

        if (chamberSize <= 0)
        {
            return Result.Failure<ElectoralSystem>(
                new Error("electoral_system.invalid_chamber", "A chamber needs at least one seat."));
        }

        if (thresholdOfValidVotes is < 0m or > 1m)
        {
            return Result.Failure<ElectoralSystem>(
                new Error("electoral_system.invalid_threshold", "The threshold must be a fraction."));
        }

        if (constituencies.Count == 0)
        {
            return Result.Failure<ElectoralSystem>(
                new Error("electoral_system.no_constituencies", "An election needs constituencies."));
        }

        var minimumTotal = constituencies.Sum(c => c.MinimumSeats);

        if (minimumTotal > chamberSize)
        {
            return Result.Failure<ElectoralSystem>(
                new Error(
                    "electoral_system.minimums_exceed_chamber",
                    $"Minimum seats total {minimumTotal}, more than the {chamberSize}-seat chamber."));
        }

        return Result.Success(
            new ElectoralSystem(chamberSize, thresholdOfValidVotes, DistributeSeats(chamberSize, constituencies)));
    }

    /// <summary>
    /// Spanish Congress: 350 seats, a 3% constituency threshold on valid votes, two seats
    /// minimum per province and one each for Ceuta and Melilla.
    /// </summary>
    public static Result<ElectoralSystem> SpanishCongress(IReadOnlyList<ConstituencyDefinition> constituencies) =>
        Create(350, 0.03m, constituencies);

    /// <summary>
    /// Gives every constituency its minimum, then shares the remainder by population using
    /// largest remainder. v0.1 leaves this "configurable" without saying how; this is the
    /// real Congress rule.
    /// </summary>
    private static List<ConstituencyDefinition> DistributeSeats(
        int chamberSize,
        IReadOnlyList<ConstituencyDefinition> constituencies)
    {
        var ordered = constituencies.OrderBy(c => c.Province).ToList();
        var remaining = chamberSize - ordered.Sum(c => c.MinimumSeats);
        var totalPopulation = ordered.Sum(c => c.Population);

        if (remaining <= 0 || totalPopulation <= 0)
        {
            return [.. ordered.Select(c => c with { Seats = c.MinimumSeats })];
        }

        var exact = ordered
            .Select(c => (Constituency: c, Quota: (decimal)c.Population / totalPopulation * remaining))
            .ToList();

        var assigned = exact
            .Select(e => (e.Constituency, Whole: (int)Math.Floor(e.Quota), Fraction: e.Quota - Math.Floor(e.Quota)))
            .ToList();

        var distributed = assigned.Sum(a => a.Whole);

        // The largest fractional remainders take the seats that whole numbers left over.
        var byRemainder = assigned
            .OrderByDescending(a => a.Fraction)
            .ThenBy(a => a.Constituency.Province)
            .ToList();

        var extra = new Dictionary<ProvinceId, int>();

        for (var index = 0; index < remaining - distributed; index++)
        {
            var winner = byRemainder[index % byRemainder.Count].Constituency.Province;
            extra[winner] = extra.TryGetValue(winner, out var current) ? current + 1 : 1;
        }

        return
        [
            .. assigned.Select(a => a.Constituency with
            {
                Seats = a.Constituency.MinimumSeats + a.Whole
                    + (extra.TryGetValue(a.Constituency.Province, out var bonus) ? bonus : 0),
            }),
        ];
    }
}
