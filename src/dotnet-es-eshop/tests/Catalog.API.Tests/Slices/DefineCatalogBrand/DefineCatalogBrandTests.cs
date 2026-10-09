using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.DefineCatalogBrand;

public class DefineCatalogBrandTests
{
    [Fact]
    public async Task Define_brand_emits_catalog_brand_defined()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogBrandDefined));
        var brandId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogBrand>(ctx, brandId)
            .When(new DefineCatalogBrandCommand(Guid.NewGuid(), brandId, "Alpine"))
            .ThenEmittedEvent<CatalogBrandDefined>(e => e.BrandId == brandId && e.Name == "Alpine")
            .ThenAppendedOnlyTo($"CatalogBrand:{brandId}")
            .RunAsync();
    }

    [Fact]
    public async Task Define_brand_rejects_an_empty_brand_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogBrandDefined));

        await AggregateSpec
            .For<CatalogBrand>(ctx, Guid.NewGuid())
            .When(new DefineCatalogBrandCommand(Guid.NewGuid(), Guid.Empty, "Alpine"))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Brand id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Define_brand_rejects_a_blank_name()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogBrandDefined));
        var brandId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogBrand>(ctx, brandId)
            .When(new DefineCatalogBrandCommand(Guid.NewGuid(), brandId, "  "))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Brand name is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Define_brand_rejects_an_existing_brand()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogBrandDefined));
        var brandId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogBrand>(ctx, brandId)
            .Given(new CatalogBrandDefined(Guid.NewGuid(), DateTime.UtcNow, brandId, "Alpine"))
            .When(new DefineCatalogBrandCommand(Guid.NewGuid(), brandId, "Summit"))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Catalog brand is already defined.", result.Exception!.Message))
            .RunAsync();
    }
}
