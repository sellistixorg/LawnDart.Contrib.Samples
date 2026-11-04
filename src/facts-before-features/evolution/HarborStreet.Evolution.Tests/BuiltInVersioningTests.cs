using HarborStreet.Evolution.BuiltInVersioning;
using HarborStreet.Evolution.Hosting;
using LawnDart.EventStore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborStreet.Evolution.Tests;

/// <summary>
/// Second way to version Order Placed: one event name, numbered versions, registered upcaster.
/// </summary>
public class BuiltInVersioningTests
{
// start snippet read-version-1
    [Fact]
    public async Task version_1_order_reads_as_current_shape_with_unknown_warehouse()
    {
        // Given yesterday's build stored Order Placed as version 1 (no warehouse)
        // When today's build reads the stream
        // Then it sees the current shape, warehouse unknown, and the record still says version 1
        await using var provider = new ServiceCollection()
            .AddHarborStreetEvolution(Path.GetTempPath())
            .BuildServiceProvider();
        var log = provider.GetRequiredKeyedService<IEventLog>(BuiltInVersioningComposition.ContextName);
        var store = provider.GetRequiredKeyedService<IEventStore>(BuiltInVersioningComposition.ContextName);
        var orderId = Guid.NewGuid();
        var streamId = $"harbor-street:orders-versioned:{orderId:N}";

        await LegacyOrderHistory.WriteVersion1Async(log, streamId,
            new OrderPlacedV1(Guid.NewGuid(), DateTime.UtcNow, orderId, Guid.NewGuid(), Guid.NewGuid(), 1));

        var read = Assert.Single(await store.ReadStreamAsync(streamId));
        var current = Assert.IsType<OrderPlaced>(read.Event);
        Assert.Equal(orderId, current.OrderId);
        Assert.Equal(OrderPlacedV1ToCurrent.UnknownWarehouseId, current.WarehouseId);
        Assert.Null(current.CustomerDisplayName);
        Assert.Equal(1, read.Metadata.SchemaVersion);
    }
// end snippet read-version-1

    [Fact]
    public async Task new_writes_use_only_the_current_version()
    {
        // Given today's build
        // When it tries to append yesterday's shape
        // Then the append is refused; only the current version is written
        await using var provider = new ServiceCollection()
            .AddHarborStreetEvolution(Path.GetTempPath())
            .BuildServiceProvider();
        var store = provider.GetRequiredKeyedService<IEventStore>(BuiltInVersioningComposition.ContextName);
        var streamId = $"harbor-street:orders-versioned:{Guid.NewGuid():N}";

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AppendAsync(streamId,
            [new OrderPlacedV1(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1)]));

        var warehouseId = Guid.NewGuid();
        await store.AppendAsync(streamId,
        [
            new OrderPlaced(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
                warehouseId, "Casey Shore")
        ]);

        var read = Assert.Single(await store.ReadStreamAsync(streamId));
        Assert.Equal(2, read.Metadata.SchemaVersion);
        Assert.Equal(warehouseId, Assert.IsType<OrderPlaced>(read.Event).WarehouseId);
    }
}
