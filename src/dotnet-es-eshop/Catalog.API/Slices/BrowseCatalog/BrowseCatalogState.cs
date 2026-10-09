namespace LawnDart.Samples.EShop.Catalog.API.Slices.BrowseCatalog;

/// <summary>
/// Stored catalog index. The route copies a page into <see cref="BrowseCatalogView"/>.
/// </summary>
public sealed class BrowseCatalogState
{
    /// <summary>
    /// Catalog items folded from item events, including removed items.
    /// </summary>
    public List<BrowseCatalogRow> Items { get; set; } = [];

    /// <summary>
    /// Brand ids from Catalog Brand Defined. A brand with no items stays here so its facet count is zero.
    /// </summary>
    public List<Guid> BrandIds { get; set; } = [];

    /// <summary>
    /// Type ids from Catalog Type Defined. A type with no items stays here so its facet count is zero.
    /// </summary>
    public List<Guid> TypeIds { get; set; } = [];
}

/// <summary>
/// One catalog item in the browse index. A removed item stays here and is left out of the page and the facet counts.
/// </summary>
public sealed class BrowseCatalogRow
{
    /// <summary>
    /// Catalog item identifier.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Product name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Unit price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Image file name.
    /// </summary>
    public string? PictureUrl { get; set; }

    /// <summary>
    /// Units currently available.
    /// </summary>
    public int AvailableStock { get; set; }

    /// <summary>
    /// Catalog brand id.
    /// </summary>
    public Guid BrandId { get; set; }

    /// <summary>
    /// Catalog type id.
    /// </summary>
    public Guid TypeId { get; set; }

    /// <summary>
    /// True after the item is removed. Removal is final. The page and the facet counts omit this row.
    /// </summary>
    public bool Removed { get; set; }
}
