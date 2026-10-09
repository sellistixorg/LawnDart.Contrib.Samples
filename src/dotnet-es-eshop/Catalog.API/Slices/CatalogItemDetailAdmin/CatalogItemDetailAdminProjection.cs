using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.CatalogItemDetailAdmin;

/// <summary>
/// Admin projection of catalog item including stock thresholds.
/// </summary>
[SingleStreamProjection("CatalogItemDetailAdmin", streamType: "CatalogItem", tenantScope: TenantScope.TenantScoped)]
public sealed class CatalogItemDetailAdminProjection : ProjectionBase<CatalogItemDetailAdminView>
{
    /// <summary>
    /// View-store key for one catalog item stream.
    /// </summary>
    public const string StorageKey = "CatalogItemDetailAdmin:v1";

    /// <summary>
    /// View instance id for <paramref name="itemId"/>.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <returns>The stream id <c>CatalogItem:{itemId}</c>.</returns>
    public static string InstanceId(Guid itemId) => $"CatalogItem:{itemId:D}";

    /// <summary>
    /// A catalog item was added. Tagged: catalog-item:{itemId}
    /// </summary>
    /// <param name="added">A catalog item was added. Tagged: catalog-item:{itemId}</param>
    /// <remarks>
    /// On reorder starts false. Restock threshold and max stock come from the event.
    /// </remarks>
    public void Handle(CatalogItemAdded added)
    {
        State.ItemId = added.ItemId;
        State.Name = added.Name ?? "";
        State.Description = added.Description;
        State.Price = added.Price;
        State.BrandId = added.BrandId;
        State.TypeId = added.TypeId;
        State.AvailableStock = added.AvailableStock;
        State.RestockThreshold = added.RestockThreshold;
        State.MaxStockThreshold = added.MaxStockThreshold;
        State.OnReorder = false;
        State.PictureFileName = added.PictureFileName;
        State.Removed = false;
    }

    /// <summary>
    /// Catalog item details were updated.
    /// </summary>
    /// <param name="updated">Catalog item details were updated.</param>
    public void Handle(CatalogItemUpdated updated)
    {
        State.ItemId = updated.ItemId;
        State.Name = updated.Name ?? "";
        State.Description = updated.Description;
        State.Price = updated.Price;
        State.BrandId = updated.BrandId;
        State.TypeId = updated.TypeId;
        State.PictureFileName = updated.PictureFileName;
    }

    /// <summary>
    /// Catalog item unit price changed (integration signal for baskets/other contexts).
    /// </summary>
    /// <param name="changed">Catalog item unit price changed (integration signal for baskets/other contexts).</param>
    public void Handle(CatalogItemPriceChanged changed)
    {
        State.ItemId = changed.ItemId;
        State.Price = changed.NewPrice;
    }

    /// <summary>
    /// Stock was added to a catalog item.
    /// </summary>
    /// <param name="restocked">Stock was added to a catalog item.</param>
    /// <remarks>
    /// Restock sets OnReorder to false.
    /// </remarks>
    public void Handle(CatalogItemRestocked restocked)
    {
        State.ItemId = restocked.ItemId;
        State.AvailableStock = restocked.AvailableStock;
        State.OnReorder = false;
    }

    /// <summary>
    /// Stock units were removed after a paid order.
    /// </summary>
    /// <param name="deducted">Stock units were removed after a paid order.</param>
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
    }

    /// <summary>
    /// Catalog item was removed.
    /// </summary>
    /// <param name="removed">Catalog item was removed.</param>
    /// <remarks>
    /// The route still returns the item. Removed is set so back office can see it.
    /// </remarks>
    public void Handle(CatalogItemRemoved removed)
    {
        State.ItemId = removed.ItemId;
        State.Removed = true;
    }
}
