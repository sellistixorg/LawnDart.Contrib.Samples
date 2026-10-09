using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.DefineCatalogBrand;

public class CatalogAssemblyRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_define_brand_once()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(CatalogBrandDefined).Assembly;

        context.AddCatalogContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("catalog-brand-defined", out var eventType));
        Assert.Equal(typeof(CatalogBrandDefined), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<DefineCatalogBrandCommand>));

        var ex = Assert.Throws<InvalidOperationException>(() => context.AddCatalogContext(assembly));
        Assert.Contains("once", ex.Message, StringComparison.Ordinal);
    }
}
