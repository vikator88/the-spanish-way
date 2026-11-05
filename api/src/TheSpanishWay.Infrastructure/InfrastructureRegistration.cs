using Microsoft.Extensions.DependencyInjection;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Infrastructure.Content;

namespace TheSpanishWay.Infrastructure;

/// <summary>
/// Wires Infrastructure up.
/// <para>
/// Repository implementations arrive with the persistence slice; what is registered here is
/// what exists.
/// </para>
/// </summary>
public static class InfrastructureRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(AttributeRegistry.Default());
        services.AddSingleton<ContentValidator>();
        services.AddSingleton<JsonScenarioLoader>();

        return services;
    }
}
