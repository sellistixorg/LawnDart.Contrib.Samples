using LawnDart;
using LawnDart.Samples.EShop.Catalog.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;

public sealed partial class CatalogItem
{
    /// <summary>
    /// Creates a new catalog item with price, brand, type, and initial stock.
    /// </summary>
    /// <param name="command">Add catalog item command.</param>
    /// <remarks>
    /// An item with this item id was already added. Add Catalog Item rejects the duplicate; re-running the seed must check first.
    /// An item id whose stream has any prior events, including a removed item, is rejected with
    /// <c>CATALOG_ITEM_ALREADY_EXISTS</c>. Removal is final.
    /// </remarks>
    public void Handle(AddCatalogItemCommand command)
    {
        if (command.ItemId == Guid.Empty)
            throw new DomainException("Item id is required.");

        var name = command.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new DomainException("Item name is required.");

        if (command.BrandId == Guid.Empty)
            throw new DomainException("Brand id is required.");

        if (command.TypeId == Guid.Empty)
            throw new DomainException("Type id is required.");

        if (command.Price < 0)
            throw new DomainException("Price cannot be negative.");

        if (command.AvailableStock < 0)
            throw new DomainException("Available stock cannot be negative.");

        if (command.RestockThreshold is < 0)
            throw new DomainException("Restock threshold cannot be negative.");

        if (command.MaxStockThreshold is < 0)
            throw new DomainException("Max stock threshold cannot be negative.");

        if (command.RestockThreshold is int restock
            && command.MaxStockThreshold is int max
            && restock > max)
        {
            throw new DomainException("Restock threshold cannot exceed max stock threshold.");
        }

        if (command.MaxStockThreshold is int maxStock && command.AvailableStock > maxStock)
            throw new DomainException("Available stock cannot exceed max stock threshold.");

        if (Version > 0 || State.Exists || State.Removed)
            throw new DomainException(AlreadyExistsMessage, AlreadyExistsTitle);

        Apply(new CatalogItemAdded(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.ItemId,
            name,
            BlankToNull(command.Description),
            command.Price,
            BlankToNull(command.PictureFileName),
            command.BrandId,
            command.TypeId,
            command.AvailableStock,
            command.RestockThreshold,
            command.MaxStockThreshold));
    }
}

public sealed partial class CatalogItemState
{
    internal void Apply(CatalogItemAdded added)
    {
        ItemId = added.ItemId;
        Name = added.Name;
        Description = added.Description;
        Price = added.Price;
        PictureFileName = added.PictureFileName;
        BrandId = added.BrandId;
        TypeId = added.TypeId;
        AvailableStock = added.AvailableStock;
        RestockThreshold = added.RestockThreshold;
        MaxStockThreshold = added.MaxStockThreshold;
        OnReorder = false;
        Exists = true;
    }
}
