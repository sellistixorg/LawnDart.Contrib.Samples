using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;

public sealed partial class CatalogItem
{
    /// <summary>
    /// Replaces catalog item details; emits Price Changed when price differs.
    /// </summary>
    /// <param name="command">Command that replaces the item details.</param>
    /// <remarks>
    /// The catalog item must already exist. An unknown or removed item is rejected with
    /// <c>CATALOG_ITEM_NOT_FOUND</c>.
    /// When the unit price differs, the command also appends Catalog Item Price Changed.
    /// </remarks>
    public void Handle(UpdateCatalogItemCommand command)
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

        if (!State.Exists)
            throw new DomainException(NotFoundMessage, NotFoundTitle);

        var oldPrice = State.Price;
        var priceChanged = command.Price != oldPrice;

        Apply(new CatalogItemUpdated(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.ItemId,
            name,
            BlankToNull(command.Description),
            command.Price,
            command.BrandId,
            command.TypeId,
            BlankToNull(command.PictureFileName)));

        if (!priceChanged)
            return;

        Apply(new CatalogItemPriceChanged(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.ItemId,
            oldPrice,
            command.Price));
    }
}

public sealed partial class CatalogItemState
{
    internal void Apply(CatalogItemUpdated updated)
    {
        ItemId = updated.ItemId;
        Name = updated.Name;
        Description = updated.Description;
        Price = updated.Price;
        BrandId = updated.BrandId;
        TypeId = updated.TypeId;
        PictureFileName = updated.PictureFileName;
    }

    internal void Apply(CatalogItemPriceChanged changed)
    {
        Price = changed.NewPrice;
    }
}
