using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;

/// <summary>
/// Skips unknown or removed products, checks every remaining line, then handles <see cref="DeductCatalogStockCommand"/>.
/// </summary>
public sealed class DeductCatalogStockHandler : ICommandHandler<DeductCatalogStockCommand>
{
    private readonly IAggregateRepository _items;

    /// <summary>
    /// Creates a handler for the catalog item store.
    /// </summary>
    /// <param name="items">Catalog item aggregate repository.</param>
    public DeductCatalogStockHandler(IAggregateRepository items) => _items = items;

    /// <summary>
    /// Decrements stock for each paid order line.
    /// </summary>
    /// <param name="command">Command that deducts stock for a paid order.</param>
    /// <param name="cancellationToken">Token that cancels the store call.</param>
    /// <returns>A task that completes when every accepted line is stored.</returns>
    /// <remarks>
    /// An unknown or removed product is skipped. Other lines still deduct.
    /// Available stock of 0 is rejected with <c>CATALOG_ITEM_OUT_OF_STOCK</c> before the quantity check.
    /// A line with a quantity of zero or less is rejected before quantities are summed.
    /// Lines for the same item id are combined by summing their quantities.
    /// Every existing item is loaded and checked before any event is appended.
    /// A concurrency or infrastructure failure partway through can still leave a partial result.
    /// </remarks>
    public async Task HandleAsync(
        DeductCatalogStockCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.OrderStockItems is null || command.OrderStockItems.Count == 0)
            throw new DomainException("Order stock items are required.");

        var accepted = new List<OrderStockItem>(command.OrderStockItems.Count);
        var loaded = new Dictionary<Guid, CatalogItem>();
        foreach (var line in command.OrderStockItems)
        {
            if (line.ProductId == Guid.Empty)
                throw new DomainException("Product id is required.");

            if (!loaded.TryGetValue(line.ProductId, out var item))
            {
                item = await _items
                    .GetOrCreateAsync<CatalogItem>(line.ProductId, cancellationToken)
                    .ConfigureAwait(false);
                loaded.Add(line.ProductId, item);
            }

            if (!item.State.Exists || item.State.Removed)
                continue;

            item.EnsureDeductLine(line);
            accepted.Add(line);
        }

        if (accepted.Count == 0)
            return;

        var lines = Combine(accepted);
        var prepared = command with { OrderStockItems = lines };
        foreach (var line in lines)
        {
            await _items
                .HandleCommandAsync(loaded[line.ProductId], prepared, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<OrderStockItem> Combine(IReadOnlyList<OrderStockItem> lines)
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

        var combined = new OrderStockItem[order.Count];
        for (var i = 0; i < order.Count; i++)
        {
            var productId = order[i];
            combined[i] = new OrderStockItem(productId, totals[productId]);
        }

        return combined;
    }
}
