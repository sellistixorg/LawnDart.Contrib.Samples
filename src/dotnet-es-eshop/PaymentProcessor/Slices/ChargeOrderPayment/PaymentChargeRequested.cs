using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;

/// <summary>
/// Charge request sent / recorded; waiting on gateway outcome.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When requested.</param>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Status">Requested.</param>
/// <param name="Currency">Currency code.</param>
[EventTypeName("payment-charge-requested")]
public sealed record PaymentChargeRequested(
    Guid Id,
    DateTime Timestamp,
    Guid AttemptId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string? Currency) : IEvent;
