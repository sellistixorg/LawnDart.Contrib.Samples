using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;

/// <summary>
/// Order was shipped.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When shipped.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="OrderStatus">Shipped.</param>
/// <param name="Description">The order was shipped.</param>
[EventTypeName("order-shipped")]
public sealed record OrderShipped(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    string OrderStatus,
    string? Description) : IEvent;
