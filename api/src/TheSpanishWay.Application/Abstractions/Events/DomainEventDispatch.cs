using Microsoft.Extensions.Logging;
using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Application.Behaviors;

/// <summary>
/// Carries a domain event into the notification world.
/// <para>
/// This adapter is what lets <see cref="IDomainEvent"/> stay free of any Application type.
/// The Domain records facts and knows nothing about handlers; wrapping happens out here, at
/// the point where facts become somebody else's business.
/// </para>
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;

/// <summary>Collects recorded events and publishes them once their state is safely committed.</summary>
public interface IDomainEventDispatcher
{
    /// <summary>Takes the events off the aggregates and clears them.</summary>
    IReadOnlyList<IDomainEvent> Collect(IReadOnlyList<AggregateRoot<GameId>> aggregates);

    /// <summary>Publishes collected events. Called only after a successful commit.</summary>
    Task Dispatch(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes domain events through the mediator after commit.
/// <para>
/// A handler that throws does not fail the command: by the time anything is published the
/// state is already committed and valid. No notification handler is load-bearing for
/// simulation correctness — the next tick reads the snapshot, never a read model.
/// </para>
/// </summary>
public sealed partial class DomainEventDispatcher(
    IPublisher publisher,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A handler for {EventType} failed. The originating command still succeeded.")]
    private partial void LogHandlerFailure(Exception exception, string eventType);

    private readonly IPublisher _publisher = publisher;
    private readonly ILogger<DomainEventDispatcher> _logger = logger;

    public IReadOnlyList<IDomainEvent> Collect(IReadOnlyList<AggregateRoot<GameId>> aggregates)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        var collected = new List<IDomainEvent>();

        foreach (var aggregate in aggregates)
        {
            collected.AddRange(aggregate.DomainEvents);
            aggregate.ClearDomainEvents();
        }

        return collected;
    }

    public async Task Dispatch(
        IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var domainEvent in events)
        {
            try
            {
                await PublishWrapped(domainEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The command has already succeeded. A failing projection is worth knowing
                // about, but it cannot retroactively invalidate what was committed.
                LogHandlerFailure(exception, domainEvent.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Closes <see cref="DomainEventNotification{T}"/> over the event's runtime type, so
    /// handlers can subscribe to the concrete event rather than to the base interface.
    /// </summary>
    private Task PublishWrapped(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        var notification = Activator.CreateInstance(notificationType, domainEvent)!;

        var publish = typeof(IPublisher)
            .GetMethod(nameof(IPublisher.Publish))!
            .MakeGenericMethod(notificationType);

        return (Task)publish.Invoke(_publisher, [notification, cancellationToken])!;
    }
}
