using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Games.Projections;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Application.Games.Queries;

/// <summary>A game at a glance, for a list of saved games.</summary>
public sealed record GameOverview(
    GameId Id,
    string Name,
    GameStatus Status,
    GamePhase Phase,
    GameDate CurrentDate,
    Revision Revision);

/// <summary>What a decision is asking of the player.</summary>
public sealed record ActiveDecision(
    EventOccurrenceId Occurrence,
    string Headline,
    IReadOnlyList<string> Options,
    GameDate? ExpiresOn);

/// <summary>
/// The player-facing queries.
/// <para>
/// Every one of these returns something already filtered through <see cref="PlayerView"/> or
/// built from public facts. None returns raw state.
/// </para>
/// </summary>
public sealed record GetGameOverview(GameId Game) : IQuery<GameOverview>;

public sealed record GetPlayerView(GameId Game) : IQuery<PlayerView>;

public sealed record GetAvailableActions(GameId Game) : IQuery<IReadOnlyList<ActionDefinitionId>>;

public sealed record GetActiveDecisions(GameId Game) : IQuery<IReadOnlyList<ActiveDecision>>;

public sealed record GetPollHistory(GameId Game, int Months) : IQuery<IReadOnlyList<Poll>>;

public sealed record GetElectionResults(GameId Game) : IQuery<IReadOnlyList<ElectionRecord>>;

public sealed record GetNewsFeed(GameId Game, int Days) : IQuery<IReadOnlyList<string>>;

public sealed record GetGameHistory(GameId Game) : IQuery<IReadOnlyList<string>>;
