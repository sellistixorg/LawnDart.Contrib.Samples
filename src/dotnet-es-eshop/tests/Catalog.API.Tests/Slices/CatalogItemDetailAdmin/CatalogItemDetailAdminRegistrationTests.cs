using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Catalog.API.Slices.CatalogItemDetailAdmin;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.CatalogItemDetailAdmin;

public class CatalogItemDetailAdminRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_the_admin_item_projection()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddCatalogContext(typeof(CatalogItemDetailAdminProjection).Assembly);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
            EShopLawnDartHost.ContextName);
        var registration = Assert.Single(
            registrations,
            item => item.HandlerType == typeof(CatalogItemDetailAdminProjection));

        Assert.Equal(CatalogItemDetailAdminProjection.StorageKey, registration.StorageKey);
        Assert.Equal(ProjectionKind.SingleStream, registration.Kind);
        Assert.Equal(typeof(CatalogItemDetailAdminView), registration.ViewType);
    }
}
