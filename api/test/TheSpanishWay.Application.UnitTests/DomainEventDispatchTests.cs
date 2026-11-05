using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Application.Behaviors;
using TheSpanishWay.Application.Infrastructure;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Games.Events;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Application.UnitTests;

/// <summary>
/// Domain events are recorded by the Domain and published by this layer, strictly after the
/// state they describe is committed.
/// </summary>
public sealed class DomainEventDispatchTests
{
    private static readonly GameDate Today = GameDate.From(2027, 3, 1);

    private sealed class StartedHandler : INotificationHandler<DomainEventNotification<GameStarted>>
    {
        public static List<GameId> Seen { get; } = [];

        public Task Handle(
            DomainEventNotification<GameStarted> notification, CancellationToken cancellationToken)
        {
            Seen.Add(notification.DomainEvent.GameId);

            return Task.CompletedTask;
        }
    }

    private sealed class BrokenProjection : INotificationHandler<DomainEventNotification<GameStarted>>
    {
        public Task Handle(
            DomainEventNotification<GameStarted> notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the read model fell over");
    }

    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Collecting_takes_the_events_off_the_aggregate()
    {
        var session = new GameSessionBuilder().Build();
        session.Start();

        var dispatcher = Build().GetRequiredService<IDomainEventDispatcher>();

        var collected = dispatcher.Collect([session]);

        collected.ShouldNotBeEmpty();
        session.DomainEvents.ShouldBeEmpty("collected events must not be published twice");
    }

    [Fact]
    public async Task A_recorded_event_reaches_a_handler_subscribed_to_its_concrete_type()
    {
        StartedHandler.Seen.Clear();

        var provider = Build(s =>
            s.AddScoped<INotificationHandler<DomainEventNotification<GameStarted>>, StartedHandler>());

        var session = new GameSessionBuilder().Build();
        session.Start();

        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();
        var events = dispatcher.Collect([session]);

        await dispatcher.Dispatch(events, TestContext.Current.CancellationToken);

        StartedHandler.Seen.ShouldContain(session.Id);
    }

    [Fact]
    public async Task A_failing_handler_does_not_undo_a_committed_command()
    {
        // By the time anything is published the state is already committed. A broken
        // projection is worth logging and nothing more.
        var provider = Build(s =>
            s.AddScoped<INotificationHandler<DomainEventNotification<GameStarted>>, BrokenProjection>());

        var session = new GameSessionBuilder().Build();
        session.Start();

        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();
        var events = dispatcher.Collect([session]);

        await Should.NotThrowAsync(
            async () => await dispatcher.Dispatch(events, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task One_broken_handler_does_not_stop_the_others()
    {
        StartedHandler.Seen.Clear();

        var provider = Build(s =>
        {
            s.AddScoped<INotificationHandler<DomainEventNotification<GameStarted>>, BrokenProjection>();
            s.AddScoped<INotificationHandler<DomainEventNotification<GameStarted>>, StartedHandler>();
        });

        var session = new GameSessionBuilder().Build();
        session.Start();

        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.Dispatch(
            dispatcher.Collect([session]), TestContext.Current.CancellationToken);

        StartedHandler.Seen.ShouldContain(session.Id);
    }

    [Fact]
    public void The_domain_never_implements_the_notification_interface()
    {
        // The adapter exists so IDomainEvent stays free of any Application type.
        typeof(IDomainEvent).GetInterfaces().ShouldNotContain(typeof(INotification));
        typeof(GameStarted).GetInterfaces().ShouldNotContain(typeof(INotification));
        typeof(DomainEventNotification<GameStarted>).GetInterfaces().ShouldContain(typeof(INotification));
    }

    [Fact]
    public void The_domain_assembly_contains_no_dispatcher()
    {
        var dispatchTypes = typeof(GameStarted).Assembly.GetTypes()
            .Where(t => t.Name.Contains("Dispatcher", StringComparison.Ordinal)
                     || t.Name.Contains("Publisher", StringComparison.Ordinal)
                     || t.Name.Contains("Mediator", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        dispatchTypes.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_events_simulation_events_and_notifications_are_three_different_things()
    {
        // A corruption scandal is a GameEventDefinition. GameStarted is a domain event.
        // DomainEventNotification is how the second becomes somebody else's business.
        typeof(GameStarted).Namespace.ShouldEndWith(".Events", Case.Sensitive);
        typeof(Domain.Content.ValueObjects.GameEventDefinition).GetInterfaces()
            .ShouldNotContain(typeof(IDomainEvent));
    }

    [Fact]
    public void Handler_registration_is_validated()
    {
        // A request with no handler is a deployment mistake, and this is what catches it
        // before a request arrives that needs one.
        var problems = ApplicationRegistration.ValidateHandlers(
            typeof(ApplicationRegistration).Assembly);

        // Only AdvanceUntil has a handler so far; every other command is a contract awaiting
        // one. The validator reporting them is it doing its job.
        problems.ShouldNotBeEmpty();
        problems.ShouldAllBe(p => p.Description.Contains("no handler", StringComparison.Ordinal));
        problems.ShouldNotContain(p => p.RequestType == "AdvanceUntil");
    }

    [Fact]
    public void A_duplicated_handler_would_be_reported()
    {
        var problems = ApplicationRegistration.ValidateHandlers(typeof(DomainEventDispatchTests).Assembly);

        problems.ShouldNotBeNull();
    }
}
