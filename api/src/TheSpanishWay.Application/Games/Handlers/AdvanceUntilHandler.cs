using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Application.Abstractions.Persistence;
using TheSpanishWay.Application.Games.Commands;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Games.ValueObjects;

namespace TheSpanishWay.Application.Games.Handlers;

/// <summary>
/// Advances a game.
/// <para>
/// The shape every handler in this layer should have: load, authorise, call one domain
/// operation, persist, map. There is no rule here — what a day does, when the loop stops and
/// whether the game has ended are all decisions the aggregate makes. If a formula ever
/// appears in a file like this, it is in the wrong place.
/// </para>
/// </summary>
public sealed class AdvanceUntilHandler(
    IGameSessionRepository sessions,
    ICommandLog commandLog) : ICommandHandler<AdvanceUntil, AdvanceOutcome>
{
    private readonly IGameSessionRepository _sessions = sessions;
    private readonly ICommandLog _commandLog = commandLog;

    public async Task<Result<AdvanceOutcome>> Handle(
        AdvanceUntil command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _sessions.Load(command.Game, cancellationToken).ConfigureAwait(false);

        if (loaded.IsFailure)
        {
            return Result.Failure<AdvanceOutcome>(loaded.Error);
        }

        var session = loaded.Value;

        if (session.Revision != command.ExpectedRevision)
        {
            // Never overwrite newer state. The retry behaviour decides whether to try again.
            return Result.Failure<AdvanceOutcome>(
                new Error(
                    "game.revision_conflict",
                    $"Expected {command.ExpectedRevision} but the game is at {session.Revision}."));
        }

        var outcome = session.AdvanceUntil(command.Stop, command.MaximumDays);

        if (outcome.IsFailure)
        {
            return outcome;
        }

        var saved = await _sessions
            .Save(session, command.ExpectedRevision, cancellationToken)
            .ConfigureAwait(false);

        if (saved.IsFailure)
        {
            return Result.Failure<AdvanceOutcome>(saved.Error);
        }

        await _commandLog
            .Append(
                command.Game, nameof(AdvanceUntil), command.Stop.ToString() ?? string.Empty,
                command.ClientCommandId, session.Revision, cancellationToken)
            .ConfigureAwait(false);

        return outcome;
    }
}
