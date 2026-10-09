using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;

/// <summary>
/// One charge attempt for an order. Stream key: payment-attempt:{attemptId}. eShop simulates success/fail via config; LawnDart models a real gateway intent.
/// </summary>
public sealed partial class PaymentAttempt : AggregateRoot<PaymentAttemptState>
{
    /// <summary>
    /// Requested. Charge request sent and waiting on the gateway outcome.
    /// </summary>
    public const string RequestedStatus = "Requested";

    /// <summary>
    /// Succeeded.
    /// </summary>
    public const string SucceededStatus = "Succeeded";

    /// <summary>
    /// Failed.
    /// </summary>
    public const string FailedStatus = "Failed";

    /// <inheritdoc />
    protected override void ApplyEventToState(IEvent @event) => EShopStateApplier.Apply(State, @event);
}
