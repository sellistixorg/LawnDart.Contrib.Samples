namespace LawnDart.Samples.EShop.Catalog.API.Slices.BrowseCatalog;

/// <summary>
/// Paginated item list plus facet counts for brands and types.
/// </summary>
public sealed class BrowseCatalogView
{
    /// <summary>
    /// Page of catalog item summaries (item id, name, price, picture URL, available stock).
    /// </summary>
    public List<BrowseCatalogItem> Items { get; set; } = [];

    /// <summary>
    /// Total matching items.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Current page index.
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// Page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Brand id + count pairs (brand names come from the Catalog Brands And Types view).
    /// </summary>
    public List<BrowseCatalogFacet> BrandFacets { get; set; } = [];

    /// <summary>
    /// Type id + count pairs (type names come from the Catalog Brands And Types view).
    /// </summary>
    public List<BrowseCatalogFacet> TypeFacets { get; set; } = [];

    /// <summary>
    /// Active brand filters.
    /// </summary>
    public List<Guid> SelectedBrandIds { get; set; } = [];

    /// <summary>
    /// Active type filters.
    /// </summary>
    public List<Guid> SelectedTypeIds { get; set; } = [];
}

/// <summary>
/// One catalog item summary on the browse page.
/// </summary>
public sealed class BrowseCatalogItem
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
    /// Price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Picture URL.
    /// </summary>
    public string? PictureUrl { get; set; }

    /// <summary>
    /// Available stock.
    /// </summary>
    public int AvailableStock { get; set; }
}

/// <summary>
/// One brand id or type id and the number of matching items.
/// </summary>
public sealed class BrowseCatalogFacet
{
    /// <summary>
    /// Brand id or type id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Count of matching items for this id.
    /// </summary>
    public int Count { get; set; }
}
