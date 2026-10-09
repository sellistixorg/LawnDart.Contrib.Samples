using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.RestockCatalogItem;

public class RestockCatalogItemRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_restock_catalog_item()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(CatalogItemRestocked).Assembly;
        var restocked = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "catalog-item-restocked")
            .ToArray();

        Assert.Single(restocked);
        Assert.Equal(typeof(CatalogItemRestocked), restocked[0]);

        context.AddCatalogContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("catalog-item-restocked", out var eventType));
        Assert.Equal(typeof(CatalogItemRestocked), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<RestockCatalogItemCommand>));
    }
}
