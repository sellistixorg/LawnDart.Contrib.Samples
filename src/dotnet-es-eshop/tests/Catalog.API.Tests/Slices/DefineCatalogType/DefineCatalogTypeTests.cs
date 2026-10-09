using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.DefineCatalogType;

public class DefineCatalogTypeTests
{
    [Fact]
    public async Task Define_type_emits_catalog_type_defined()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogTypeDefined));
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogType>(ctx, typeId)
            .When(new DefineCatalogTypeCommand(Guid.NewGuid(), typeId, "Mug"))
            .ThenEmittedEvent<CatalogTypeDefined>(e => e.TypeId == typeId && e.Name == "Mug")
            .ThenAppendedOnlyTo($"CatalogType:{typeId}")
            .RunAsync();
    }

    [Fact]
    public async Task Define_type_rejects_an_empty_type_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogTypeDefined));

        await AggregateSpec
            .For<CatalogType>(ctx, Guid.NewGuid())
            .When(new DefineCatalogTypeCommand(Guid.NewGuid(), Guid.Empty, "Mug"))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Type id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Define_type_rejects_a_blank_name()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogTypeDefined));
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogType>(ctx, typeId)
            .When(new DefineCatalogTypeCommand(Guid.NewGuid(), typeId, "  "))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Type name is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Define_type_rejects_an_existing_type()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogTypeDefined));
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogType>(ctx, typeId)
            .Given(new CatalogTypeDefined(Guid.NewGuid(), DateTime.UtcNow, typeId, "Mug"))
            .When(new DefineCatalogTypeCommand(Guid.NewGuid(), typeId, "Cup"))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Catalog type is already defined.", result.Exception!.Message))
            .RunAsync();
    }
}
