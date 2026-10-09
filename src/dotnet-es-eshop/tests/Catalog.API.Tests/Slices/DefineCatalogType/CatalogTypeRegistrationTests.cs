using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.DefineCatalogType;

public class CatalogTypeRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_define_type()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");

        context.AddCatalogContext(typeof(CatalogTypeDefined).Assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("catalog-type-defined", out var eventType));
        Assert.Equal(typeof(CatalogTypeDefined), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<DefineCatalogTypeCommand>));
    }
}
