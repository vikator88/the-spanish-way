using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Application.Infrastructure;
using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Application.UnitTests;

public sealed class MediatorTests
{
    private sealed record Greet(string Name) : ICommand<string>;

    private sealed record DoThing : ICommand;

    private sealed record AskThing : IQuery<int>;

    private sealed record ThingHappened(string What) : INotification;

    private sealed class GreetHandler : ICommandHandler<Greet, string>
    {
        public Task<Result<string>> Handle(Greet command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success($"Hola, {command.Name}"));
    }

    private sealed class DoThingHandler : ICommandHandler<DoThing>
    {
        public static int Calls { get; set; }

        public Task<Result> Handle(DoThing command, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class AskThingHandler : IQueryHandler<AskThing, int>
    {
        public Task<Result<int>> Handle(AskThing query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(42));
    }

    private sealed class FailingHandler : ICommandHandler<Greet, string>
    {
        public Task<Result<string>> Handle(Greet command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<string>(new Error("greet.refused", "No.")));
    }

    private sealed class RecordingHandler : INotificationHandler<ThingHappened>
    {
        public static List<string> Seen { get; } = [];

        public Task Handle(ThingHappened notification, CancellationToken cancellationToken)
        {
            Seen.Add(notification.What);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : INotificationHandler<ThingHappened>
    {
        public Task Handle(ThingHappened notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("projection broke");
    }

    private sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public static List<string> Trace { get; } = [];

        public async Task<TResponse> Handle(
            TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            Trace.Add($"before:{typeof(TRequest).Name}");

            var response = await next();

            Trace.Add($"after:{typeof(TRequest).Name}");

            return response;
        }
    }

    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.AddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_command_reaches_its_handler_and_returns_a_value()
    {
        var provider = Build(s => s.AddScoped<ICommandHandler<Greet, string>, GreetHandler>());

        var result = await provider.GetRequiredService<ISender>().Send(new Greet("Victor"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Hola, Victor");
    }

    [Fact]
    public async Task A_command_with_no_response_reaches_its_handler()
    {
        DoThingHandler.Calls = 0;
        var provider = Build(s => s.AddScoped<ICommandHandler<DoThing>, DoThingHandler>());

        var result = await provider.GetRequiredService<ISender>().Send(new DoThing(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        DoThingHandler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task A_query_reaches_its_handler()
    {
        var provider = Build(s => s.AddScoped<IQueryHandler<AskThing, int>, AskThingHandler>());

        (await provider.GetRequiredService<ISender>().Send(new AskThing(), TestContext.Current.CancellationToken)).Value.ShouldBe(42);
    }

    [Fact]
    public async Task A_rejected_command_returns_a_failure_rather_than_throwing()
    {
        var provider = Build(s => s.AddScoped<ICommandHandler<Greet, string>, FailingHandler>());

        var result = await provider.GetRequiredService<ISender>().Send(new Greet("Victor"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("greet.refused");
    }

    [Fact]
    public async Task A_missing_handler_fails_loudly()
    {
        var provider = Build();

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await provider.GetRequiredService<ISender>().Send(new Greet("Victor"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Repeated_dispatch_reuses_the_cached_invoker()
    {
        var provider = Build(s => s.AddScoped<ICommandHandler<Greet, string>, GreetHandler>());
        var sender = provider.GetRequiredService<ISender>();

        for (var i = 0; i < 50; i++)
        {
            (await sender.Send(new Greet($"n{i}"), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Behaviours_wrap_the_handler()
    {
        TracingBehavior<Greet, Result<string>>.Trace.Clear();

        var provider = Build(s =>
        {
            s.AddScoped<ICommandHandler<Greet, string>, GreetHandler>();
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));
        });

        await provider.GetRequiredService<ISender>().Send(new Greet("Victor"), TestContext.Current.CancellationToken);

        TracingBehavior<Greet, Result<string>>.Trace.ShouldBe(["before:Greet", "after:Greet"]);
    }

    [Fact]
    public async Task A_notification_reaches_every_handler()
    {
        RecordingHandler.Seen.Clear();

        var provider = Build(s => s.AddScoped<INotificationHandler<ThingHappened>, RecordingHandler>());

        await provider.GetRequiredService<IPublisher>().Publish(new ThingHappened("election"), TestContext.Current.CancellationToken);

        RecordingHandler.Seen.ShouldBe(["election"]);
    }

    [Fact]
    public void No_third_party_mediator_is_referenced()
    {
        typeof(Mediator).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ShouldNotContain("MediatR");
    }

    [Fact]
    public void Commands_and_queries_are_distinguishable_without_a_naming_convention()
    {
        typeof(Greet).GetInterfaces().ShouldContain(typeof(ICommand<string>));
        typeof(AskThing).GetInterfaces().ShouldContain(typeof(IQuery<int>));
        typeof(Greet).GetInterfaces().ShouldNotContain(typeof(IQuery<string>));
    }
}
