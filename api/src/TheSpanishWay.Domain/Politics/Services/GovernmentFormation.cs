using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Politics.Services;

/// <summary>How an investiture vote went.</summary>
public sealed record InvestitureOutcome(bool Invested, int VotesFor, int VotesAgainst, bool WasFirstVote)
{
    public bool RequiresSecondVote => !Invested && WasFirstVote;
}

/// <summary>
/// Spanish investiture, simplified to its two decisive rules.
/// <para>
/// An absolute majority at the first vote; a simple majority at the second, where
/// abstentions help the candidate. If nobody is invested within two months, parliament is
/// dissolved and the election repeats — which is why abstention is a real strategic choice
/// rather than a way of avoiding one.
/// </para>
/// </summary>
public static class GovernmentFormation
{
    /// <summary>Days after an election before a failed formation forces a repeat.</summary>
    public const int FormationWindowDays = 60;

    public static InvestitureOutcome Vote(
        Parliament parliament,
        IReadOnlyCollection<PartyId> inFavour,
        IReadOnlyCollection<PartyId> against,
        bool isFirstVote)
    {
        ArgumentNullException.ThrowIfNull(parliament);
        ArgumentNullException.ThrowIfNull(inFavour);
        ArgumentNullException.ThrowIfNull(against);

        var votesFor = inFavour.Sum(parliament.SeatsFor);
        var votesAgainst = against.Sum(parliament.SeatsFor);

        var invested = isFirstVote
            ? votesFor >= parliament.AbsoluteMajority
            : votesFor > votesAgainst;

        return new InvestitureOutcome(invested, votesFor, votesAgainst, isFirstVote);
    }

    /// <summary>
    /// Forms a government after a successful investiture. The parties that voted for it but
    /// are not in the coalition become its external support.
    /// </summary>
    public static Result<GovernmentState> Form(
        PartyId candidate,
        IReadOnlyCollection<PartyId> coalition,
        IReadOnlyCollection<PartyId> votedInFavour,
        GameDate on,
        IReadOnlyCollection<PartyId> partiesInGame)
    {
        ArgumentNullException.ThrowIfNull(coalition);
        ArgumentNullException.ThrowIfNull(votedInFavour);

        var support = votedInFavour
            .Where(p => p != candidate && !coalition.Contains(p))
            .Distinct()
            .ToList();

        return GovernmentState.Form(candidate, coalition, support, on, partiesInGame);
    }

    /// <summary>Whether the window has closed with nobody invested.</summary>
    public static bool MustRepeatElection(GameDate electionDate, GameDate today) =>
        today.DaysSince(electionDate) >= FormationWindowDays;
}
