using Microsoft.Extensions.Logging;
using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Application.Abstractions.Persistence;
using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Application.Behaviors;

/// <summary>
/// Wraps every request in a logging scope carrying the request name, so a failure can be
/// traced without guessing which command produced it.
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling {RequestName}")]
    private partial void LogHandling(string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "{RequestName} was rejected: {ErrorCode}")]
    private partial void LogRejected(string requestName, string errorCode);

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var name = typeof(TRequest).Name;

        // A dictionary scope avoids the params-array overload the analyzer objects to, and
        // gives structured loggers a named property rather than a positional one.
        using var scope = _logger.BeginScope(
            new Dictionary<string, object> { ["RequestName"] = name });

        LogHandling(name);

        var response = await next().ConfigureAwait(false);

        if (response is Result { IsFailure: true } failure)
        {
            // A rejected command is an expected outcome, not an incident.
            LogRejected(name, failure.Error.Code);
        }

        return response;
    }
}

/// <summary>Validates one request before it reaches a handler.</summary>
public interface IRequestValidator<in TRequest>
{
    /// <summary>
    /// Checks the request's shape and required fields. Never a business rule: whether a
    /// party can afford an action is the domain's judgement, not this layer's.
    /// </summary>
    Result Validate(TRequest request);
}

/// <summary>
/// Rejects malformed requests before they reach a handler.
/// <para>
/// Shape only. A validator that knew whether an action was available would be a business
/// rule living in the wrong layer, and would drift from the domain's own answer.
/// </para>
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IRequestValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IRequestValidator<TRequest>> _validators = [.. validators];

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        foreach (var validator in _validators)
        {
            var result = validator.Validate(request);

            if (result.IsFailure && ResultFactory.TryCreateFailure<TResponse>(result.Error, out var failure))
            {
                return failure;
            }
        }

        return await next().ConfigureAwait(false);
    }
}

/// <summary>
/// Opens a transaction, commits on success, and hands the collected domain events to the
/// dispatcher afterwards.
/// <para>
/// Publication happens strictly after commit. A handler reacting to a fact that was then
/// rolled back would be reacting to something that never happened.
/// </para>
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher dispatcher) : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDomainEventDispatcher _dispatcher = dispatcher;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next().ConfigureAwait(false);

        if (response is Result { IsFailure: true })
        {
            await _unitOfWork.Rollback(cancellationToken).ConfigureAwait(false);

            return response;
        }

        // Collect before committing: the commit may clear the change tracker.
        var events = _dispatcher.Collect(_unitOfWork.TrackedAggregates);

        var committed = await _unitOfWork.Commit(cancellationToken).ConfigureAwait(false);

        if (committed.IsFailure)
        {
            return ResultFactory.TryCreateFailure<TResponse>(committed.Error, out var failure)
                ? failure
                : response;
        }

        await _dispatcher.Dispatch(events, cancellationToken).ConfigureAwait(false);

        return response;
    }
}

/// <summary>
/// Retries a command that lost a race, for commands that have said retrying them is safe.
/// <para>
/// Opt-in through <see cref="IRetryableCommand"/>. Re-running a command from fresh state only
/// means the same thing for some of them: advancing the calendar is safe, executing a costed
/// action is not.
/// </para>
/// </summary>
public sealed partial class ConcurrencyRetryBehavior<TRequest, TResponse>(
    ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Retrying {RequestName} after a revision conflict (attempt {Attempt})")]
    private partial void LogRetry(string requestName, int attempt);

    private const int MaximumAttempts = 3;
    private const string ConflictCode = "game.revision_conflict";

    private readonly ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IRetryableCommand)
        {
            return await next().ConfigureAwait(false);
        }

        for (var attempt = 1; ; attempt++)
        {
            var response = await next().ConfigureAwait(false);

            if (response is not Result { IsFailure: true } failure
                || failure.Error.Code != ConflictCode
                || attempt >= MaximumAttempts)
            {
                return response;
            }

            LogRetry(typeof(TRequest).Name, attempt);
        }
    }
}

/// <summary>
/// Builds a failed <c>Result</c> of whatever shape the pipeline is carrying.
/// <para>
/// Needed because a behaviour is generic over its response and must be able to short-circuit
/// without knowing whether that is <c>Result</c> or <c>Result&lt;T&gt;</c>.
/// </para>
/// </summary>
internal static class ResultFactory
{
    internal static bool TryCreateFailure<TResponse>(Error error, out TResponse failure)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            failure = (TResponse)(object)Result.Failure(error);

            return true;
        }

        if (typeof(TResponse).IsGenericType
            && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var method = typeof(Result)
                .GetMethods()
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
                .MakeGenericMethod(typeof(TResponse).GetGenericArguments()[0]);

            failure = (TResponse)method.Invoke(null, [error])!;

            return true;
        }

        failure = default!;

        return false;
    }
}
