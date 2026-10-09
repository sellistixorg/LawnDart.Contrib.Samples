using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Order status is StockConfirmed. PaymentProcessor charges the order from this event.
/// </summary>
/// <remarks>
/// The token is <c>order-status-stock-confirmed</c>. It carries the order id, the buyer id, the buyer name, and the order total.
/// Ordering appends it on the order stream. Applying it sets the status to StockConfirmed.
/// The SQL outbox publishes it on the RabbitMQ transport.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When confirmed.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="BuyerId">Buyer id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="OrderTotal">Amount to charge.</param>
/// <param name="BuyerName">Buyer display name.</param>
[EventTypeName("order-status-stock-confirmed")]
public sealed record OrderStatusStockConfirmed(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid BuyerId,
    decimal OrderTotal,
    string BuyerName) : IEvent;
