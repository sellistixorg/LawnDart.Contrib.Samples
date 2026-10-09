using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.UpdateCatalogItem;

public class UpdateCatalogItemRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_update_catalog_item()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(CatalogItemUpdated).Assembly;
        var added = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "catalog-item-added")
            .ToArray();
        var updated = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "catalog-item-updated")
            .ToArray();
        var priceChanged = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "catalog-item-price-changed")
            .ToArray();

        Assert.Single(added);
        Assert.Equal(typeof(CatalogItemAdded), added[0]);
        Assert.Single(updated);
        Assert.Equal(typeof(CatalogItemUpdated), updated[0]);
        Assert.Single(priceChanged);
        Assert.Equal(typeof(CatalogItemPriceChanged), priceChanged[0]);

        context.AddCatalogContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("catalog-item-updated", out var updatedType));
        Assert.Equal(typeof(CatalogItemUpdated), updatedType);
        Assert.True(context.EventCatalog.TryResolveType("catalog-item-price-changed", out var priceChangedType));
        Assert.Equal(typeof(CatalogItemPriceChanged), priceChangedType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<UpdateCatalogItemCommand>));
    }
}
