using System.Security.Cryptography;
using System.Text;
using LawnDart;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewaySucceeded;

/// <summary>
/// Subscribes to the payment gateway success callback and issues Record Payment Succeeded.
/// </summary>
/// <remarks>
/// The callback is <see cref="PaymentGatewayChargeSucceeded"/>.
/// The transport token is <c>payment-gateway-charge-succeeded</c>.
/// The callback has no event id and no timestamp.
/// <see cref="TranslateGatewaySucceededModule"/> registers this host.
/// <c>AddEShopSlices</c> finds that module.
/// A Record Payment Succeeded rejection is logged and the inbox records the message, so the delivery is not retried.
/// An attempt that is already Succeeded or Failed writes no event.
/// A delivery with no message id throws. This host logs that failure and rethrows it. The inbox does not record the message.
/// </remarks>
public sealed class TranslateGatewaySucceededHost : BackgroundService
{
    private readonly TranslateGatewaySucceededMessageHost _messages;
    private readonly IMessageTransport _transport;
    private readonly IInboxStore _inbox;
    private readonly ICommandDispatcher _dispatcher;
    private readonly ILogger<TranslateGatewaySucceededHost> _logger;

    /// <summary>
    /// Creates the subscriber.
    /// </summary>
    /// <param name="messages">Builds Record Payment Succeeded from the callback.</param>
    /// <param name="transport">Transport that delivers <see cref="PaymentGatewayChargeSucceeded"/>.</param>
    /// <param name="inbox">Inbox that drops a repeat of the same message id.</param>
    /// <param name="dispatcher">Dispatcher for Record Payment Succeeded.</param>
    /// <param name="logger">Logger used when Record Payment Succeeded rejects the callback.</param>
    public TranslateGatewaySucceededHost(
        TranslateGatewaySucceededMessageHost messages,
        IMessageTransport transport,
        IInboxStore inbox,
        ICommandDispatcher dispatcher,
        ILogger<TranslateGatewaySucceededHost> logger)
    {
        _messages = messages;
        _transport = transport;
        _inbox = inbox;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Subscribes to <see cref="PaymentGatewayChargeSucceeded"/> until the host stops.
    /// </summary>
    /// <param name="stoppingToken">Token that cancels the subscription.</param>
    /// <returns>A task that completes when the subscription ends.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _transport.SubscribeAsync<PaymentGatewayChargeSucceeded>(HandleAsync, stoppingToken);

    private async Task HandleAsync(
        PaymentGatewayChargeSucceeded callback,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var commands = (await _messages
                .ReactAsync(callback, context, cancellationToken)
                .ConfigureAwait(false)).ToList();

            var inboxKey = TranslateGatewaySucceededInbox.Key(context.MessageId!);
            if (await _inbox.IsProcessedAsync(inboxKey, cancellationToken).ConfigureAwait(false))
                return;

            try
            {
                foreach (var command in commands)
                {
                    await _dispatcher
                        .DispatchAsync(command, context.CreateChild(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(
                    "Payment gateway charge succeeded for attempt {AttemptId} was rejected: {Message} ({Title}). Skipping.",
                    callback.AttemptId,
                    ex.Message,
                    ex.Title);
                await _inbox
                    .MarkProcessedAsync(inboxKey, DateTimeOffset.UtcNow, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await _inbox
                .MarkProcessedAsync(inboxKey, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Translate Gateway Succeeded failed while handling attempt {AttemptId} (MessageId={MessageId}).",
                callback.AttemptId,
                context.MessageId);
            throw;
        }
    }
}

file static class TranslateGatewaySucceededInbox
{
    private const int MaxKeyLength = 256;

    public static string Key(string messageId)
    {
        var key = "translate-gateway-succeeded:" + messageId;
        if (key.Length <= MaxKeyLength)
            return key;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(messageId));
        return "translate-gateway-succeeded:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
