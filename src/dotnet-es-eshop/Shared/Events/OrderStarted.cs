using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Integration signal that ordering started for a buyer. Basket clears the shopper basket on this signal. Distinct from Order Placed (business fact of the new order).
/// </summary>
/// <remarks>
/// The token is <c>order-started</c>. It carries the order id and the buyer id.
/// Ordering appends it on the order stream. The SQL outbox publishes it on the RabbitMQ transport.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When signaled.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="BuyerId">User/buyer id whose basket to clear. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
[EventTypeName("order-started")]
public sealed record OrderStarted(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid BuyerId) : IEvent;
