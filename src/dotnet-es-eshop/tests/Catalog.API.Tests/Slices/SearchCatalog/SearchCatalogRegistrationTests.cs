using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Catalog.API.Slices.SearchCatalog;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.SearchCatalog;

public class SearchCatalogRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_the_search_projection()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddCatalogContext(typeof(SearchCatalogProjection).Assembly);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
            EShopLawnDartHost.ContextName);
        var registration = Assert.Single(
            registrations,
            item => item.HandlerType == typeof(SearchCatalogProjection));

        Assert.Equal(SearchCatalogProjection.StorageKey, registration.StorageKey);
        Assert.Equal(ProjectionKind.Global, registration.Kind);
        Assert.Equal(typeof(SearchCatalogState), registration.ViewType);
    }
}
