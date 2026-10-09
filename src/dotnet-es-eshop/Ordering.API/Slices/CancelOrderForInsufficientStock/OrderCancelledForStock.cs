using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;

/// <summary>
/// Order cancelled due to insufficient stock.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When cancelled.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="RejectedProductIds">Products lacking stock.</param>
/// <param name="OrderStatus">Cancelled.</param>
[EventTypeName("order-cancelled-for-stock")]
public sealed record OrderCancelledForStock(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    IReadOnlyList<Guid> RejectedProductIds,
    string OrderStatus) : IEvent;
