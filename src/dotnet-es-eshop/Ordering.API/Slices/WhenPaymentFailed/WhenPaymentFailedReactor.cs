using LawnDart.Aggregates;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.WhenPaymentFailed;

/// <summary>
/// Subscribes to Payment Failed and issues Cancel Order.
/// </summary>
/// <remarks>
/// Payment failed cancels the order through the shopper or operator cancel command.
/// Pass the order id and the failure detail.
/// Maps to eShop <c>OrderPaymentFailedIntegrationEventHandler</c>, which sends Cancel Order.
/// </remarks>
public sealed class WhenPaymentFailedReactor : IReactor<PaymentFailed>
{
    private readonly IAggregateRepository _orders;
    private readonly ILogger<WhenPaymentFailedReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="orders">Aggregate repository for the order stream.</param>
    /// <param name="logger">Logger used when a payment failure is skipped.</param>
    public WhenPaymentFailedReactor(
        IAggregateRepository orders,
        ILogger<WhenPaymentFailedReactor> logger)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(logger);
        _orders = orders;
        _logger = logger;
    }

    /// <summary>
    /// RFC 4122 version 5 command id for one delivery of Payment Failed.
    /// </summary>
    /// <param name="messageId">Transport message id for this delivery.</param>
    /// <returns>
    /// The version 5 id whose name is <c>payment-failed:{messageId}</c>.
    /// </returns>
    /// <remarks>
    /// The same message id always yields the same command id, so a redelivery is the same command.
    /// </remarks>
    public static Guid CommandIdFor(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return EShopVersion5Id.ForPrefixedName("payment-failed", messageId);
    }

    /// <summary>
    /// Issues Cancel Order for the failed payment.
    /// </summary>
    /// <param name="event">
    /// Payment failed. Ordering should cancel or mark failed.
    /// <see cref="PaymentFailed.FailureReason"/> is the failure detail.
    /// </param>
    /// <param name="context">Transport message context. <see cref="MessageContext.MessageId"/> is the inbox key.</param>
    /// <param name="cancellationToken">Token used to cancel the order load.</param>
    /// <returns>
    /// One <see cref="CancelOrderCommand"/> for <see cref="PaymentFailed.OrderId"/>,
    /// or none when the message is skipped.
    /// The cancel reason is <see cref="PaymentFailed.FailureReason"/>.
    /// </returns>
    /// <remarks>
    /// An empty order id is logged and skipped.
    /// Cancel Order would reject it with <c>ORDER_ID_REQUIRED</c>, and this delivery must not be retried.
    /// An unknown order is logged and skipped.
    /// Cancel Order would reject it with <c>ORDER_NOT_FOUND</c>, and this delivery must not be retried.
    /// A Paid or Shipped order is logged and skipped.
    /// Cancel Order would reject it with <c>CANNOT_CANCEL_ORDER</c>, and this delivery must not be retried.
    /// A delivery with no message id throws. That failure is rethrown so the inbox does not record the message.
    /// A store failure while loading the order is not caught here.
    /// The reactor host rethrows that failure so the transport retries.
    /// Any other status issues Cancel Order. The command id name is <c>payment-failed:{messageId}</c>.
    /// </remarks>
    public async Task<IEnumerable<ICommand>> ReactAsync(
        PaymentFailed @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        if (@event.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Payment Failed {EventId} has an empty order id ({Title}). Skipping.",
                @event.Id,
                OrderAggregate.OrderIdRequiredTitle);
            return [];
        }

        if (string.IsNullOrWhiteSpace(context.MessageId))
        {
            throw new InvalidOperationException(
                $"Payment Failed {@event.Id} has no message id.");
        }

        var order = await _orders
            .GetOrCreateAsync<OrderAggregate>(@event.OrderId, cancellationToken)
            .ConfigureAwait(false);

        if (order.Version == 0)
        {
            _logger.LogWarning(
                "Payment Failed {EventId} for order {OrderId} was rejected: {Message} ({Title}). Skipping.",
                @event.Id,
                @event.OrderId,
                OrderAggregate.OrderNotFoundMessage,
                OrderAggregate.OrderNotFoundTitle);
            return [];
        }

        if (order.State.OrderStatus is OrderAggregate.PaidStatus or OrderAggregate.ShippedStatus)
        {
            _logger.LogWarning(
                "Payment Failed {EventId} for order {OrderId} was rejected: {Message} ({Title}). Skipping.",
                @event.Id,
                @event.OrderId,
                OrderAggregate.CannotCancelOrderMessage,
                OrderAggregate.CannotCancelOrderTitle);
            return [];
        }

        return
        [
            new CancelOrderCommand(
                CommandIdFor(context.MessageId),
                @event.OrderId,
                @event.FailureReason)
        ];
    }
}
