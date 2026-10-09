using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.AddCatalogItem;

public class CatalogItemRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_add_catalog_item()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(CatalogItemAdded).Assembly;
        var declared = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "catalog-item-added")
            .ToArray();

        Assert.Single(declared);
        Assert.Equal(typeof(CatalogItemAdded), declared[0]);

        context.AddCatalogContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("catalog-item-added", out var eventType));
        Assert.Equal(typeof(CatalogItemAdded), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<AddCatalogItemCommand>));
    }
}
