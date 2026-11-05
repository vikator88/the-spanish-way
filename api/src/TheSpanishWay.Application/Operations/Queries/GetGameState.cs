using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Domain.Games.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Application.Operations.Queries;

/// <summary>
/// Marks a query as requiring the operator role.
/// <para>
/// A marker rather than a convention, so the API layer can refuse to expose any query
/// carrying it without having to know what each one does.
/// </para>
/// </summary>
public interface IOperatorQuery;

/// <summary>
/// The whole simulation state, for an operator.
/// <para>
/// Lives in a different namespace from the player-facing queries on purpose. Imperfect
/// information is a design principle, so this is authorised by an operator role rather than
/// by owning the game — owning it is exactly what the player does, and letting them read
/// this would hand them the answers.
/// </para>
/// <para>
/// What it is for: reproducing a bug report, inspecting where a replay diverged, and
/// asserting state from the test kit. It is never wired to a player-facing endpoint, and an
/// architecture test says so.
/// </para>
/// </summary>
public sealed record GetGameState(GameId Game) : IQuery<GameStateSnapshot>, IOperatorQuery;

