using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.BasketContents;

public class BasketContentsRegistrationTests
{
    [Fact]
    public void Basket_assembly_scan_registers_the_basket_projection_once()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddBasketContext(typeof(BasketContentsProjection).Assembly);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
            EShopLawnDartHost.ContextName);
        var registration = Assert.Single(
            registrations,
            item => item.HandlerType == typeof(BasketContentsProjection));

        Assert.Equal(BasketContentsProjection.StorageKey, registration.StorageKey);
        Assert.Equal(ProjectionKind.SingleStream, registration.Kind);
        Assert.Equal(typeof(BasketContentsView), registration.ViewType);
    }
}
