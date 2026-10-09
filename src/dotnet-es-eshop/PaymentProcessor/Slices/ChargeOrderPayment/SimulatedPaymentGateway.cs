using LawnDart.Messaging;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;

/// <summary>
/// Outbound charge request to the payment gateway. Upstream this is a simulated stub driven by PaymentOptions.PaymentSucceeded.
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    /// <summary>
    /// Why the charge failed.
    /// </summary>
    public const string DeclinedReason = "Payment was declined.";

    private readonly PaymentOptions _options;
    private readonly IMessageTransport _transport;

    /// <summary>
    /// Creates the stub.
    /// </summary>
    /// <param name="options">PaymentSucceeded flag. Defaults to true when the section is missing.</param>
    /// <param name="transport">Transport that publishes the gateway callback.</param>
    public SimulatedPaymentGateway(IOptions<PaymentOptions> options, IMessageTransport transport)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transport);
        _options = options.Value;
        _transport = transport;
    }

    /// <summary>
    /// Charges the attempt and returns success or failure from <see cref="PaymentOptions.PaymentSucceeded"/>.
    /// </summary>
    /// <param name="request">The charge to send.</param>
    /// <param name="cancellationToken">Token used to cancel the charge.</param>
    /// <returns>
    /// <see cref="PaymentGatewaySucceeded"/> when the flag is true.
    /// <see cref="PaymentGatewayFailed"/> when the flag is false.
    /// </returns>
    /// <remarks>
    /// After choosing the result, the stub publishes <see cref="PaymentGatewayChargeSucceeded"/> or
    /// <see cref="PaymentGatewayChargeFailed"/> on the message transport. That callback is the gateway webhook.
    /// The return value is unchanged. The attempt stream is not appended.
    /// The message id is stable for the attempt and the outcome, so a retry is the same message.
    /// </remarks>
    public async Task<PaymentGatewayResult> ChargeAsync(
        PaymentChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_options.PaymentSucceeded)
        {
            var callback = new PaymentGatewayChargeSucceeded(
                request.AttemptId,
                request.OrderId,
                request.Amount,
                request.Currency,
                null);
            await PublishAsync(callback, PaymentGatewayChargeSucceeded.MessageIdFor(request.AttemptId), cancellationToken)
                .ConfigureAwait(false);
            return new PaymentGatewaySucceeded(request.AttemptId, request.OrderId, null, request.Amount);
        }

        var failed = new PaymentGatewayChargeFailed(
            request.AttemptId,
            request.OrderId,
            request.Amount,
            request.Currency,
            null,
            DeclinedReason);
        await PublishAsync(failed, PaymentGatewayChargeFailed.MessageIdFor(request.AttemptId), cancellationToken)
            .ConfigureAwait(false);
        return new PaymentGatewayFailed(request.AttemptId, request.OrderId, DeclinedReason);
    }

    private Task PublishAsync<TMessage>(TMessage message, string messageId, CancellationToken cancellationToken)
        where TMessage : class
    {
        var context = new MessageContext
        {
            MessageId = messageId,
            CorrelationId = messageId,
            EnqueuedAt = DateTimeOffset.UtcNow
        };
        return _transport.PublishAsync(message, context, cancellationToken);
    }
}
