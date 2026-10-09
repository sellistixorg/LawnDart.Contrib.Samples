using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Order paid. Catalog deduct automation subscribes to this event.
/// </summary>
/// <remarks>
/// The token is <c>order-paid</c>. It carries the order id, the buyer id, and each line's product id and units.
/// Ordering appends it on the order stream. Applying it sets the status to Paid.
/// The SQL outbox publishes it on the RabbitMQ transport.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When paid.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="OrderStockItems">Product id + units to deduct.</param>
/// <param name="BuyerId">Buyer id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
[EventTypeName("order-paid")]
public sealed record OrderPaid(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    IReadOnlyList<OrderStockItem> OrderStockItems,
    Guid BuyerId) : IEvent;
