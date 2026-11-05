using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Application.Games.Commands;

/// <summary>
/// The player-facing command surface.
/// <para>
/// Every state-changing command carries the revision the client last saw, so a stale client
/// cannot overwrite newer state. Those that are safe to re-run from fresh state say so by
/// implementing <see cref="IRetryableCommand"/>.
/// </para>
/// </summary>
public sealed record CreateGame(
    OwnerId Owner,
    string Name,
    PartyId PlayerParty,
    ScenarioVersionRef Scenario,
    RandomSeed Seed,
    GameDate StartDate,
    Guid ClientCommandId) : ICommand<GameId>, IIdempotentCommand;

public sealed record StartGame(GameId Game, Revision ExpectedRevision, Guid ClientCommandId)
    : ICommand, IIdempotentCommand;

/// <summary>
/// Runs the simulation forward. Safe to retry: re-running it from fresh state advances from
/// wherever the game now is, which is what the caller wanted either way.
/// </summary>
public sealed record AdvanceUntil(
    GameId Game,
    StopCondition Stop,
    int MaximumDays,
    Revision ExpectedRevision,
    Guid ClientCommandId) : ICommand<AdvanceOutcome>, IIdempotentCommand, IRetryableCommand;

/// <summary>
/// Executes an action. Deliberately <em>not</em> retryable: it charges a cost, and re-running
/// it would charge twice.
/// </summary>
public sealed record ExecuteGameAction(
    GameId Game,
    ActionDefinitionId Action,
    IReadOnlyDictionary<string, string> Parameters,
    Revision ExpectedRevision,
    Guid ClientCommandId) : ICommand, IIdempotentCommand;

public sealed record ResolveEvent(
    GameId Game,
    EventOccurrenceId Occurrence,
    string OptionKey,
    Revision ExpectedRevision,
    Guid ClientCommandId) : ICommand, IIdempotentCommand;

public sealed record PauseGame(GameId Game, Revision ExpectedRevision, Guid ClientCommandId)
    : ICommand, IIdempotentCommand;

public sealed record ResumeGame(GameId Game, Revision ExpectedRevision, Guid ClientCommandId)
    : ICommand, IIdempotentCommand;

public sealed record AbandonGame(GameId Game, Revision ExpectedRevision, Guid ClientCommandId)
    : ICommand, IIdempotentCommand;
