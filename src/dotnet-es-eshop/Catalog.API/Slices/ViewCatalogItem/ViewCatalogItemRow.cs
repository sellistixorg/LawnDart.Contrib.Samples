namespace LawnDart.Samples.EShop.Catalog.API.Slices.ViewCatalogItem;

/// <summary>
/// Stored row for one catalog item. The route copies the nine model fields into <see cref="ViewCatalogItemView"/>.
/// </summary>
public sealed class ViewCatalogItemRow
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
    /// Catalog brand id.
    /// </summary>
    public Guid BrandId { get; set; }

    /// <summary>
    /// Brand display name.
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>
    /// Catalog type id.
    /// </summary>
    public Guid TypeId { get; set; }

    /// <summary>
    /// Type display name.
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Available stock.
    /// </summary>
    public int AvailableStock { get; set; }

    /// <summary>
    /// Picture URL.
    /// </summary>
    public string? PictureUrl { get; set; }

    /// <summary>
    /// Whether item is listed and in stock.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// True after the item is removed. Removal is final. The route returns not found.
    /// </summary>
    public bool Removed { get; set; }
}
