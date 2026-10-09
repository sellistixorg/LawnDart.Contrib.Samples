using LawnDart;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.WhenCatalogStockConfirmed;

/// <summary>
/// Subscribes to Order Stock Confirmed from Catalog and issues Mark Order Stock Confirmed.
/// </summary>
/// <remarks>
/// Maps to eShop <c>OrderStockConfirmedIntegrationEventHandler</c>, which sets the order to StockConfirmed.
/// </remarks>
public sealed class WhenCatalogStockConfirmedReactor : IReactor<OrderStockConfirmed>
{
    private readonly ILogger<WhenCatalogStockConfirmedReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="logger">Logger used when a confirmation is skipped.</param>
    public WhenCatalogStockConfirmedReactor(ILogger<WhenCatalogStockConfirmedReactor> logger) =>
        _logger = logger;

    /// <summary>
    /// RFC 4122 version 5 command id for one delivery of Order Stock Confirmed.
    /// </summary>
    /// <param name="messageId">Transport message id for this delivery.</param>
    /// <returns>
    /// The version 5 id whose name is <c>order-stock-confirmed:{messageId}</c>.
    /// </returns>
    /// <remarks>
    /// The same message id always yields the same command id, so a redelivery is the same command.
    /// </remarks>
    public static Guid CommandIdFor(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return EShopVersion5Id.ForPrefixedName("order-stock-confirmed", messageId);
    }

    /// <summary>
    /// Issues Mark Order Stock Confirmed for the confirmed order.
    /// </summary>
    /// <param name="event">Order Stock Confirmed from Catalog. All requested units are available for the order.</param>
    /// <param name="context">Transport message context. <see cref="MessageContext.MessageId"/> is the inbox key.</param>
    /// <param name="cancellationToken">Token unused by this handoff.</param>
    /// <returns>
    /// One <see cref="MarkOrderStockConfirmedCommand"/> for <see cref="OrderStockConfirmed.OrderId"/>,
    /// or none when the order id is empty.
    /// </returns>
    /// <remarks>
    /// An empty order id is logged and skipped. Mark Order Stock Confirmed would reject it with <c>ORDER_ID_REQUIRED</c>.
    /// A delivery with no message id throws. That failure is rethrown so the inbox does not record the message.
    /// Only an AwaitingValidation order moves to StockConfirmed. Any other status writes no event.
    /// </remarks>
    public Task<IEnumerable<ICommand>> ReactAsync(
        OrderStockConfirmed @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        if (@event.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Order Stock Confirmed {EventId} has an empty order id. Skipping.",
                @event.Id);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }

        if (string.IsNullOrWhiteSpace(context.MessageId))
        {
            throw new InvalidOperationException(
                $"Order Stock Confirmed {@event.Id} has no message id.");
        }

        return Task.FromResult<IEnumerable<ICommand>>(
            [new MarkOrderStockConfirmedCommand(CommandIdFor(context.MessageId), @event.OrderId)]);
    }
}
