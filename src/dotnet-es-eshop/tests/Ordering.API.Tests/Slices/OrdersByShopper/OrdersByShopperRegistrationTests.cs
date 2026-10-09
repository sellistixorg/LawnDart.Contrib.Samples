using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Slices.OrdersByShopper;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.OrdersByShopper;

public class OrdersByShopperRegistrationTests
{
    [Fact]
    public void Ordering_assembly_scan_registers_the_orders_projection()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddOrderingContext(
            typeof(OrdersByShopperProjection).Assembly,
            typeof(LawnDart.Samples.EShop.Shared.Events.OrderStarted).Assembly);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
            EShopLawnDartHost.ContextName);
        var registration = Assert.Single(
            registrations,
            item => item.HandlerType == typeof(OrdersByShopperProjection));

        Assert.Equal(OrdersByShopperProjection.StorageKey, registration.StorageKey);
        Assert.Equal(ProjectionKind.Global, registration.Kind);
        Assert.Equal(typeof(OrdersByShopperState), registration.ViewType);
    }
}
