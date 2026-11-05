using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Politics.Enumerations;

namespace TheSpanishWay.Domain.Politics.Services;

/// <summary>When a party ceases to exist. Every number is a scenario configuration value.</summary>
public sealed record SurvivalThresholds(
    int MonthsOfDeficit,
    decimal MinimumCohesion,
    int ElectionsWithoutSeats)
{
    /// <summary>
    /// The MVP defaults. Placeholders, deliberately: v0.1 lists party survival as an open
    /// decision and these are the simplest rules that unblock a game-over condition.
    /// </summary>
    public static SurvivalThresholds Default { get; } = new(6, 15m, 2);
}

/// <summary>Why a party dissolved, so the player is told something more useful than "it ended".</summary>
public enum DissolutionReason
{
    None = 0,
    Bankruptcy = 1,
    Fragmentation = 2,
    ElectoralIrrelevance = 3,
    Leaderless = 4,
}

/// <summary>What the policy decided about one party.</summary>
public sealed record SurvivalVerdict(bool Survives, DissolutionReason Reason);

/// <summary>
/// Whether a party lives to fight another day.
/// <para>
/// Evaluated at month end, never on an election night. Losing an election — even badly, even
/// twice — is not in itself fatal. A party can be wiped out of parliament and still exist,
/// which is the whole point of a game about keeping a party alive rather than about winning.
/// </para>
/// </summary>
public static class PartySurvivalPolicy
{
    /// <summary>
    /// Judges one party. The edge cases v0.1 leaves open are named explicitly here rather
    /// than left to whatever the code happens to do.
    /// </summary>
    public static SurvivalVerdict Evaluate(PartyState party, SurvivalThresholds? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(party);

        var limits = thresholds ?? SurvivalThresholds.Default;

        if (!party.IsActive)
        {
            return new SurvivalVerdict(false, DissolutionReason.None);
        }

        if (party.ConsecutiveMonthsInDeficit >= limits.MonthsOfDeficit)
        {
            return new SurvivalVerdict(false, DissolutionReason.Bankruptcy);
        }

        if (party.Cohesion.Value < limits.MinimumCohesion)
        {
            return new SurvivalVerdict(false, DissolutionReason.Fragmentation);
        }

        if (party.ConsecutiveElectionsWithoutSeats >= limits.ElectionsWithoutSeats)
        {
            return new SurvivalVerdict(false, DissolutionReason.ElectoralIrrelevance);
        }

        // A party with nobody left to lead it has stopped being a party, whatever its
        // balance sheet says.
        if (party.ActiveMemberships.Count == 0)
        {
            return new SurvivalVerdict(false, DissolutionReason.Leaderless);
        }

        return new SurvivalVerdict(true, DissolutionReason.None);
    }

    /// <summary>
    /// Applies the verdict. The only path to <see cref="PartyStatus.Dissolved"/> anywhere in
    /// the model.
    /// </summary>
    public static SurvivalVerdict Apply(PartyState party, SurvivalThresholds? thresholds = null)
    {
        var verdict = Evaluate(party, thresholds);

        if (!verdict.Survives && verdict.Reason != DissolutionReason.None)
        {
            party.Dissolve();
        }

        return verdict;
    }
}
