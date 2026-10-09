using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Order awaiting stock validation. Catalog validates stock on this signal.
/// </summary>
/// <remarks>
/// The token is <c>order-awaiting-validation</c>. It carries the order id, the buyer id, and each line's product id and units.
/// Ordering appends it on the order stream. Applying it sets the status to AwaitingValidation.
/// The SQL outbox publishes it on the RabbitMQ transport.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When marked.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="OrderStockItems">Product id + units to validate.</param>
/// <param name="BuyerId">Buyer id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
[EventTypeName("order-awaiting-validation")]
public sealed record OrderAwaitingValidation(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    IReadOnlyList<OrderStockItem> OrderStockItems,
    Guid BuyerId) : IEvent;
