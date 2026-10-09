using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.ViewCatalogItem;

/// <summary>
/// Shopper-facing item detail read model.
/// </summary>
[SingleStreamProjection("ViewCatalogItem", streamType: "CatalogItem", tenantScope: TenantScope.TenantScoped)]
public sealed class ViewCatalogItemProjection : ProjectionBase<ViewCatalogItemRow>
{
    /// <summary>
    /// View-store key for one catalog item stream.
    /// </summary>
    public const string StorageKey = "ViewCatalogItem:v1";

    /// <summary>
    /// View instance id for <paramref name="itemId"/>.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <returns>The stream id <c>CatalogItem:{itemId}</c>.</returns>
    public static string InstanceId(Guid itemId) => $"CatalogItem:{itemId:D}";

    /// <summary>
    /// A catalog item was added. Tagged: catalog-item:{itemId}
    /// </summary>
    /// <param name="added">Catalog Item Added.</param>
    public void Handle(CatalogItemAdded added)
    {
        State.ItemId = added.ItemId;
        State.Name = added.Name ?? "";
        State.Description = added.Description;
        State.Price = added.Price;
        State.BrandId = added.BrandId;
        State.TypeId = added.TypeId;
        State.PictureUrl = added.PictureFileName;
        State.AvailableStock = added.AvailableStock;
        State.Removed = false;
        State.IsAvailable = added.AvailableStock > 0;
    }

    /// <summary>
    /// Catalog item details were updated.
    /// </summary>
    /// <param name="updated">Catalog Item Updated.</param>
    public void Handle(CatalogItemUpdated updated)
    {
        State.ItemId = updated.ItemId;
        State.Name = updated.Name ?? "";
        State.Description = updated.Description;
        State.Price = updated.Price;
        State.BrandId = updated.BrandId;
        State.TypeId = updated.TypeId;
        State.PictureUrl = updated.PictureFileName;
    }

    /// <summary>
    /// Catalog item unit price changed (integration signal for baskets/other contexts).
    /// </summary>
    /// <param name="changed">Catalog Item Price Changed.</param>
    public void Handle(CatalogItemPriceChanged changed)
    {
        State.ItemId = changed.ItemId;
        State.Price = changed.NewPrice;
    }

    /// <summary>
    /// Stock was added to a catalog item.
    /// </summary>
    /// <param name="restocked">Catalog Item Restocked.</param>
    public void Handle(CatalogItemRestocked restocked)
    {
        State.ItemId = restocked.ItemId;
        State.AvailableStock = restocked.AvailableStock;
        State.IsAvailable = !State.Removed && restocked.AvailableStock > 0;
    }

    /// <summary>
    /// Stock units were removed after a paid order.
    /// </summary>
    /// <param name="deducted">Catalog Stock Deducted. One event per order. Lines name the item id and the units removed.</param>
    /// <remarks>
    /// Only lines for this item change available stock. Stock does not go below zero.
    /// </remarks>
    public void Handle(CatalogStockDeducted deducted)
    {
        if (State.ItemId == Guid.Empty || deducted.Deductions is null)
            return;

        var removed = 0;
        foreach (var line in deducted.Deductions)
        {
            if (line.ItemId == State.ItemId)
                removed += line.UnitsRemoved;
        }

        var stock = State.AvailableStock - removed;
        State.AvailableStock = stock < 0 ? 0 : stock;
        State.IsAvailable = !State.Removed && State.AvailableStock > 0;
    }

    /// <summary>
    /// Catalog item was removed.
    /// </summary>
    /// <param name="removed">Catalog Item Removed.</param>
    /// <remarks>
    /// Removal is final. The route returns not found for this item id.
    /// </remarks>
    public void Handle(CatalogItemRemoved removed)
    {
        State.ItemId = removed.ItemId;
        State.Removed = true;
        State.IsAvailable = false;
    }
}
