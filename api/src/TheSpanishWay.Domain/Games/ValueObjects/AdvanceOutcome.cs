using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Games.ValueObjects;

/// <summary>
/// What one <c>AdvanceUntil</c> command did: how far it got and why it stopped. The reason
/// matters to the caller, because stopping at a decision point needs a different response
/// from running out of requested days.
/// </summary>
public sealed record AdvanceOutcome(
    int DaysAdvanced,
    GameDate FinalDate,
    StopReason Reason,
    GameStatus Status)
{
    /// <summary>True when the player must answer something before the game can continue.</summary>
    public bool NeedsPlayerDecision => Reason == StopReason.DecisionRequired;
}
