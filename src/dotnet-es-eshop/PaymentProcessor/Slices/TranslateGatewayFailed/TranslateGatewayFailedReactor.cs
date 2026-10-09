using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.Shared;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewayFailed;

/// <summary>
/// Adapter: translates the gateway failure signal into the Record Payment Failed command.
/// </summary>
/// <remarks>
/// Inbound failure signal from the payment gateway. Upstream: simulated stub (PaymentOptions.PaymentSucceeded = false).
/// Receives the gateway failure (callback, webhook, or simulator result) and issues Record Payment Failed.
/// Passes the order id, the payment attempt id, and why the charge failed.
/// The callback is not a stored event, so this type is not an <c>IReactor</c>.
/// <see cref="TranslateGatewayFailedSubscription"/> subscribes to <see cref="PaymentGatewayChargeFailed"/>.
/// </remarks>
public sealed class TranslateGatewayFailedReactor
{
    private readonly IAggregateRepository _attempts;
    private readonly ILogger<TranslateGatewayFailedReactor> _logger;

    /// <summary>
    /// Creates the adapter.
    /// </summary>
    /// <param name="attempts">Aggregate repository for the payment attempt stream.</param>
    /// <param name="logger">Logger used when a gateway failure is skipped.</param>
    public TranslateGatewayFailedReactor(
        IAggregateRepository attempts,
        ILogger<TranslateGatewayFailedReactor> logger)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(logger);
        _attempts = attempts;
        _logger = logger;
    }

    /// <summary>
    /// RFC 4122 version 5 command id for one delivery of the gateway failure.
    /// </summary>
    /// <param name="messageId">Transport message id for this delivery.</param>
    /// <returns>
    /// The version 5 id whose name is <c>payment-gateway-charge-failed:{messageId}</c>.
    /// </returns>
    /// <remarks>
    /// The same message id always yields the same command id, so a redelivery is the same command.
    /// </remarks>
    public static Guid CommandIdFor(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return EShopVersion5Id.ForPrefixedName(PaymentGatewayChargeFailed.Token, messageId);
    }

    /// <summary>
    /// Issues Record Payment Failed for the failed charge.
    /// </summary>
    /// <param name="failed">
    /// Inbound failure signal from the payment gateway.
    /// Upstream: simulated stub (PaymentOptions.PaymentSucceeded = false).
    /// </param>
    /// <param name="context">Transport message context. <see cref="MessageContext.MessageId"/> is the inbox key.</param>
    /// <param name="cancellationToken">Token used to cancel the attempt load.</param>
    /// <returns>
    /// One <see cref="RecordPaymentFailedCommand"/> for <see cref="PaymentGatewayChargeFailed.AttemptId"/>,
    /// or none when the message is skipped.
    /// The order id is <see cref="PaymentGatewayChargeFailed.OrderId"/>.
    /// The failure reason is <see cref="PaymentGatewayChargeFailed.FailureReason"/>.
    /// </returns>
    /// <remarks>
    /// An empty attempt id is logged and skipped.
    /// Record Payment Failed would reject it with <c>PAYMENT_ATTEMPT_ID_REQUIRED</c>, and this delivery must not be retried.
    /// An empty order id is logged and skipped.
    /// Record Payment Failed would reject it with <c>PAYMENT_ORDER_ID_REQUIRED</c>, and this delivery must not be retried.
    /// A delivery with no message id throws. That failure is rethrown so the inbox does not record the message.
    /// An attempt with no Payment Charge Requested is logged and skipped.
    /// Record Payment Failed would reject it with <c>PAYMENT_ATTEMPT_NOT_FOUND</c>, and this delivery must not be retried.
    /// An attempt whose status is Succeeded or Failed is logged and skipped.
    /// Record Payment Failed writes nothing for that status.
    /// An order id that does not match the attempt is logged and skipped.
    /// Record Payment Failed would reject it with <c>PAYMENT_ORDER_ID_MISMATCH</c>, and this delivery must not be retried.
    /// A store failure while loading the attempt is not caught here.
    /// The subscription rethrows that failure so the inbox does not record the message.
    /// </remarks>
    public async Task<IEnumerable<ICommand>> ReactAsync(
        PaymentGatewayChargeFailed failed,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(context);

        if (failed.AttemptId == Guid.Empty)
        {
            _logger.LogWarning(
                "Payment Gateway Charge Failed has an empty attempt id ({Title}). Skipping.",
                PaymentAttempt.AttemptIdRequiredTitle);
            return [];
        }

        if (failed.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Payment Gateway Charge Failed for attempt {AttemptId} has an empty order id ({Title}). Skipping.",
                failed.AttemptId,
                PaymentAttempt.OrderIdRequiredTitle);
            return [];
        }

        if (string.IsNullOrWhiteSpace(context.MessageId))
        {
            throw new InvalidOperationException(
                $"Payment Gateway Charge Failed for attempt {failed.AttemptId:D} has no message id.");
        }

        var attempt = await _attempts
            .GetOrCreateAsync<PaymentAttempt>(failed.AttemptId, cancellationToken)
            .ConfigureAwait(false);

        if (attempt.Version == 0)
        {
            _logger.LogWarning(
                "Payment Gateway Charge Failed for attempt {AttemptId} was rejected: {Message} ({Title}). Skipping.",
                failed.AttemptId,
                PaymentAttempt.AttemptNotFoundMessage,
                PaymentAttempt.AttemptNotFoundTitle);
            return [];
        }

        if (attempt.State.Status is PaymentAttempt.SucceededStatus or PaymentAttempt.FailedStatus)
        {
            _logger.LogWarning(
                "Payment Gateway Charge Failed for attempt {AttemptId} is already {Status}. Skipping.",
                failed.AttemptId,
                attempt.State.Status);
            return [];
        }

        if (failed.OrderId != attempt.State.OrderId)
        {
            _logger.LogWarning(
                "Payment Gateway Charge Failed for attempt {AttemptId} was rejected: {Message} ({Title}). Skipping.",
                failed.AttemptId,
                PaymentAttempt.OrderMismatchMessage,
                PaymentAttempt.OrderMismatchTitle);
            return [];
        }

        return
        [
            new RecordPaymentFailedCommand(
                CommandIdFor(context.MessageId),
                failed.AttemptId,
                failed.OrderId,
                failed.FailureReason)
        ];
    }
}
