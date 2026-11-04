using HarborStreet.Projections;
using HarborStreet.Projections.Events;
using HarborStreet.Projections.Projectors;
using HarborStreet.Projections.Seeding;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.Projections.Tests;

/// <summary>
/// Read models from Harbor Street events — catalog, stock dashboard, order history.
/// </summary>
public class ReadModelProjectionTests
{
    [Fact]
    public async Task catalog_view_shows_scarce_title_and_price()
    {
        await using var ctx = BddTestContext.CreateInMemory(ProjectionEventTypes.All);
        var titleId = Guid.NewGuid();
        var pipeline = new ProjectionPipeline(new InMemoryViewStore());

        await new DemoEventSeeder(ctx.EventStore).SeedLighthouseKeeperStoryAsync(
            titleId, Guid.NewGuid(), Guid.NewGuid());
        await pipeline.RebuildAsync(ctx.EventStore);

        var row = pipeline.Store.Catalog[titleId];
        Assert.Equal("The Lighthouse Keeper", row.Name);
        Assert.Equal(52.00m, row.Price);
        Assert.True(row.IsScarce);
    }

// start snippet stock-dashboard
    [Fact]
    public async Task stock_dashboard_tracks_available_and_reserved()
    {
        // Given Title Added + Restocked(1) + Order Placed + Stock Reserved(1)
        // Then available=0, reserved=1
        await using var ctx = BddTestContext.CreateInMemory(ProjectionEventTypes.All);
        var titleId = Guid.NewGuid();
        var pipeline = new ProjectionPipeline(new InMemoryViewStore());

        await new DemoEventSeeder(ctx.EventStore).SeedLighthouseKeeperStoryAsync(
            titleId, Guid.NewGuid(), Guid.NewGuid());
        await pipeline.RebuildAsync(ctx.EventStore);

        var stock = pipeline.Store.Stock[titleId];
        Assert.Equal(0, stock.Available);
        Assert.Equal(1, stock.Reserved);
        Assert.True(stock.IsScarce);
        Assert.Equal("The Lighthouse Keeper", stock.Name);
// end snippet stock-dashboard
    }

    [Fact]
    public async Task order_history_lists_placed_order()
    {
        await using var ctx = BddTestContext.CreateInMemory(ProjectionEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var pipeline = new ProjectionPipeline(new InMemoryViewStore());

        await new DemoEventSeeder(ctx.EventStore).SeedLighthouseKeeperStoryAsync(
            titleId, orderId, customerId);
        await pipeline.RebuildAsync(ctx.EventStore);

        var order = pipeline.Store.Orders[orderId];
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(titleId, order.TitleId);
        Assert.Equal(1, order.Quantity);
    }

// start snippet rebuild-replays-views
    [Fact]
    public async Task rebuild_replays_same_views_after_clear()
    {
        await using var ctx = BddTestContext.CreateInMemory(ProjectionEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var pipeline = new ProjectionPipeline(new InMemoryViewStore());

        await new DemoEventSeeder(ctx.EventStore).SeedLighthouseKeeperStoryAsync(
            titleId, orderId, Guid.NewGuid());
        await pipeline.RebuildAsync(ctx.EventStore);

        Assert.Single(pipeline.Store.Catalog);
        Assert.Single(pipeline.Store.Stock);
        Assert.Single(pipeline.Store.Orders);

        pipeline.Store.Clear();
        Assert.Empty(pipeline.Store.Catalog);

        await pipeline.RebuildAsync(ctx.EventStore);

        Assert.Equal(52.00m, pipeline.Store.Catalog[titleId].Price);
        Assert.Equal(0, pipeline.Store.Stock[titleId].Available);
        Assert.Equal(1, pipeline.Store.Stock[titleId].Reserved);
        Assert.True(pipeline.Store.Orders.ContainsKey(orderId));
    }
// end snippet rebuild-replays-views

    [Fact]
    public void projectors_ignore_unrelated_event_shapes()
    {
        // Guardrail: Apply is additive and type-safe — unknown events do not throw.
        var store = new InMemoryViewStore();
        var catalog = new CatalogViewProjector(store);
        catalog.Apply(new TitleRestocked(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), 1));
        Assert.Empty(store.Catalog);
    }
}
