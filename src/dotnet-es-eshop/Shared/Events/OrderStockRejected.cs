using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// One or more lines lack sufficient stock. Ordering cancels the order for the products that had no stock.
/// </summary>
/// <remarks>
/// The token is <c>order-stock-rejected</c>. It carries the order id and each product with a hasStock flag.
/// Catalog appends it on stream <c>OrderStockValidation:{orderId}</c>.
/// The SQL outbox publishes it on the RabbitMQ transport.
/// Ordering's When Catalog Stock Rejected reactor consumes it and issues Cancel Order For Insufficient Stock with the product ids whose hasStock flag is false.
/// </remarks>
/// <param name="Id">Event identifier</param>
/// <param name="Timestamp">When rejected</param>
/// <param name="OrderId">Order id</param>
/// <param name="ConfirmedOrderStockItems">Per-line hasStock flags</param>
[EventTypeName("order-stock-rejected")]
public sealed record OrderStockRejected(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    IReadOnlyList<ConfirmedOrderStockItem> ConfirmedOrderStockItems) : IEvent;
