using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Order Shipped as the storefront reads it from the bus.
/// </summary>
/// <remarks>
/// The token is <c>order-shipped</c>. Ordering publishes a wider event with the same token.
/// This record keeps the id, the time, and the order id. Extra JSON fields are ignored.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When shipped.</param>
/// <param name="OrderId">Order id.</param>
[EventTypeName("order-shipped")]
public sealed record OrderShippedMessage(Guid Id, DateTime Timestamp, Guid OrderId);

/// <summary>
/// Order Cancelled as the storefront reads it from the bus.
/// </summary>
/// <remarks>
/// The token is <c>order-cancelled</c>. Shopper cancel and the payment-failed reactor append this event.
/// This record keeps the id, the time, and the order id. Extra JSON fields are ignored.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When cancelled.</param>
/// <param name="OrderId">Order id.</param>
[EventTypeName("order-cancelled")]
public sealed record OrderCancelledMessage(Guid Id, DateTime Timestamp, Guid OrderId);

/// <summary>
/// Order Cancelled For Stock as the storefront reads it from the bus.
/// </summary>
/// <remarks>
/// The token is <c>order-cancelled-for-stock</c>. The stock-rejected reactor appends this event.
/// This record keeps the id, the time, and the order id. Extra JSON fields are ignored.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When cancelled.</param>
/// <param name="OrderId">Order id.</param>
[EventTypeName("order-cancelled-for-stock")]
public sealed record OrderCancelledForStockMessage(Guid Id, DateTime Timestamp, Guid OrderId);
