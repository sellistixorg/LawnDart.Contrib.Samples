using LawnDart;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.Shared;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewaySucceeded;

/// <summary>
/// Builds Record Payment Succeeded from a gateway success callback.
/// </summary>
/// <remarks>
/// Receives the gateway success callback, webhook, or simulator result.
/// Issues Record Payment Succeeded.
/// Passes the order id and the payment attempt id.
/// Also passes the gateway reference and the charged amount, which that command records.
/// The callback is not a stored event, so this type is not an <c>IReactor</c>.
/// <see cref="TranslateGatewaySucceededHost"/> subscribes to the callback and calls this type.
/// </remarks>
public sealed class TranslateGatewaySucceededMessageHost
{
    /// <summary>
    /// RFC 4122 version 5 command id for one delivery of the gateway success callback.
    /// </summary>
    /// <param name="messageId">Transport message id for this delivery.</param>
    /// <returns>
    /// The version 5 id whose name is <c>payment-gateway-charge-succeeded:{messageId}</c>.
    /// </returns>
    /// <remarks>
    /// The same message id always yields the same command id, so a redelivery is the same command.
    /// </remarks>
    public static Guid CommandIdFor(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return EShopVersion5Id.ForPrefixedName("payment-gateway-charge-succeeded", messageId);
    }

    /// <summary>
    /// Issues Record Payment Succeeded for the gateway success callback.
    /// </summary>
    /// <param name="callback">
    /// Inbound success signal from the payment gateway.
    /// Upstream: simulated stub (PaymentOptions.PaymentSucceeded = true).
    /// </param>
    /// <param name="context">Transport message context. <see cref="MessageContext.MessageId"/> is the inbox key.</param>
    /// <param name="cancellationToken">Token unused by this handoff.</param>
    /// <returns>
    /// One <see cref="RecordPaymentSucceededCommand"/> for <see cref="PaymentGatewayChargeSucceeded.AttemptId"/>.
    /// The order id is <see cref="PaymentGatewayChargeSucceeded.OrderId"/>.
    /// The gateway reference is <see cref="PaymentGatewayChargeSucceeded.GatewayReference"/>.
    /// The amount is <see cref="PaymentGatewayChargeSucceeded.Amount"/>.
    /// </returns>
    /// <remarks>
    /// A delivery with no message id throws. That failure is rethrown so the inbox does not record the message.
    /// </remarks>
    public Task<IEnumerable<ICommand>> ReactAsync(
        PaymentGatewayChargeSucceeded callback,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.MessageId))
        {
            throw new InvalidOperationException(
                $"Payment gateway charge succeeded for attempt {callback.AttemptId:D} has no message id.");
        }

        return Task.FromResult<IEnumerable<ICommand>>(
        [
            new RecordPaymentSucceededCommand(
                CommandIdFor(context.MessageId),
                callback.AttemptId,
                callback.OrderId,
                callback.GatewayReference,
                callback.Amount)
        ]);
    }
}
