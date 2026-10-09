using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;

public sealed partial class CatalogItem
{
    /// <summary>
    /// Adds units to available stock up to MaxStockThreshold.
    /// </summary>
    /// <param name="command">Command that restocks the item.</param>
    /// <remarks>
    /// The catalog item must already exist. An unknown or removed item is rejected with
    /// <c>CATALOG_ITEM_NOT_FOUND</c>. Removal sets Exists to false and Removed to true, and is final.
    /// When MaxStockThreshold is empty, there is no cap. When it is set, QuantityAdded is the units that fit.
    /// Restock sets OnReorder to false.
    /// A negative quantity is rejected.
    /// </remarks>
    public void Handle(RestockCatalogItemCommand command)
    {
        if (command.ItemId == Guid.Empty)
            throw new DomainException("Item id is required.");

        if (!State.Exists)
            throw new DomainException(NotFoundMessage, NotFoundTitle);

        if (command.Quantity < 0)
            throw new DomainException("Quantity cannot be negative.");

        var quantityAdded = command.Quantity;
        if (State.MaxStockThreshold is int max)
        {
            var room = max - State.AvailableStock;
            if (room < 0)
                room = 0;
            if (quantityAdded > room)
                quantityAdded = room;
        }

        Apply(new CatalogItemRestocked(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.ItemId,
            quantityAdded,
            State.AvailableStock + quantityAdded));
    }
}

public sealed partial class CatalogItemState
{
    internal void Apply(CatalogItemRestocked restocked)
    {
        ItemId = restocked.ItemId;
        AvailableStock = restocked.AvailableStock;
        OnReorder = false;
    }
}
