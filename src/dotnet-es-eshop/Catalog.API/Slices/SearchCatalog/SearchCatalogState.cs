namespace LawnDart.Samples.EShop.Catalog.API.Slices.SearchCatalog;

/// <summary>
/// Stored catalog index. The route copies matching rows into <see cref="SearchCatalogView"/>.
/// </summary>
public sealed class SearchCatalogState
{
    /// <summary>
    /// Catalog items folded from item events, including removed items.
    /// </summary>
    public List<SearchCatalogRow> Items { get; set; } = [];
}

/// <summary>
/// One catalog item in the search index. A removed item stays here and is left out of search results.
/// </summary>
public sealed class SearchCatalogRow
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
    /// Product description.
    /// </summary>
    public string? Description { get; set; }

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
    /// Whether item is listed and in stock.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// True after the item is removed. Removal is final. Search results omit this row.
    /// </summary>
    public bool Removed { get; set; }
}
