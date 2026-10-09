using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Payment failed. Ordering cancels the order when it receives this event.
/// </summary>
/// <remarks>
/// The token is <c>payment-failed</c>. It carries the order id, the payment attempt id, and the failure detail.
/// PaymentProcessor appends it on stream <c>PaymentAttempt:{attemptId}</c>.
/// The SQL outbox publishes it on the RabbitMQ transport.
/// Ordering's When Payment Failed subscribes to this token and cancels the order.
/// eShop's <c>OrderPaymentFailedIntegrationEvent</c> carries the order id. This event carries that order id too.
/// </remarks>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When failed.</param>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="FailureReason">Failure detail.</param>
[EventTypeName("payment-failed")]
public sealed record PaymentFailed(
    Guid Id,
    DateTime Timestamp,
    Guid AttemptId,
    Guid OrderId,
    string? FailureReason) : IEvent;
