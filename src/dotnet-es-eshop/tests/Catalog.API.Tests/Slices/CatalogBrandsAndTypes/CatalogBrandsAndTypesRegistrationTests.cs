using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.CatalogBrandsAndTypes;

public class CatalogBrandsAndTypesRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_the_brands_and_types_projection()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddCatalogContext(typeof(CatalogBrandsAndTypesProjection).Assembly);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
            EShopLawnDartHost.ContextName);
        var registration = Assert.Single(
            registrations,
            item => item.HandlerType == typeof(CatalogBrandsAndTypesProjection));

        Assert.Equal(CatalogBrandsAndTypesProjection.StorageKey, registration.StorageKey);
        Assert.Equal(ProjectionKind.Global, registration.Kind);
        Assert.Equal(typeof(CatalogBrandsAndTypesView), registration.ViewType);
    }
}
