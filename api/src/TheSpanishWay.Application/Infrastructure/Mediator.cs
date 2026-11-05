using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Application.Infrastructure;

/// <summary>
/// The mediator, written by hand rather than taken from a package.
/// <para>
/// The whole contract is a handful of interfaces, dispatch is one container lookup on a
/// closed generic type, and owning it keeps the Application layer's own abstractions in the
/// repository where they can be read and changed.
/// </para>
/// <para>
/// Handlers are found by closing the open generic interface over the request's runtime type
/// and resolving it. The invoker is compiled once per request type and cached, so the
/// reflection cost is paid at startup rather than on every call.
/// </para>
/// </summary>
public sealed class Mediator(IServiceProvider services) : IMediator
{
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task>> Invokers = new();

    private readonly IServiceProvider _services = services;

    public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return Dispatch<Result>(command, typeof(ICommandHandler<>).MakeGenericType(command.GetType()), cancellationToken);
    }

    public Task<Result<TResponse>> Send<TResponse>(
        ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));

        return Dispatch<Result<TResponse>>(command, handlerType, cancellationToken);
    }

    public Task<Result<TResponse>> Send<TResponse>(
        IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResponse));

        return Dispatch<Result<TResponse>>(query, handlerType, cancellationToken);
    }

    public async Task Publish<TNotification>(
        TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var handlers = _services.GetServices<INotificationHandler<TNotification>>();

        List<Exception>? failures = null;

        // Sequential and ordered, so a replay produces the same sequence of side effects.
        // Each handler is isolated: one broken projection must not stop the rest from
        // running, because no handler is load-bearing for anything another one does.
        foreach (var handler in handlers)
        {
            try
            {
                await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                (failures ??= []).Add(exception);
            }
        }

        // Reported once every handler has had its turn, so the caller still learns that
        // something went wrong.
        if (failures is not null)
        {
            throw new AggregateException(
                $"{failures.Count} handler(s) for {typeof(TNotification).Name} failed.", failures);
        }
    }

    private async Task<TResult> Dispatch<TResult>(
        object request, Type handlerType, CancellationToken cancellationToken)
    {
        var handler = _services.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler registered for {request.GetType().Name}. "
                + "Handler registration is validated at startup; this should be unreachable.");

        var invoker = Invokers.GetOrAdd(handlerType, BuildInvoker);

        var behaviors = BehaviorsFor(request.GetType(), typeof(TResult));

        // Compose the chain inside out, so the first behaviour registered runs outermost.
        RequestHandlerDelegate<TResult> pipeline = async () =>
            (TResult)await ToObjectTask(invoker(handler, request, cancellationToken)).ConfigureAwait(false);

        foreach (var behavior in behaviors.Reverse())
        {
            var next = pipeline;
            var current = behavior;

            pipeline = () => InvokeBehavior<TResult>(current, request, next, cancellationToken);
        }

        return await pipeline().ConfigureAwait(false);
    }

    private IReadOnlyList<object> BehaviorsFor(Type requestType, Type responseType)
    {
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);

        return [.. _services.GetServices(behaviorType).Where(b => b is not null).Select(b => b!)];
    }

    private static Task<TResult> InvokeBehavior<TResult>(
        object behavior,
        object request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        var method = behavior.GetType().GetMethod(nameof(IPipelineBehavior<object, object>.Handle))
            ?? throw new InvalidOperationException($"{behavior.GetType().Name} has no Handle method.");

        return (Task<TResult>)method.Invoke(behavior, [request, next, cancellationToken])!;
    }

    /// <summary>
    /// Builds a delegate that calls the handler's <c>Handle</c> method. Compiled once per
    /// handler type and reused, so no call pays for reflection twice.
    /// </summary>
    private static Func<object, object, CancellationToken, Task> BuildInvoker(Type handlerType)
    {
        var method = handlerType.GetMethod("Handle")
            ?? throw new InvalidOperationException($"{handlerType.Name} has no Handle method.");

        return (handler, request, token) => (Task)method.Invoke(handler, [request, token])!;
    }

    /// <summary>Bridges the weakly typed invoker back to the typed result the caller expects.</summary>
    private static async Task<object> ToObjectTask(Task task)
    {
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result")
            ?? throw new InvalidOperationException("A handler returned a task with no result.");

        return resultProperty.GetValue(task)!;
    }
}
