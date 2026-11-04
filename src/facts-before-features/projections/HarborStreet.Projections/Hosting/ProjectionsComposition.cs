using HarborStreet.Projections.Projectors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventStore;

namespace HarborStreet.Projections.Hosting;

public static class ProjectionsComposition
{
    public const string ContextName = "default";

    public static IServiceCollection AddHarborStreetProjections(
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
            .WithEventTypes(ProjectionEventTypes.All);

        services.AddSingleton<InMemoryViewStore>();
        services.AddSingleton<ProjectionPipeline>();

        return services;
    }
}
