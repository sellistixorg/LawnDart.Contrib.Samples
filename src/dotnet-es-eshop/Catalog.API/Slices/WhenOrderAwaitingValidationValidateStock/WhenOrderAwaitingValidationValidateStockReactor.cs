using LawnDart;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;
using StockLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.WhenOrderAwaitingValidationValidateStock;

/// <summary>
/// On Order Awaiting Validation, issues Validate Order Stock with the order id and each line's product id and units.
/// </summary>
public sealed class WhenOrderAwaitingValidationValidateStockReactor : IReactor<OrderAwaitingValidation>
{
    private readonly ILogger<WhenOrderAwaitingValidationValidateStockReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="logger">Logger used when an Order Awaiting Validation message is skipped.</param>
    public WhenOrderAwaitingValidationValidateStockReactor(
        ILogger<WhenOrderAwaitingValidationValidateStockReactor> logger) =>
        _logger = logger;

    /// <summary>
    /// Command identifier for <paramref name="orderId"/>.
    /// A redelivery of the same order uses this same id.
    /// </summary>
    /// <param name="orderId">Order being validated.</param>
    /// <returns>A version 5 id from the order id.</returns>
    public static Guid CommandIdFor(Guid orderId) =>
        EShopVersion5Id.ForPrefixedName("validate-order-stock", orderId.ToString("D"));

    /// <summary>
    /// Subscribes to Ordering awaiting-validation signal; issues Validate Order Stock.
    /// </summary>
    /// <param name="event">Order Awaiting Validation from Ordering.</param>
    /// <param name="context">Transport message context.</param>
    /// <param name="cancellationToken">Token unused by this handoff.</param>
    /// <returns>
    /// One <see cref="ValidateOrderStockCommand"/> for <see cref="OrderAwaitingValidation.OrderId"/>,
    /// or none when the message is skipped.
    /// </returns>
    /// <remarks>
    /// Pass order id and stock lines.
    /// An empty order id is logged and skipped.
    /// An empty line list is logged and skipped.
    /// Invalid stock input is logged and skipped before the command is issued. The order stays in AwaitingValidation.
    /// A repeat of the same order uses <see cref="CommandIdFor"/>. Validate Order Stock keeps the first result.
    /// This method does not call the store. A store failure while the host dispatches the command is not caught here.
    /// The reactor host rethrows that failure so the transport retries.
    /// </remarks>
    public Task<IEnumerable<ICommand>> ReactAsync(
        OrderAwaitingValidation @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        if (@event.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Order Awaiting Validation {EventId} has an empty order id. Skipping.",
                @event.Id);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }

        if (@event.OrderStockItems is null || @event.OrderStockItems.Count == 0)
        {
            _logger.LogWarning(
                "Order Awaiting Validation {EventId} has an empty line list. Skipping.",
                @event.Id);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }

        var lines = new StockLine[@event.OrderStockItems.Count];
        for (var i = 0; i < lines.Length; i++)
            lines[i] = @event.OrderStockItems[i];

        var command = new ValidateOrderStockCommand(
            CommandIdFor(@event.OrderId),
            @event.OrderId,
            lines);
        try
        {
            OrderStockValidation.Require(command);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(
                ex,
                "Reactor skipped invalid stock input on Order Awaiting Validation {EventId} for order {OrderId} ({Title}). The order stays in AwaitingValidation.",
                @event.Id,
                @event.OrderId,
                ex.Title);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }

        return Task.FromResult<IEnumerable<ICommand>>([command]);
    }
}
