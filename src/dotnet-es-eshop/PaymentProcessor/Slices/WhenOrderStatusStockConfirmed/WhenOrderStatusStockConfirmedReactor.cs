using LawnDart;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.WhenOrderStatusStockConfirmed;

/// <summary>
/// Subscribes to Order Status Stock Confirmed from Ordering and issues Charge Order Payment.
/// </summary>
/// <remarks>
/// Pass order id, amount, and buyer id.
/// The event token is <c>order-status-stock-confirmed</c>.
/// This reactor does not subscribe to Catalog order stock confirmed.
/// </remarks>
public sealed class WhenOrderStatusStockConfirmedReactor : IReactor<OrderStatusStockConfirmed>
{
    private readonly ILogger<WhenOrderStatusStockConfirmedReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="logger">Logger used when a stock-confirmed message is skipped.</param>
    public WhenOrderStatusStockConfirmedReactor(ILogger<WhenOrderStatusStockConfirmedReactor> logger) =>
        _logger = logger;

    /// <summary>
    /// Issues Charge Order Payment for the stock-confirmed order.
    /// </summary>
    /// <param name="event">
    /// Order Status Stock Confirmed from Ordering. The order is ready to charge.
    /// </param>
    /// <param name="context">Transport message context. <see cref="MessageContext.MessageId"/> is the inbox key.</param>
    /// <param name="cancellationToken">Token unused by this handoff.</param>
    /// <returns>
    /// One <see cref="ChargeOrderPaymentCommand"/> for <see cref="OrderStatusStockConfirmed.OrderId"/>,
    /// or none when the message is skipped.
    /// The amount is <see cref="OrderStatusStockConfirmed.OrderTotal"/>.
    /// The buyer id is <see cref="OrderStatusStockConfirmed.BuyerId"/>.
    /// The currency is null. This event has no currency code.
    /// </returns>
    /// <remarks>
    /// Pass order id, amount, buyer id.
    /// An empty order id is logged and skipped.
    /// Charge Order Payment would reject it with <c>PAYMENT_ORDER_ID_REQUIRED</c>, and this delivery must not be retried.
    /// An order total that is not greater than zero is logged and skipped.
    /// Charge Order Payment would reject it with <c>PAYMENT_AMOUNT_INVALID</c>, and this delivery must not be retried.
    /// A delivery with no message id throws. That failure is rethrown so the inbox does not record the message.
    /// The command id name is <c>order-status-stock-confirmed:{messageId}</c>.
    /// The attempt id name is <c>payment-attempt:{messageId}</c>.
    /// The same message id is the same command and the same attempt.
    /// </remarks>
    public Task<IEnumerable<ICommand>> ReactAsync(
        OrderStatusStockConfirmed @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        if (@event.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Order Status Stock Confirmed {EventId} has an empty order id ({Title}). Skipping.",
                @event.Id,
                PaymentAttempt.OrderIdRequiredTitle);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }

        if (@event.OrderTotal <= 0)
        {
            _logger.LogWarning(
                "Order Status Stock Confirmed {EventId} for order {OrderId} was rejected: {Message} ({Title}). Skipping.",
                @event.Id,
                @event.OrderId,
                PaymentAttempt.AmountInvalidMessage,
                PaymentAttempt.AmountInvalidTitle);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }

        if (string.IsNullOrWhiteSpace(context.MessageId))
        {
            throw new InvalidOperationException(
                $"Order Status Stock Confirmed {@event.Id} has no message id.");
        }

        return Task.FromResult<IEnumerable<ICommand>>(
        [
            new ChargeOrderPaymentCommand(
                EShopVersion5Id.ForPrefixedName("order-status-stock-confirmed", context.MessageId),
                EShopVersion5Id.ForPrefixedName("payment-attempt", context.MessageId),
                @event.OrderId,
                @event.OrderTotal,
                null,
                @event.BuyerId)
        ]);
    }
}
