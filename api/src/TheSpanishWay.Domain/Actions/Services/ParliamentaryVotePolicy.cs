using TheSpanishWay.Domain.Actions.Enumerations;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Politics.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Actions.Services;

/// <summary>
/// How a party that is not the player votes on a bill.
/// <para>
/// By rule rather than by authored outcome: government parties back their own bill, and
/// everyone else weighs how close the bill is to them against how much they trust whoever
/// proposed it. The player's vote is never decided here — it is presented as an action.
/// </para>
/// </summary>
public static class ParliamentaryVotePolicy
{
    /// <summary>Above this ideological fit a party votes for; below the lower bound, against.</summary>
    private const decimal SupportThreshold = 62m;
    private const decimal OppositionThreshold = 42m;

    public static VoteChoice DecideFor(
        PartyState voter,
        IdeologicalProfile billPosition,
        GovernmentPosition position,
        decimal trustInProposer)
    {
        ArgumentNullException.ThrowIfNull(voter);
        ArgumentNullException.ThrowIfNull(billPosition);

        if (position is GovernmentPosition.Lead or GovernmentPosition.Coalition)
        {
            return VoteChoice.For;
        }

        var fit = IdeologicalAffinityCalculator.Between(
            voter.DeclaredIdeology, billPosition, AxisWeights.Uniform);

        // Trust shifts the verdict without overriding ideology: a trusted proposer can carry
        // a bill a party is lukewarm about, but not one it actively opposes.
        var adjusted = fit + ((trustInProposer - 50m) * 0.2m);

        if (position == GovernmentPosition.ParliamentarySupport)
        {
            adjusted += 10m;
        }

        return adjusted switch
        {
            >= SupportThreshold => VoteChoice.For,
            <= OppositionThreshold => VoteChoice.Against,
            _ => VoteChoice.Abstain,
        };
    }
}
