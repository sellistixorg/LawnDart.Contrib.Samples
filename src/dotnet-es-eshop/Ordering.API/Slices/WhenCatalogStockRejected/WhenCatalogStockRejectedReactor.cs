using LawnDart;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.WhenCatalogStockRejected;

/// <summary>
/// Subscribes to Catalog-owned Order Stock Rejected.
/// </summary>
/// <remarks>
/// Catalog Order Stock Rejected issues Cancel Order For Insufficient Stock.
/// Issues Cancel Order For Insufficient Stock.
/// Maps to eShop <c>OrderStockRejectedIntegrationEventHandler</c>, which cancels the order for insufficient stock.
/// </remarks>
public sealed class WhenCatalogStockRejectedReactor : IReactor<OrderStockRejected>
{
    private readonly ILogger<WhenCatalogStockRejectedReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="logger">Logger used when a rejection is skipped.</param>
    public WhenCatalogStockRejectedReactor(ILogger<WhenCatalogStockRejectedReactor> logger) =>
        _logger = logger;

    /// <summary>
    /// RFC 4122 version 5 command id for one delivery of Order Stock Rejected.
    /// </summary>
    /// <param name="messageId">Transport message id for this delivery.</param>
    /// <returns>
    /// The version 5 id whose name is <c>order-stock-rejected:{messageId}</c>.
    /// </returns>
    /// <remarks>
    /// The same message id always yields the same command id, so a redelivery is the same command.
    /// </remarks>
    public static Guid CommandIdFor(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return EShopVersion5Id.ForPrefixedName("order-stock-rejected", messageId);
    }

    /// <summary>
    /// Issues Cancel Order For Insufficient Stock for the rejected order.
    /// </summary>
    /// <param name="event">
    /// Order Stock Rejected from Catalog. One or more lines lack sufficient stock.
    /// <see cref="OrderStockRejected.ConfirmedOrderStockItems"/> holds the per-line hasStock flags.
    /// </param>
    /// <param name="context">Transport message context. <see cref="MessageContext.MessageId"/> is the inbox key.</param>
    /// <param name="cancellationToken">Token unused by this handoff.</param>
    /// <returns>
    /// One <see cref="CancelOrderForInsufficientStockCommand"/> for <see cref="OrderStockRejected.OrderId"/>,
    /// or none when the order id is empty.
    /// The command's products lacking stock are the product ids whose hasStock flag is false.
    /// </returns>
    /// <remarks>
    /// An empty order id is logged and skipped. Cancel Order For Insufficient Stock would reject it with <c>ORDER_ID_REQUIRED</c>.
    /// A delivery with no message id throws. That failure is rethrown so the inbox does not record the message.
    /// Product ids stay in the order they appear. A product whose hasStock flag is true is left off the command.
    /// When every product shows hasStock true, or the line list is missing, the command still runs with an empty product list.
    /// Cancel Order For Insufficient Stock then cancels an AwaitingValidation order.
    /// Only an AwaitingValidation order moves to Cancelled. Any other status writes no event.
    /// This method does not call the store. A store failure while the host dispatches the command is not caught here.
    /// The reactor host rethrows that failure so the inbox does not record the message.
    /// </remarks>
    public Task<IEnumerable<ICommand>> ReactAsync(
        OrderStockRejected @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        if (@event.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Order Stock Rejected {EventId} has an empty order id. Skipping.",
                @event.Id);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }

        if (string.IsNullOrWhiteSpace(context.MessageId))
        {
            throw new InvalidOperationException(
                $"Order Stock Rejected {@event.Id} has no message id.");
        }

        return Task.FromResult<IEnumerable<ICommand>>(
        [
            new CancelOrderForInsufficientStockCommand(
                CommandIdFor(context.MessageId),
                @event.OrderId,
                ProductsLackingStock(@event))
        ]);
    }

    /// <summary>
    /// Product ids whose hasStock flag is false.
    /// </summary>
    /// <param name="rejected">Order Stock Rejected from Catalog.</param>
    /// <returns>Those product ids, in the order they appear. Empty when none lack stock.</returns>
    private static Guid[] ProductsLackingStock(OrderStockRejected rejected)
    {
        if (rejected.ConfirmedOrderStockItems is not { Count: > 0 } items)
            return [];

        var lacking = new List<Guid>(items.Count);
        foreach (var item in items)
        {
            if (item is { HasStock: false })
                lacking.Add(item.ProductId);
        }

        return lacking.Count == 0 ? [] : lacking.ToArray();
    }
}
