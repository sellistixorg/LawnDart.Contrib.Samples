using HarborStreet.Catalog.Commands;
using HarborStreet.Catalog.Events;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.Catalog.Tests;

/// <summary>
/// GWT-style specs for the catalog first cut (model: COMMANDS_AND_EVENTS.md §1).
/// </summary>
public class TitleCatalogTests
{
    private static readonly Type[] EventTypes =
    [
        typeof(TitleAddedToCatalog),
        typeof(TitlePriceUpdated),
        typeof(TitleRestocked),
        typeof(TitleMarkedScarce)
    ];
// start snippet add-title-emits-event

    [Fact]
    public async Task add_title_to_catalog_emits_title_added()
    {
        // Given a title that is not yet in the catalog
        // When Add Title to Catalog
        // Then Title Added to Catalog
        await using var ctx = BddTestContext.CreateInMemory(EventTypes);
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
                     && e.IsScarce,
                "scarce signed title should enter the catalog")
            .RunAsync();

// end snippet add-title-emits-event
        Assert.Equal("The Lighthouse Keeper", result.Aggregate.State.Name);
        Assert.True(result.Aggregate.State.IsScarce);
    }

    [Fact]
    public async Task update_title_price_after_add()
    {
        // Given Title Added to Catalog
        // When Update Title Price
        // Then Title Price Updated
        await using var ctx = BddTestContext.CreateInMemory(EventTypes);
        var titleId = Guid.NewGuid();
        var added = new TitleAddedToCatalog(
            Guid.NewGuid(), DateTime.UtcNow, titleId, "The Lighthouse Keeper", 52.00m, true);

        await AggregateSpec
            .For<Title>(ctx, titleId)
            .Given(added)
            .When(new UpdateTitlePriceCommand(Guid.NewGuid(), titleId, 55.00m))
            .ThenEmittedEvent<TitlePriceUpdated>(
                e => e.TitleId == titleId && e.NewPrice == 55.00m)
            .AndExpectedVersion(2)
            .RunAsync();
    }

    [Fact]
    public async Task update_title_price_when_missing_fails()
    {
        // Given no title in the catalog
        // When Update Title Price
        // Then rejection (title not in catalog)
        await using var ctx = BddTestContext.CreateInMemory(EventTypes);
        var titleId = Guid.NewGuid();

        await AggregateSpec
            .For<Title>(ctx, titleId)
            .When(new UpdateTitlePriceCommand(Guid.NewGuid(), titleId, 52.00m))
            .ThenThrows<InvalidOperationException>()
            .RunAsync();
    }

    [Fact]
    public async Task restock_title_increases_available_units()
    {
        // Given Title Added to Catalog
        // When Restock Title (1)
        // Then Title Restocked
        await using var ctx = BddTestContext.CreateInMemory(EventTypes);
        var titleId = Guid.NewGuid();
        var added = new TitleAddedToCatalog(
            Guid.NewGuid(), DateTime.UtcNow, titleId, "The Lighthouse Keeper", 52.00m, true);

        var result = await AggregateSpec
            .For<Title>(ctx, titleId)
            .Given(added)
            .When(new RestockTitleCommand(Guid.NewGuid(), titleId, 1))
            .ThenEmittedEvent<TitleRestocked>(e => e.TitleId == titleId && e.Quantity == 1)
            .AndExpectedVersion(2)
            .RunAsync();

        Assert.Equal(1, result.Aggregate.State.AvailableUnits);
    }

    [Fact]
    public async Task mark_title_scarce_after_ordinary_add()
    {
        await using var ctx = BddTestContext.CreateInMemory(EventTypes);
        var titleId = Guid.NewGuid();
        var added = new TitleAddedToCatalog(
            Guid.NewGuid(), DateTime.UtcNow, titleId, "Harbor Almanac", 12.00m, IsScarce: false);

        await AggregateSpec
            .For<Title>(ctx, titleId)
            .Given(added)
            .When(new MarkTitleScarceCommand(Guid.NewGuid(), titleId))
            .ThenEmittedEvent<TitleMarkedScarce>(e => e.TitleId == titleId)
            .AndExpectedVersion(2)
            .RunAsync();
    }
}
