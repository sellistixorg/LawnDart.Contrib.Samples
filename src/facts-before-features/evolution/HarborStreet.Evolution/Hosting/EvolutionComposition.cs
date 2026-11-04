using HarborStreet.Evolution.BuiltInVersioning;
using HarborStreet.Evolution.Projectors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventStore;

namespace HarborStreet.Evolution.Hosting;

public static class EvolutionComposition
{
    public const string ContextName = "default";

    public static IServiceCollection AddHarborStreetEvolution(
        this IServiceCollection services,
        string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddLawnDart(o =>
        {
            o.RequireTenantId = false;
            o.EnableAuthorization = false;
        });

        services
            .AddBoundedContext(ContextName)
            .UseInMemory()
            .WithEventTypes(EvolutionEventTypes.All);

        // Second approach, side by side: one event name with numbered versions.
        services.AddBuiltInOrderVersioning();

        services.AddSingleton<EvolutionViewStore>();
        services.AddSingleton<EvolutionPipeline>();

        return services;
    }
}
