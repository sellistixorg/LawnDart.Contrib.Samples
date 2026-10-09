using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.RemoveCatalogItem;

public class RemoveCatalogItemRegistrationTests
{
    [Fact]
    public void Each_catalog_event_type_name_is_declared_once()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(CatalogItemRemoved).Assembly;
        var declared = assembly.GetTypes()
            .Select(type => (Type: type, Name: EventTypeCatalog.TryGetDeclaredName(type)))
            .Where(entry => entry.Name is not null)
            .GroupBy(entry => entry.Name!)
            .ToArray();

        Assert.NotEmpty(declared);
        Assert.All(declared, group =>
            Assert.True(
                group.Count() == 1,
                $"Event type '{group.Key}' is declared {group.Count()} times."));
        Assert.Contains(
            declared,
            group => group.Key == "catalog-item-removed"
                && group.Single().Type == typeof(CatalogItemRemoved));
        Assert.Contains(
            declared,
            group => group.Key == "catalog-item-added"
                && group.Single().Type == typeof(CatalogItemAdded));
        Assert.Contains(
            declared,
            group => group.Key == "catalog-item-restocked"
                && group.Single().Type == typeof(CatalogItemRestocked));
        Assert.Contains(
            declared,
            group => group.Key == "catalog-stock-deducted"
                && group.Single().Type == typeof(CatalogStockDeducted));

        context.AddCatalogContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("catalog-item-removed", out var removedType));
        Assert.Equal(typeof(CatalogItemRemoved), removedType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<RemoveCatalogItemCommand>));
    }
}
