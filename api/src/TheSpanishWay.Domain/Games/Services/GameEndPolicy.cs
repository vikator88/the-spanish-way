using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Politics.Services;

namespace TheSpanishWay.Domain.Games.Services;

/// <summary>Whether the game is over, and why.</summary>
public sealed record GameEndVerdict(bool IsOver, string Reason)
{
    public static GameEndVerdict Continues { get; } = new(false, string.Empty);
}

/// <summary>
/// The terminal condition.
/// <para>
/// For the MVP there is exactly one: the player's party ceases to exist. Losing an election
/// is not it, nor is being out of office for a decade — the game is about survival, and a
/// party in opposition is still alive.
/// </para>
/// </summary>
public static class GameEndPolicy
{
    public static GameEndVerdict Evaluate(PartyState playerParty, SurvivalThresholds? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(playerParty);

        var verdict = PartySurvivalPolicy.Evaluate(playerParty, thresholds);

        if (verdict.Survives)
        {
            return GameEndVerdict.Continues;
        }

        return new GameEndVerdict(
            true,
            verdict.Reason switch
            {
                DissolutionReason.Bankruptcy => "The party ran out of money.",
                DissolutionReason.Fragmentation => "The party tore itself apart.",
                DissolutionReason.ElectoralIrrelevance => "The party lost its last seat and never won another.",
                DissolutionReason.Leaderless => "The party was left with nobody to lead it.",
                _ => "The party no longer exists.",
            });
    }
}
