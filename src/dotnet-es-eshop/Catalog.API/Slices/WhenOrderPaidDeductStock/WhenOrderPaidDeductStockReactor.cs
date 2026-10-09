using LawnDart;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;
using DeductLine = LawnDart.Samples.EShop.Catalog.Commands.OrderStockItem;
using PaidLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.WhenOrderPaidDeductStock;

/// <summary>
/// Subscribes to the Ordering paid signal and issues Deduct Catalog Stock.
/// </summary>
/// <remarks>
/// Pass order id and stock lines.
/// The event token is <c>order-paid</c>.
/// </remarks>
public sealed class WhenOrderPaidDeductStockReactor : IReactor<OrderPaid>
{
    private readonly ICommandDispatcher _dispatcher;
    private readonly ILogger<WhenOrderPaidDeductStockReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="dispatcher">Dispatcher for Deduct Catalog Stock.</param>
    /// <param name="logger">Logger used when Deduct Catalog Stock rejects the order.</param>
    public WhenOrderPaidDeductStockReactor(
        ICommandDispatcher dispatcher,
        ILogger<WhenOrderPaidDeductStockReactor> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// RFC 4122 version 5 command id for one delivery of Order Paid.
    /// </summary>
    /// <param name="messageId">Transport message id for this delivery.</param>
    /// <returns>
    /// The version 5 id whose name is <c>order-paid:{messageId}</c>.
    /// </returns>
    /// <remarks>
    /// The same message id always yields the same command id, so a redelivery is the same command.
    /// </remarks>
    public static Guid CommandIdFor(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return EShopVersion5Id.ForPrefixedName("order-paid", messageId);
    }

    /// <summary>
    /// Issues Deduct Catalog Stock for the paid order.
    /// </summary>
    /// <param name="event">Order paid. Catalog deduct automation subscribes to this event.</param>
    /// <param name="context">Transport message context. <see cref="MessageContext.MessageId"/> is the inbox key.</param>
    /// <param name="cancellationToken">Token that cancels the command dispatch.</param>
    /// <returns>
    /// No commands. The method dispatches <see cref="DeductCatalogStockCommand"/> itself.
    /// Returning the command would deduct again when the host dispatches it.
    /// </returns>
    /// <remarks>
    /// Pass order id and stock lines.
    /// The command id name is <c>order-paid:{messageId}</c>.
    /// A delivery with no message id throws. That failure is rethrown so the inbox does not record the message.
    /// A <see cref="DomainException"/> is logged and this method returns no commands.
    /// Available stock that is already 0 is that rejection, with title <c>CATALOG_ITEM_OUT_OF_STOCK</c>.
    /// A quantity of zero or less is the same kind of rejection.
    /// The host then records the inbox key, so the delivery is not retried.
    /// Any other failure is thrown. The host rethrows it so the transport retries.
    /// An unknown or removed product is skipped by Deduct Catalog Stock.
    /// This method does not record the order id. A different message id for the same order deducts again.
    /// </remarks>
    public async Task<IEnumerable<ICommand>> ReactAsync(
        OrderPaid @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.MessageId))
        {
            throw new InvalidOperationException(
                $"Order Paid {@event.Id} has no message id.");
        }

        var command = new DeductCatalogStockCommand(
            CommandIdFor(context.MessageId),
            @event.OrderId,
            Lines(@event.OrderStockItems));

        try
        {
            await _dispatcher
                .DispatchAsync(command, context.CreateChild(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(
                "Order Paid {EventId} for order {OrderId} was rejected: {Message} ({Title}). Skipping.",
                @event.Id,
                @event.OrderId,
                ex.Message,
                ex.Title);
            return [];
        }

        return [];
    }

    /// <summary>
    /// Product id and units from the paid order.
    /// </summary>
    /// <param name="items">Lines on Order Paid. Empty when the event has none.</param>
    /// <returns>Those lines for Deduct Catalog Stock.</returns>
    private static IReadOnlyList<DeductLine> Lines(IReadOnlyList<PaidLine>? items)
    {
        if (items is null || items.Count == 0)
            return [];

        var lines = new DeductLine[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            lines[i] = new DeductLine(item.ProductId, item.Units);
        }

        return lines;
    }
}
