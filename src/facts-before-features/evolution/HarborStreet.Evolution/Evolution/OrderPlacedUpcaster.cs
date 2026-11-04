using HarborStreet.Evolution.Events;
using LawnDart;

namespace HarborStreet.Evolution.Evolution;

/// <summary>
/// Read-time upcast: historical <see cref="OrderPlaced"/> → canonical <see cref="OrderPlacedV2"/>.
// start snippet upcast-order-placed
/// Does not rewrite the event store.
/// </summary>
public static class OrderPlacedUpcaster
{
    /// <summary>Warehouse id used when upcasting pre-warehouse orders.</summary>
    public static readonly Guid UnknownWarehouseId = Guid.Empty;

    public static OrderPlacedV2 ToV2(IEvent @event) => @event switch
    {
        OrderPlacedV2 already => already,
        OrderPlaced v1 => new OrderPlacedV2(
            v1.Id,
            v1.Timestamp,
            v1.OrderId,
            v1.CustomerId,
            v1.TitleId,
            v1.Quantity,
            WarehouseId: UnknownWarehouseId,
            CustomerDisplayName: null),
        _ => throw new InvalidOperationException(
            $"Cannot upcast {@event.GetType().Name} to OrderPlacedV2.")
    };

    public static bool IsOrderPlacedFamily(IEvent @event) =>
        @event is OrderPlaced or OrderPlacedV2;
// end snippet upcast-order-placed
}
