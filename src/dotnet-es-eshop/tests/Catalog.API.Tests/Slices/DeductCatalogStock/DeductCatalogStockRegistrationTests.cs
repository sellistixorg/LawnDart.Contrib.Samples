using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.DeductCatalogStock;

public class DeductCatalogStockRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_deduct_catalog_stock()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(CatalogStockDeducted).Assembly;
        var deducted = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "catalog-stock-deducted")
            .ToArray();

        Assert.Single(deducted);
        Assert.Equal(typeof(CatalogStockDeducted), deducted[0]);

        context.AddCatalogContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("catalog-stock-deducted", out var eventType));
        Assert.Equal(typeof(CatalogStockDeducted), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<DeductCatalogStockCommand>));
    }
}
