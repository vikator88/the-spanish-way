using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TheSpanishWay.Application.Abstractions.Messaging;
using TheSpanishWay.Application.Behaviors;

namespace TheSpanishWay.Application.Infrastructure;

/// <summary>What is wrong with the handler registration, in terms an author can act on.</summary>
public sealed record RegistrationProblem(string RequestType, string Description)
{
    public override string ToString() => $"{RequestType}: {Description}";
}

/// <summary>
/// Wires the Application layer up, and checks the wiring.
/// <para>
/// A request with no handler, or with two, fails at startup rather than on the first request
/// that happens to need it. The whole reason to validate here is that a missing handler is a
/// deployment-time mistake and should be found at deployment time.
/// </para>
/// </summary>
public static class ApplicationRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(ApplicationRegistration).Assembly;

        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.AddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        RegisterImplementations(services, assembly, typeof(ICommandHandler<>));
        RegisterImplementations(services, assembly, typeof(ICommandHandler<,>));
        RegisterImplementations(services, assembly, typeof(IQueryHandler<,>));
        RegisterImplementations(services, assembly, typeof(INotificationHandler<>));
        RegisterImplementations(services, assembly, typeof(IRequestValidator<>));

        // Order matters: the first registered runs outermost. Logging sees everything,
        // including rejections from the behaviours inside it.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ConcurrencyRetryBehavior<,>));

        return services;
    }

    /// <summary>
    /// Every request type in the assembly has exactly one handler. Call this at startup and
    /// refuse to boot if it reports anything.
    /// </summary>
    public static IReadOnlyList<RegistrationProblem> ValidateHandlers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var problems = new List<RegistrationProblem>();
        var types = assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }).ToList();

        foreach (var request in types.Where(IsRequest).OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var handlers = types.Where(t => HandlesRequest(t, request)).ToList();

            if (handlers.Count == 0)
            {
                problems.Add(new RegistrationProblem(request.Name, "no handler is registered"));
            }
            else if (handlers.Count > 1)
            {
                problems.Add(new RegistrationProblem(
                    request.Name,
                    $"{handlers.Count} handlers compete: {string.Join(", ", handlers.Select(h => h.Name))}"));
            }
        }

        return problems;
    }

    private static bool IsRequest(Type type) =>
        type.GetInterfaces().Any(i =>
            i == typeof(ICommand)
            || (i.IsGenericType
                && (i.GetGenericTypeDefinition() == typeof(ICommand<>)
                    || i.GetGenericTypeDefinition() == typeof(IQuery<>))));

    private static bool HandlesRequest(Type candidate, Type request) =>
        candidate.GetInterfaces().Any(i =>
            i.IsGenericType
            && i.GetGenericTypeDefinition() is var definition
            && (definition == typeof(ICommandHandler<>)
                || definition == typeof(ICommandHandler<,>)
                || definition == typeof(IQueryHandler<,>))
            && i.GetGenericArguments()[0] == request);

    private static void RegisterImplementations(
        IServiceCollection services, Assembly assembly, Type openInterface)
    {
        var implementations = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface)
                .Select(i => (Service: i, Implementation: t)))
            .OrderBy(pair => pair.Implementation.Name, StringComparer.Ordinal);

        foreach (var (service, implementation) in implementations)
        {
            services.AddScoped(service, implementation);
        }
    }
}
