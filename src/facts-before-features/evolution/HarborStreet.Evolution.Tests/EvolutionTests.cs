using HarborStreet.Evolution;
using HarborStreet.Evolution.Evolution;
using HarborStreet.Evolution.Events;
using HarborStreet.Evolution.Projectors;
using HarborStreet.Evolution.Seeding;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.Evolution.Tests;

/// <summary>
/// Event upcast + projection families — OrderPlaced → OrderPlacedV2; Catalog v1/v2.
/// </summary>
public class EvolutionTests
{
    [Fact]
    public void upcast_maps_legacy_order_placed_to_v2_with_unknown_warehouse()
    {
        var legacy = new OrderPlaced(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        var v2 = OrderPlacedUpcaster.ToV2(legacy);

        Assert.Equal(legacy.OrderId, v2.OrderId);
        Assert.Equal(OrderPlacedUpcaster.UnknownWarehouseId, v2.WarehouseId);
        Assert.Null(v2.CustomerDisplayName);
    }

// start snippet mixed-history
    [Fact]
    public async Task mixed_history_rebuilds_both_order_projection_families()
    {
        await using var ctx = BddTestContext.CreateInMemory(EvolutionEventTypes.All);
        var titleId = Guid.NewGuid();
        var legacyOrder = Guid.NewGuid();
        var modernOrder = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var pipeline = new EvolutionPipeline(new EvolutionViewStore());

        await new EvolutionSeeder(ctx.EventStore).SeedMixedHistoryAsync(
            titleId, legacyOrder, modernOrder, warehouseId);
        await pipeline.RebuildAsync(ctx.EventStore);

        Assert.Equal(2, pipeline.Store.OrdersV1.Count);
        Assert.Equal(2, pipeline.Store.OrdersV2.Count);

        var legacyV2 = pipeline.Store.OrdersV2[legacyOrder];
        Assert.False(legacyV2.WarehouseKnown);
        Assert.Equal(OrderPlacedUpcaster.UnknownWarehouseId, legacyV2.WarehouseId);

        var modernV2 = pipeline.Store.OrdersV2[modernOrder];
        Assert.True(modernV2.WarehouseKnown);
        Assert.Equal(warehouseId, modernV2.WarehouseId);
        Assert.Equal("Casey Shore", modernV2.CustomerDisplayName);
    }
// end snippet mixed-history

    [Fact]
    public async Task catalog_v2_adds_scarcity_label_without_breaking_v1()
    {
        await using var ctx = BddTestContext.CreateInMemory(EvolutionEventTypes.All);
        var titleId = Guid.NewGuid();
        var pipeline = new EvolutionPipeline(new EvolutionViewStore());

        await new EvolutionSeeder(ctx.EventStore).SeedMixedHistoryAsync(
            titleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await pipeline.RebuildAsync(ctx.EventStore);

        Assert.Equal("The Lighthouse Keeper", pipeline.Store.CatalogV1[titleId].Name);
        Assert.Equal(52.00m, pipeline.Store.CatalogV1[titleId].Price);

        var v2 = pipeline.Store.CatalogV2[titleId];
        Assert.True(v2.IsScarce);
        Assert.Contains("Scarce", v2.ScarcityLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task rebuild_after_clear_replays_upcast_path()
    {
        await using var ctx = BddTestContext.CreateInMemory(EvolutionEventTypes.All);
        var titleId = Guid.NewGuid();
        var legacyOrder = Guid.NewGuid();
        var pipeline = new EvolutionPipeline(new EvolutionViewStore());

        await new EvolutionSeeder(ctx.EventStore).SeedMixedHistoryAsync(
            titleId, legacyOrder, Guid.NewGuid(), Guid.NewGuid());
        await pipeline.RebuildAsync(ctx.EventStore);

        pipeline.Store.Clear();
        Assert.Empty(pipeline.Store.OrdersV2);

        await pipeline.RebuildAsync(ctx.EventStore);

        Assert.True(pipeline.Store.OrdersV2.ContainsKey(legacyOrder));
        Assert.False(pipeline.Store.OrdersV2[legacyOrder].WarehouseKnown);
    }
}
