using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Games.Aggregates;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Application.Abstractions.Persistence;

/// <summary>
/// Loads and saves the one mutable aggregate.
/// <para>
/// There is deliberately no repository for parties, characters or the electorate: they are
/// state components inside the session and are never loaded apart from it.
/// </para>
/// </summary>
public interface IGameSessionRepository
{
    Task<Result<GameSession>> Load(GameId id, CancellationToken cancellationToken = default);

    Task<Result> Save(GameSession session, Revision expectedRevision, CancellationToken cancellationToken = default);
}

/// <summary>Reads published scenario versions. Games pin a version and never see a later one.</summary>
public interface IScenarioRepository
{
    Task<Result<Domain.Content.Aggregates.ScenarioDefinition>> Load(
        ScenarioVersionRef reference, CancellationToken cancellationToken = default);
}

/// <summary>
/// Append-only history: elections, polls, action executions, event occurrences.
/// <para>
/// Written in the same transaction as the snapshot, and never read back to compute the next
/// state. If resuming a game ever needed one of these, the snapshot would be incomplete.
/// </para>
/// </summary>
public interface IHistoryStore
{
    Task Append<TRecord>(GameId game, TRecord record, CancellationToken cancellationToken = default)
        where TRecord : notnull;
}

/// <summary>
/// Every accepted command, in order.
/// <para>
/// With the seed and the scenario version this replays the whole game, which turns a player's
/// bug report into a reproducible test rather than a description of one.
/// </para>
/// </summary>
public interface ICommandLog
{
    Task Append(
        GameId game,
        string commandType,
        string parameters,
        Guid clientCommandId,
        Revision resultingRevision,
        CancellationToken cancellationToken = default);

    /// <summary>The stored outcome of a command already executed under this id, if any.</summary>
    Task<string?> FindResult(GameId game, Guid clientCommandId, CancellationToken cancellationToken = default);
}

/// <summary>One transaction around one command.</summary>
public interface IUnitOfWork
{
    Task<Result> Commit(CancellationToken cancellationToken = default);

    Task Rollback(CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates touched in this transaction, so their recorded events can be collected
    /// after a successful commit.
    /// </summary>
    IReadOnlyList<AggregateRoot<GameId>> TrackedAggregates { get; }
}

/// <summary>
/// Server time, for audit columns only.
/// <para>
/// No domain rule may read this. In-game time is <c>GameDate</c>, and a rule that consulted
/// the wall clock would make a saved game depend on when it was played.
/// </para>
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
