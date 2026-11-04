using LawnDart.EventSourcing;
using LawnDart.EventStore;
using Microsoft.Extensions.DependencyInjection;

namespace HarborStreet.Evolution.BuiltInVersioning;

/// <summary>
/// Registers a second bounded context that versions Order Placed with the tooling's
/// built-in support, so the two approaches can run side by side in one sample.
/// </summary>
public static class BuiltInVersioningComposition
{
    public const string ContextName = "orders-versioned";

    public static IServiceCollection AddBuiltInOrderVersioning(this IServiceCollection services)
    {
// start snippet register-upcaster
        services
            .AddBoundedContext(ContextName)
            .UseInMemory()
            .WithEventTypes(typeof(OrderPlacedV1), typeof(OrderPlaced))
            .WithUpcasters(typeof(OrderPlacedV1ToCurrent));
// end snippet register-upcaster

        return services;
    }
}
