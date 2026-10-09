using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;

public sealed partial class CatalogItem
{
    /// <summary>
    /// Decrements stock for each paid order line.
    /// </summary>
    /// <param name="command">Deduct Catalog Stock command.</param>
    /// <remarks>
    /// An unknown or removed item is skipped. No event is appended.
    /// When the item exists, the command must include this catalog item once.
    /// Available stock that is already 0 is rejected with <c>CATALOG_ITEM_OUT_OF_STOCK</c>.
    /// That check runs before a quantity of zero or less is rejected.
    /// Otherwise the command deducts the smaller of the requested units and the available stock.
    /// Available stock does not go below zero.
    /// </remarks>
    public void Handle(DeductCatalogStockCommand command)
    {
        if (!State.Exists || State.Removed)
            return;

        if (command.OrderId == Guid.Empty)
            throw new DomainException("Order id is required.");

        if (command.OrderStockItems is null || command.OrderStockItems.Count == 0)
            throw new DomainException("Order stock items are required.");

        var matches = 0;
        OrderStockItem? line = null;
        foreach (var stockItem in command.OrderStockItems)
        {
            if (stockItem.ProductId != State.ItemId)
                continue;

            matches++;
            line = stockItem;
        }

        if (matches != 1 || line is null)
            throw new DomainException("Order stock items must include this catalog item once.");

        EnsureDeductLine(line);

        var removed = line.Units < State.AvailableStock ? line.Units : State.AvailableStock;

        Apply(new CatalogStockDeducted(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.OrderId,
            [new CatalogStockDeduction(State.ItemId, removed)]));
    }

    /// <summary>
    /// Rejects a deduct line for an item that exists.
    /// </summary>
    /// <param name="line">Product id and units to deduct for this item.</param>
    /// <remarks>
    /// Available stock that is already 0 is rejected with <c>CATALOG_ITEM_OUT_OF_STOCK</c>.
    /// That check runs before a quantity of zero or less is rejected.
    /// The caller skips an unknown or removed item before calling this method.
    /// </remarks>
    internal void EnsureDeductLine(OrderStockItem line)
    {
        if (State.AvailableStock == 0)
            throw new DomainException(OutOfStockMessage, OutOfStockTitle);

        if (line.Units <= 0)
            throw new DomainException("Item units desired should be greater than zero.");
    }
}

public sealed partial class CatalogItemState
{
    internal void Apply(CatalogStockDeducted deducted)
    {
        var removedUnits = 0;
        foreach (var deduction in deducted.Deductions)
        {
            if (deduction.ItemId == ItemId)
                removedUnits += deduction.UnitsRemoved;
        }

        var stock = AvailableStock - removedUnits;
        AvailableStock = stock < 0 ? 0 : stock;
    }
}
