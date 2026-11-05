using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Application.Abstractions.Messaging;

/// <summary>A request that changes something and returns no value.</summary>
public interface ICommand;

/// <summary>A request that changes something and returns a value.</summary>
public interface ICommand<out TResponse>;

/// <summary>A request that reads and never writes.</summary>
public interface IQuery<out TResponse>;

/// <summary>Handles a command that returns nothing.</summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handles a command that returns a value.</summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handles a query.</summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}

/// <summary>Something that happened, published to whoever cares.</summary>
public interface INotification;

/// <summary>Reacts to a notification. Never load-bearing for simulation correctness.</summary>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>Sends requests to their handlers.</summary>
public interface ISender
{
    Task<Result> Send(ICommand command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> Send<TResponse>(
        ICommand<TResponse> command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> Send<TResponse>(
        IQuery<TResponse> query, CancellationToken cancellationToken = default);
}

/// <summary>Publishes notifications to every handler that wants them.</summary>
public interface IPublisher
{
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

/// <summary>Both halves, for callers that genuinely need both.</summary>
public interface IMediator : ISender, IPublisher;

/// <summary>The next step in the pipeline.</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Wraps handler execution. Behaviours compose outermost-first, so the first registered sees
/// the request first and the response last.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// A command that may be retried after a concurrency conflict.
/// <para>
/// Opt-in, because retrying is only safe when re-running the command from fresh state means
/// the same thing. Advancing the calendar is; executing an action with a cost is not.
/// </para>
/// </summary>
public interface IRetryableCommand;

/// <summary>
/// A command carrying a client-supplied id, so a retried request executes at most once.
/// </summary>
public interface IIdempotentCommand
{
    Guid ClientCommandId { get; }
}
