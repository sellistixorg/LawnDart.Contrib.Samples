using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// All requested units are available for the order. Ordering marks the order StockConfirmed on this signal.
/// </summary>
/// <remarks>
/// The token is <c>order-stock-confirmed</c>. It carries the order id.
/// Catalog appends it on stream <c>OrderStockValidation:{orderId}</c>.
/// The SQL outbox publishes it on the RabbitMQ transport.
/// </remarks>
/// <param name="Id">Event identifier</param>
/// <param name="Timestamp">When confirmed</param>
/// <param name="OrderId">Order id</param>
[EventTypeName("order-stock-confirmed")]
public sealed record OrderStockConfirmed(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId) : IEvent;
