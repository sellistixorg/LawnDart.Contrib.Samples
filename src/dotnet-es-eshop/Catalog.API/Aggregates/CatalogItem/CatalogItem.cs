using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;

/// <summary>
/// Sellable product in the catalog. Stream key: catalog-item:{itemId}. Stock fields live on the item (eShop CatalogItem).
/// </summary>
public sealed partial class CatalogItem : AggregateRoot<CatalogItemState>
{
    /// <summary>
    /// An item with this item id was already added. Add Catalog Item rejects the duplicate; re-running the seed must check first.
    /// </summary>
    /// <remarks>
    /// Also rejects an item id whose stream already has events, including a removed item. Removal is final.
    /// </remarks>
    public const string AlreadyExistsMessage = "Catalog item already exists.";

    /// <summary>
    /// Error code <c>CATALOG_ITEM_ALREADY_EXISTS</c>.
    /// </summary>
    public const string AlreadyExistsTitle = "CATALOG_ITEM_ALREADY_EXISTS";

    /// <summary>
    /// Update Catalog Item, Remove Catalog Item, and Restock Catalog Item reject an item id that is not on the stream.
    /// A removed item has Exists set to false and Removed set to true, and stays removed.
    /// Deduct Catalog Stock skips an unknown or removed item.
    /// </summary>
    public const string NotFoundMessage = "Catalog item does not exist.";

    /// <summary>
    /// Error code <c>CATALOG_ITEM_NOT_FOUND</c>.
    /// </summary>
    public const string NotFoundTitle = "CATALOG_ITEM_NOT_FOUND";

    /// <summary>
    /// Deduct Catalog Stock rejects the item when available stock is already 0.
    /// That check runs before the quantity check.
    /// </summary>
    public const string OutOfStockMessage = "Catalog item is out of stock.";

    /// <summary>
    /// Error code <c>CATALOG_ITEM_OUT_OF_STOCK</c>.
    /// </summary>
    public const string OutOfStockTitle = "CATALOG_ITEM_OUT_OF_STOCK";

    /// <inheritdoc />
    protected override void ApplyEventToState(IEvent @event) => EShopStateApplier.Apply(State, @event);

    private static string? BlankToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
