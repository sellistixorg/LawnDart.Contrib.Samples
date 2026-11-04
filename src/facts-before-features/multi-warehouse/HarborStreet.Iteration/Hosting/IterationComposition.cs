using HarborStreet.Iteration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Dcb;
using LawnDart.EventSourcing;
using LawnDart.EventStore;

namespace HarborStreet.Iteration.Hosting;

public static class IterationComposition
{
    public const string ContextName = "default";

    public static IServiceCollection AddHarborStreetIteration(
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
            .WithEventTypes(IterationEventTypes.All);

        return services;
    }

    public static MultiWarehouseCheckout CreateCheckout(IServiceProvider sp)
    {
        var aggregates = sp.GetRequiredKeyedService<IAggregateRepository>(ContextName);
        var dcb = sp.GetRequiredKeyedService<IDcbRepository>(ContextName);
        return new MultiWarehouseCheckout(aggregates, dcb);
    }
}
