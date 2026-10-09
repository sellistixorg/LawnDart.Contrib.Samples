using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;

/// <summary>
/// Order cancelled by shopper or operator.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When cancelled.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="OrderStatus">Cancelled.</param>
/// <param name="Reason">Cancel reason.</param>
[EventTypeName("order-cancelled")]
public sealed record OrderCancelled(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    string OrderStatus,
    string? Reason) : IEvent;
