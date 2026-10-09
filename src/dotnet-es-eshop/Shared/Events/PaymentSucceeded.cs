using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Payment succeeded. Ordering marks the order paid when it receives this event.
/// </summary>
/// <remarks>
/// The token is <c>payment-succeeded</c>. It carries the order id.
/// PaymentProcessor appends it on stream <c>PaymentAttempt:{attemptId}</c>.
/// The SQL outbox publishes it on the RabbitMQ transport.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When succeeded.</param>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="GatewayReference">Gateway reference.</param>
/// <param name="Amount">Charged amount.</param>
[EventTypeName("payment-succeeded")]
public sealed record PaymentSucceeded(
    Guid Id,
    DateTime Timestamp,
    Guid AttemptId,
    Guid OrderId,
    string? GatewayReference,
    decimal? Amount) : IEvent;
