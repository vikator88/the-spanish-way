using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Games.Events;

// Lifecycle facts, recorded on the aggregate and published by the Application layer after
// the transaction commits. The Domain never dispatches them and references no handler.
//
// These are C# domain events. An in-game corruption scandal is a *simulation* event and is
// modelled by GameEventDefinition, which is a different thing with a confusingly similar
// name. See application/domain-event-dispatch.

/// <summary>A game was created from a scenario but has not started.</summary>
public sealed record GameCreated(
    GameId GameId,
    OwnerId Owner,
    PartyId PlayerParty,
    GameDate OccurredOn) : IDomainEvent;

/// <summary>A game began advancing for the first time.</summary>
public sealed record GameStarted(GameId GameId, GameDate OccurredOn) : IDomainEvent;

/// <summary>The player chose to stop. Distinct from halting at a decision point.</summary>
public sealed record GamePaused(GameId GameId, GameDate OccurredOn) : IDomainEvent;

/// <summary>A paused game resumed, into the status named by <paramref name="ResumedInto"/>.</summary>
public sealed record GameResumed(
    GameId GameId,
    GameStatus ResumedInto,
    GameDate OccurredOn) : IDomainEvent;

/// <summary>The player gave up on this game.</summary>
public sealed record GameAbandoned(GameId GameId, GameDate OccurredOn) : IDomainEvent;

/// <summary>
/// The simulation halted because a blocking event needs an answer. The loop does not
/// advance past a decision point.
/// </summary>
public sealed record DecisionRequired(
    GameId GameId,
    EventOccurrenceId Occurrence,
    GameDate OccurredOn) : IDomainEvent;

/// <summary>A blocking occurrence was answered and no longer holds the simulation.</summary>
public sealed record DecisionResolved(
    GameId GameId,
    EventOccurrenceId Occurrence,
    GameDate OccurredOn) : IDomainEvent;

/// <summary>
/// A terminal condition was met. Losing an election is not one of these: party survival is
/// decided by its own policy.
/// </summary>
public sealed record GameEnded(GameId GameId, string Reason, GameDate OccurredOn) : IDomainEvent;

/// <summary>The political cycle moved on, for example a campaign opened or polling day arrived.</summary>
public sealed record PhaseChanged(
    GameId GameId,
    GamePhase From,
    GamePhase To,
    GameDate OccurredOn) : IDomainEvent;
