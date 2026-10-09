using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.PaymentProcessor.Gateway;

/// <summary>
/// Message from the payment gateway. The charge failed. This is not a domain event.
/// </summary>
/// <remarks>
/// The transport token is <c>payment-gateway-charge-failed</c>.
/// Translate Gateway Failed subscribes to this message and issues Record Payment Failed.
/// The message id is <see cref="MessageIdFor"/> so a second callback for the same attempt is the same message.
/// </remarks>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="Amount">Amount that was not charged.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="GatewayReference">Gateway transaction reference, when the gateway sent one.</param>
/// <param name="FailureReason">Why the charge failed.</param>
[EventTypeName("payment-gateway-charge-failed")]
public sealed record PaymentGatewayChargeFailed(
    Guid AttemptId,
    Guid OrderId,
    decimal Amount,
    string? Currency,
    string? GatewayReference,
    string? FailureReason)
{
    /// <summary>
    /// Transport token <c>payment-gateway-charge-failed</c>.
    /// </summary>
    public const string Token = "payment-gateway-charge-failed";

    /// <summary>
    /// Transport message id for <paramref name="attemptId"/>.
    /// A retry of the same failed charge publishes this same id.
    /// </summary>
    /// <param name="attemptId">Payment attempt id. The gateway dedupes on this id.</param>
    /// <returns>The message id <c>payment-gateway-charge-failed:{attemptId}</c>.</returns>
    public static string MessageIdFor(Guid attemptId) => Token + ":" + attemptId.ToString("D");
}
