namespace LawnDart.Samples.EShop.Catalog.API.Slices.CatalogItemDetailAdmin;

/// <summary>
/// Admin projection of catalog item including stock thresholds.
/// </summary>
public sealed class CatalogItemDetailAdminView
{
    /// <summary>
    /// Item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Brand id.
    /// </summary>
    public Guid BrandId { get; set; }

    /// <summary>
    /// Type id.
    /// </summary>
    public Guid TypeId { get; set; }

    /// <summary>
    /// Available stock.
    /// </summary>
    public int AvailableStock { get; set; }

    /// <summary>
    /// Restock threshold.
    /// </summary>
    public int? RestockThreshold { get; set; }

    /// <summary>
    /// Max stock.
    /// </summary>
    public int? MaxStockThreshold { get; set; }

    /// <summary>
    /// On reorder.
    /// </summary>
    public bool OnReorder { get; set; }

    /// <summary>
    /// Picture file name.
    /// </summary>
    public string? PictureFileName { get; set; }

    /// <summary>
    /// True after the item is removed. The route still returns the item.
    /// </summary>
    public bool Removed { get; set; }
}
