using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;

public sealed partial class CatalogItem
{
    /// <summary>
    /// Removes a catalog item from sale.
    /// </summary>
    /// <param name="command">Command that removes the item.</param>
    /// <remarks>
    /// The catalog item must already exist. An unknown or removed item is rejected with
    /// <c>CATALOG_ITEM_NOT_FOUND</c>.
    /// The command appends Catalog Item Removed. Exists becomes false and Removed becomes true.
    /// The item is no longer present.
    /// </remarks>
    public void Handle(RemoveCatalogItemCommand command)
    {
        if (command.ItemId == Guid.Empty)
            throw new DomainException("Item id is required.");

        if (!State.Exists)
            throw new DomainException(NotFoundMessage, NotFoundTitle);

        Apply(new CatalogItemRemoved(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.ItemId));
    }
}

public sealed partial class CatalogItemState
{
    internal void Apply(CatalogItemRemoved removed)
    {
        _ = removed;
        Exists = false;
        Removed = true;
    }
}
