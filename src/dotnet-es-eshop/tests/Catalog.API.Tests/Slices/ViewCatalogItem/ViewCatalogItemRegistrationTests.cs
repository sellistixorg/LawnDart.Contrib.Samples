using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Catalog.API.Slices.ViewCatalogItem;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.ViewCatalogItem;

public class ViewCatalogItemRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_the_item_projection()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddCatalogContext(typeof(ViewCatalogItemProjection).Assembly);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
            EShopLawnDartHost.ContextName);
        var registration = Assert.Single(
            registrations,
            item => item.HandlerType == typeof(ViewCatalogItemProjection));

        Assert.Equal(ViewCatalogItemProjection.StorageKey, registration.StorageKey);
        Assert.Equal(ProjectionKind.SingleStream, registration.Kind);
        Assert.Equal(typeof(ViewCatalogItemRow), registration.ViewType);
    }
}
