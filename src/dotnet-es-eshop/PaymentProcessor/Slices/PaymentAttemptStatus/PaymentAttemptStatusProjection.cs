using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.Shared.Events;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.PaymentAttemptStatus;

/// <summary>
/// Read model of payment attempt lifecycle for ops/diagnostics.
/// </summary>
[SingleStreamProjection("PaymentAttemptStatus", streamType: "PaymentAttempt", tenantScope: TenantScope.TenantScoped)]
public sealed class PaymentAttemptStatusProjection : ProjectionBase<PaymentAttemptStatusView>
{
    /// <summary>
    /// View-store key for one payment attempt stream.
    /// </summary>
    public const string StorageKey = "PaymentAttemptStatus:v1";

    /// <summary>
    /// View instance id for <paramref name="attemptId"/>.
    /// </summary>
    /// <param name="attemptId">Attempt id.</param>
    /// <returns>The stream id <c>PaymentAttempt:{attemptId}</c>.</returns>
    public static string InstanceId(Guid attemptId) => $"PaymentAttempt:{attemptId:D}";

    /// <summary>
    /// Charge request sent / recorded; waiting on gateway outcome.
    /// </summary>
    /// <param name="requested">Payment Charge Requested.</param>
    /// <remarks>
    /// Sets the attempt id, the order id, the amount, and status <see cref="PaymentAttempt.RequestedStatus"/>.
    /// Gateway reference and failure detail stay empty.
    /// </remarks>
    public void Handle(PaymentChargeRequested requested)
    {
        State.AttemptId = requested.AttemptId;
        State.OrderId = requested.OrderId;
        State.Amount = requested.Amount;
        State.Status = PaymentAttempt.RequestedStatus;
        State.GatewayReference = null;
        State.FailureReason = null;
    }

    /// <summary>
    /// Payment succeeded. Ordering automation When Payment Succeeded subscribes to this event.
    /// </summary>
    /// <param name="succeeded">Payment Succeeded.</param>
    /// <remarks>
    /// Sets status to <see cref="PaymentAttempt.SucceededStatus"/>.
    /// A gateway reference on the event is stored.
    /// A charged amount on the event replaces the requested amount.
    /// The order id stays as it was.
    /// </remarks>
    public void Handle(PaymentSucceeded succeeded)
    {
        State.AttemptId = succeeded.AttemptId;
        State.Status = PaymentAttempt.SucceededStatus;
        if (succeeded.GatewayReference is not null)
            State.GatewayReference = succeeded.GatewayReference;
        if (succeeded.Amount is not null)
            State.Amount = succeeded.Amount.Value;
    }

    /// <summary>
    /// Payment failed. Ordering should cancel or mark the order failed.
    /// </summary>
    /// <param name="failed">Payment Failed.</param>
    /// <remarks>
    /// Sets status to <see cref="PaymentAttempt.FailedStatus"/> and stores the failure detail.
    /// Amount, gateway reference, and order id stay as they were.
    /// </remarks>
    public void Handle(PaymentFailed failed)
    {
        State.AttemptId = failed.AttemptId;
        State.Status = PaymentAttempt.FailedStatus;
        State.FailureReason = failed.FailureReason;
    }
}
