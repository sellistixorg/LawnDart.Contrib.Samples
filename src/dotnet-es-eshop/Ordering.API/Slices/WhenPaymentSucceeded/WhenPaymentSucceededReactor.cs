using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.WhenPaymentSucceeded;

/// <summary>
/// Subscribes to Payment Succeeded from PaymentProcessor and issues Mark Order Paid.
/// </summary>
/// <remarks>
/// Payment succeeded. This automation subscribes to that event and issues Mark Order Paid.
/// Maps to eShop <c>OrderPaymentSucceededIntegrationEventHandler</c>, which sets the order to Paid.
/// </remarks>
public sealed class WhenPaymentSucceededReactor : IReactor<PaymentSucceeded>
{
    private readonly IAggregateRepository _orders;
    private readonly ILogger<WhenPaymentSucceededReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="orders">Aggregate repository for the order stream.</param>
    /// <param name="logger">Logger used when a payment success is skipped.</param>
    public WhenPaymentSucceededReactor(
        IAggregateRepository orders,
        ILogger<WhenPaymentSucceededReactor> logger)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(logger);
        _orders = orders;
        _logger = logger;
    }

    /// <summary>
    /// Issues Mark Order Paid for the order on the payment success.
    /// </summary>
    /// <param name="event">Payment succeeded from PaymentProcessor.</param>
    /// <param name="context">Transport message context. <see cref="MessageContext.MessageId"/> is the inbox key.</param>
    /// <param name="cancellationToken">Token used to cancel the order load.</param>
    /// <returns>
    /// One <see cref="MarkOrderPaidCommand"/> for <see cref="PaymentSucceeded.OrderId"/>,
    /// or none when the message is skipped.
    /// </returns>
    /// <remarks>
    /// An empty order id is logged and skipped.
    /// Mark Order Paid would reject it with <c>ORDER_ID_REQUIRED</c>, and this delivery must not be retried.
    /// A delivery with no message id throws. That failure is rethrown so the inbox does not record the message.
    /// An unknown order is logged and skipped.
    /// Mark Order Paid would reject it with <c>ORDER_NOT_FOUND</c>, and this delivery must not be retried.
    /// The command id name is <c>payment-succeeded:{messageId}</c>.
    /// The same message id is the same command.
    /// Only a StockConfirmed order moves to Paid. Any other status writes no event.
    /// A repeat while the order is already Paid writes no event.
    /// A store failure while loading the order is not caught here.
    /// The reactor host rethrows that failure so the transport retries.
    /// </remarks>
    public async Task<IEnumerable<ICommand>> ReactAsync(
        PaymentSucceeded @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        if (@event.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Payment Succeeded {EventId} has an empty order id ({Title}). Skipping.",
                @event.Id,
                OrderAggregate.OrderIdRequiredTitle);
            return [];
        }

        if (string.IsNullOrWhiteSpace(context.MessageId))
        {
            throw new InvalidOperationException(
                $"Payment Succeeded {@event.Id} has no message id.");
        }

        var order = await _orders
            .GetOrCreateAsync<OrderAggregate>(@event.OrderId, cancellationToken)
            .ConfigureAwait(false);
        if (order.Version == 0)
        {
            _logger.LogWarning(
                "Payment Succeeded {EventId} for order {OrderId} was rejected: {Message} ({Title}). Skipping.",
                @event.Id,
                @event.OrderId,
                OrderAggregate.OrderNotFoundMessage,
                OrderAggregate.OrderNotFoundTitle);
            return [];
        }

        return
        [
            new MarkOrderPaidCommand(
                EShopVersion5Id.ForPrefixedName("payment-succeeded", context.MessageId),
                @event.OrderId)
        ];
    }
}
