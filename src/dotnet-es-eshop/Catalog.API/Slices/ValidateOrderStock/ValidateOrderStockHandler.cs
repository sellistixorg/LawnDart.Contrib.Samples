using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using StockLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;

/// <summary>
/// Catalog checks availability for order lines. One command with branching outcomes (Confirmed vs Rejected). Preferred over two separate commands. Maps to AwaitingValidation handler.
/// </summary>
public sealed class ValidateOrderStockHandler : ICommandHandler<ValidateOrderStockCommand>
{
    private readonly IAggregateRepository _items;

    /// <summary>
    /// Creates a handler for the catalog store.
    /// </summary>
    /// <param name="items">Aggregate repository for catalog items and the order stock check.</param>
    public ValidateOrderStockHandler(IAggregateRepository items) => _items = items;

    /// <summary>
    /// Checks AvailableStock for each order line; confirms or rejects the order stock check.
    /// </summary>
    /// <param name="command">Validate Order Stock command.</param>
    /// <param name="cancellationToken">Token that cancels the store calls.</param>
    /// <returns>A task that completes when the result is stored, or when this order was already checked.</returns>
    /// <remarks>
    /// Units for the same product are added, then compared with that item's available stock.
    /// Available stock at least equal to the total counts as in stock.
    /// A removed item has Exists set to false. An unknown item has Exists set to false. Both count as no stock.
    /// When every product has stock, the command appends Order Stock Confirmed. Otherwise it appends Order Stock Rejected with one hasStock flag per product.
    /// The handler sets <see cref="ValidateOrderStockCommand.ConfirmedOrderStockItems"/> on the command it stores.
    /// The result is stored on <c>OrderStockValidation:{orderId}</c>. Catalog item streams are not appended. Available stock does not change.
    /// A checked order writes no second event. A repeat does not fail.
    /// The SQL outbox copies the appended integration event in that same write.
    /// </remarks>
    public async Task HandleAsync(
        ValidateOrderStockCommand command,
        CancellationToken cancellationToken = default)
    {
        OrderStockValidation.Require(command);

        var validation = await _items
            .GetOrCreateAsync<OrderStockValidation>(command.OrderId, cancellationToken)
            .ConfigureAwait(false);
        if (validation.State.Checked)
            return;

        var combined = Combine(command.OrderStockItems!);
        var flags = new ConfirmedOrderStockItem[combined.Count];
        for (var i = 0; i < combined.Count; i++)
        {
            var line = combined[i];
            var item = await _items
                .GetOrCreateAsync<CatalogItem>(line.ProductId, cancellationToken)
                .ConfigureAwait(false);
            flags[i] = new ConfirmedOrderStockItem(line.ProductId, HasStock(item, line.Units));
        }

        var prepared = command with { ConfirmedOrderStockItems = flags };
        try
        {
            await _items
                .HandleCommandAsync(validation, prepared, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ConcurrencyException)
        {
            var current = await _items
                .GetOrCreateAsync<OrderStockValidation>(command.OrderId, cancellationToken)
                .ConfigureAwait(false);
            if (current.State.Checked)
                return;

            throw;
        }
    }

    /// <summary>
    /// True when the item exists and available stock is at least <paramref name="units"/>.
    /// </summary>
    /// <param name="item">Catalog item, including an empty stream for an unknown id.</param>
    /// <param name="units">Units requested on the order line.</param>
    /// <returns>False for a removed or unknown item, and when available stock is below <paramref name="units"/>.</returns>
    private static bool HasStock(CatalogItem item, int units) =>
        item.State.Exists && item.State.AvailableStock >= units;

    /// <summary>
    /// Adds units for the same product. First-seen order is kept.
    /// </summary>
    /// <param name="lines">Product id + units requested. Already checked for empty ids and non-positive units.</param>
    /// <returns>One line per product.</returns>
    private static IReadOnlyList<StockLine> Combine(IReadOnlyList<StockLine> lines)
    {
        var totals = new Dictionary<Guid, int>();
        var order = new List<Guid>();
        foreach (var line in lines)
        {
            if (totals.TryGetValue(line.ProductId, out var units))
            {
                totals[line.ProductId] = checked(units + line.Units);
                continue;
            }

            totals.Add(line.ProductId, line.Units);
            order.Add(line.ProductId);
        }

        var combined = new StockLine[order.Count];
        for (var i = 0; i < order.Count; i++)
        {
            var productId = order[i];
            combined[i] = new StockLine(productId, totals[productId]);
        }

        return combined;
    }
}
