using LawnDart.Messaging;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Subscribes to the order status messages Ordering publishes.
/// </summary>
/// <remarks>
/// Buyer messages are Order Started, Order Awaiting Validation, Order Status Stock Confirmed, and Order Paid.
/// Each one carries the buyer id. That id is the shopper account id, the sub claim.
/// Order Shipped, Order Cancelled, and Order Cancelled For Stock carry the order id.
/// Those three notify a page only when that page's latest orders read contains the order id.
/// A listener failure is logged and does not stop the subscription.
/// </remarks>
public sealed class OrderStatusSubscriptionHost : BackgroundService
{
    private readonly IMessageTransport _transport;
    private readonly OrderStatusNotificationService _notifications;
    private readonly ILogger<OrderStatusSubscriptionHost> _logger;

    /// <summary>
    /// Creates the host.
    /// </summary>
    /// <param name="transport">Transport that delivers order status messages.</param>
    /// <param name="notifications">Per-buyer listeners on this process.</param>
    /// <param name="logger">Logger for a failed notification.</param>
    public OrderStatusSubscriptionHost(
        IMessageTransport transport,
        OrderStatusNotificationService notifications,
        ILogger<OrderStatusSubscriptionHost> logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(logger);
        _transport = transport;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(
            ListenBuyerAsync<OrderStarted>(message => message.BuyerId, stoppingToken),
            ListenBuyerAsync<OrderAwaitingValidation>(message => message.BuyerId, stoppingToken),
            ListenBuyerAsync<OrderStatusStockConfirmed>(message => message.BuyerId, stoppingToken),
            ListenBuyerAsync<OrderPaid>(message => message.BuyerId, stoppingToken),
            ListenOrderAsync<OrderShippedMessage>(message => message.OrderId, stoppingToken),
            ListenOrderAsync<OrderCancelledMessage>(message => message.OrderId, stoppingToken),
            ListenOrderAsync<OrderCancelledForStockMessage>(message => message.OrderId, stoppingToken));

    private Task ListenBuyerAsync<TMessage>(
        Func<TMessage, Guid> buyerId,
        CancellationToken stoppingToken)
        where TMessage : class =>
        _transport.SubscribeAsync<TMessage>(
            (message, _, _) => NotifyBuyerAsync(buyerId(message)),
            stoppingToken);

    private Task ListenOrderAsync<TMessage>(
        Func<TMessage, Guid> orderId,
        CancellationToken stoppingToken)
        where TMessage : class =>
        _transport.SubscribeAsync<TMessage>(
            (message, _, _) => NotifyOrderAsync(orderId(message)),
            stoppingToken);

    private async Task NotifyBuyerAsync(Guid buyerId)
    {
        if (buyerId == Guid.Empty)
            return;

        try
        {
            await _notifications.NotifyOrderStatusChangedAsync(buyerId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order status notification failed.");
        }
    }

    private async Task NotifyOrderAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            return;

        try
        {
            await _notifications.NotifyListedOrderAsync(orderId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order status notification failed.");
        }
    }
}
