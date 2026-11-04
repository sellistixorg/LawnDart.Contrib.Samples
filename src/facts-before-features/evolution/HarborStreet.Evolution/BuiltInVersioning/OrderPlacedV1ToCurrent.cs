using LawnDart.EventStore;

namespace HarborStreet.Evolution.BuiltInVersioning;

/// <summary>
/// One hop, forward only: version 1 to version 2. Runs on read; the stored bytes stay version 1.
/// </summary>
// start snippet upcast-v1
public sealed class OrderPlacedV1ToCurrent : IEventUpcaster<OrderPlaced, OrderPlacedV1>
{
    /// <summary>Same honest answer as the hand-written upcaster: warehouse unknown.</summary>
    public static readonly Guid UnknownWarehouseId = Guid.Empty;

    public OrderPlaced Upcast(OrderPlacedV1 source) => new(
        source.Id,
        source.Timestamp,
        source.OrderId,
        source.CustomerId,
        source.TitleId,
        source.Quantity,
        WarehouseId: UnknownWarehouseId,
        CustomerDisplayName: null);
}
// end snippet upcast-v1
