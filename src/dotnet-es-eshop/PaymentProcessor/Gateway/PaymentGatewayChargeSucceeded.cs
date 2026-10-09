using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.PaymentProcessor.Gateway;

/// <summary>
/// Message from the payment gateway. The charge succeeded. This is not a domain event.
/// </summary>
/// <remarks>
/// The transport token is <c>payment-gateway-charge-succeeded</c>.
/// Translate Gateway Succeeded subscribes to this message and issues Record Payment Succeeded.
/// The message id is <see cref="MessageIdFor"/> so a second callback for the same attempt is the same message.
/// </remarks>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="Amount">Charged amount.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="GatewayReference">Gateway transaction reference, when the gateway sent one.</param>
[EventTypeName("payment-gateway-charge-succeeded")]
public sealed record PaymentGatewayChargeSucceeded(
    Guid AttemptId,
    Guid OrderId,
    decimal Amount,
    string? Currency,
    string? GatewayReference)
{
    /// <summary>
    /// Transport token <c>payment-gateway-charge-succeeded</c>.
    /// </summary>
    public const string Token = "payment-gateway-charge-succeeded";

    /// <summary>
    /// Transport message id for <paramref name="attemptId"/>.
    /// A retry of the same successful charge publishes this same id.
    /// </summary>
    /// <param name="attemptId">Payment attempt id. The gateway dedupes on this id.</param>
    /// <returns>The message id <c>payment-gateway-charge-succeeded:{attemptId}</c>.</returns>
    public static string MessageIdFor(Guid attemptId) => Token + ":" + attemptId.ToString("D");
}
