using System.Security.Cryptography;
using System.Text;
using LawnDart;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewayFailed;

/// <summary>
/// Subscribes to the gateway failure callback and runs <see cref="TranslateGatewayFailedReactor"/>.
/// </summary>
/// <remarks>
/// Inbound failure signal from the payment gateway. Upstream: simulated stub (PaymentOptions.PaymentSucceeded = false).
/// The callback is not a stored event, so the assembly scan cannot register an <c>IReactor</c> for it.
/// This hosted service subscribes on the message transport.
/// The inbox key is <c>translate-gateway-failed:{messageId}</c>.
/// A second delivery of that message id is dropped.
/// A missing message id is rethrown so the inbox does not record the message.
/// </remarks>
public sealed class TranslateGatewayFailedSubscription : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IMessageTransport _transport;
    private readonly IInboxStore _inbox;
    private readonly ICommandDispatcher _commands;
    private readonly ILogger<TranslateGatewayFailedSubscription> _logger;

    /// <summary>
    /// Creates the subscription.
    /// </summary>
    /// <param name="scopes">Scope factory. The reactor is resolved inside a new scope for each message.</param>
    /// <param name="transport">Transport that carries <see cref="PaymentGatewayChargeFailed"/>.</param>
    /// <param name="inbox">Inbox that drops a second delivery of the same message id.</param>
    /// <param name="commands">Dispatcher for Record Payment Failed.</param>
    /// <param name="logger">Logger used when the subscription fails.</param>
    public TranslateGatewayFailedSubscription(
        IServiceScopeFactory scopes,
        IMessageTransport transport,
        IInboxStore inbox,
        ICommandDispatcher commands,
        ILogger<TranslateGatewayFailedSubscription> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(logger);
        _scopes = scopes;
        _transport = transport;
        _inbox = inbox;
        _commands = commands;
        _logger = logger;
    }

    /// <summary>
    /// Subscribes to <see cref="PaymentGatewayChargeFailed"/> until the host stops.
    /// </summary>
    /// <param name="stoppingToken">Token that stops the subscription.</param>
    /// <returns>A task that completes when the subscription is registered.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _transport.SubscribeAsync<PaymentGatewayChargeFailed>(HandleAsync, stoppingToken);

    private async Task HandleAsync(
        PaymentGatewayChargeFailed failed,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        var inboxKey = InboxKey(context.MessageId);
        if (inboxKey is not null && await _inbox.IsProcessedAsync(inboxKey, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogDebug(
                "Translate Gateway Failed skipping duplicate message {MessageId}.",
                context.MessageId);
            return;
        }

        try
        {
            List<ICommand> commands;
            await using (var scope = _scopes.CreateAsyncScope())
            {
                var reactor = scope.ServiceProvider.GetRequiredService<TranslateGatewayFailedReactor>();
                commands = (await reactor
                    .ReactAsync(failed, context, cancellationToken)
                    .ConfigureAwait(false)).ToList();
            }
            foreach (var command in commands)
            {
                await _commands
                    .DispatchAsync(command, context.CreateChild(), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (inboxKey is not null)
            {
                await _inbox
                    .MarkProcessedAsync(inboxKey, DateTimeOffset.UtcNow, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Translate Gateway Failed failed while handling attempt {AttemptId} (MessageId={MessageId}).",
                failed.AttemptId,
                context.MessageId);
            throw;
        }
    }

    /// <summary>
    /// Inbox key for <paramref name="messageId"/>.
    /// </summary>
    /// <param name="messageId">Transport message id. Null when the delivery has none.</param>
    /// <returns>
    /// <c>translate-gateway-failed:{messageId}</c>, or a hashed key when that string is longer than 256 characters.
    /// Null when <paramref name="messageId"/> is null.
    /// </returns>
    internal static string? InboxKey(string? messageId)
    {
        if (messageId is null)
            return null;

        var key = "translate-gateway-failed:" + messageId;
        if (key.Length <= 256)
            return key;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(messageId))).ToLowerInvariant();
        return "translate-gateway-failed:" + hash;
    }
}
