using HarborStreet.Catalog.Slice.Domain;
using HarborStreet.Catalog.Slice.Domain.Commands;
using HarborStreet.Catalog.Slice.Domain.Events;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.Catalog.Slice.Tests;

/// <summary>
/// Specs bound to SLICE.md — primary Add Title GWT plus supporting catalog commands.
/// </summary>
public class AddTitleToCatalogTests
{
// start snippet add-title-to-catalog
    [Fact]
    public async Task slice_gwt_add_title_to_catalog()
    {
        // Given a title that is not yet in the catalog
        // When Add Title to Catalog
        // Then Title Added to Catalog
        await using var ctx = BddTestContext.CreateInMemory(CatalogEventTypes.All);
        var titleId = Guid.NewGuid();

        var result = await AggregateSpec
            .For<Title>(ctx, titleId)
            .When(new AddTitleToCatalogCommand(
                Guid.NewGuid(),
                titleId,
                "The Lighthouse Keeper",
                52.00m,
                IsScarce: true))
            .ThenEmittedEvent<TitleAddedToCatalog>(
                e => e.TitleId == titleId
                     && e.Name == "The Lighthouse Keeper"
                     && e.Price == 52.00m
                     && e.IsScarce)
            .RunAsync();

        Assert.Equal("The Lighthouse Keeper", result.Aggregate.State.Name);
        Assert.True(result.Aggregate.State.IsScarce);
// end snippet add-title-to-catalog
    }

    [Fact]
    public async Task supporting_restock_after_add()
    {
        await using var ctx = BddTestContext.CreateInMemory(CatalogEventTypes.All);
        var titleId = Guid.NewGuid();
        var added = new TitleAddedToCatalog(
            Guid.NewGuid(), DateTime.UtcNow, titleId, "The Lighthouse Keeper", 52.00m, true);

        var result = await AggregateSpec
            .For<Title>(ctx, titleId)
            .Given(added)
            .When(new RestockTitleCommand(Guid.NewGuid(), titleId, 1))
            .ThenEmittedEvent<TitleRestocked>(e => e.Quantity == 1)
            .AndExpectedVersion(2)
            .RunAsync();

        Assert.Equal(1, result.Aggregate.State.AvailableUnits);
    }

    [Fact]
    public async Task supporting_update_price_rejects_missing_title()
    {
        await using var ctx = BddTestContext.CreateInMemory(CatalogEventTypes.All);
        var titleId = Guid.NewGuid();

        await AggregateSpec
            .For<Title>(ctx, titleId)
            .When(new UpdateTitlePriceCommand(Guid.NewGuid(), titleId, 10m))
            .ThenThrows<InvalidOperationException>()
            .RunAsync();
    }

    [Fact]
    public async Task catalog_event_types_match_slice_contract()
    {
        // Guardrail: AI must not drop types from CatalogEventTypes when model lists them.
        Assert.Equal(4, CatalogEventTypes.All.Length);
        Assert.Contains(typeof(TitleAddedToCatalog), CatalogEventTypes.All);
        Assert.Contains(typeof(TitlePriceUpdated), CatalogEventTypes.All);
        Assert.Contains(typeof(TitleRestocked), CatalogEventTypes.All);
        Assert.Contains(typeof(TitleMarkedScarce), CatalogEventTypes.All);
    }
}
