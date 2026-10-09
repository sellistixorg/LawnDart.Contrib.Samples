namespace LawnDart.Samples.EShop.Catalog.API.Slices.SearchCatalog;

/// <summary>
/// Catalog items whose names start with the search text.
/// </summary>
public sealed class SearchCatalogView
{
    /// <summary>
    /// Search text (matched as a name prefix).
    /// </summary>
    public string Query { get; set; } = "";

    /// <summary>
    /// Item summaries whose names start with the search text.
    /// </summary>
    public List<SearchCatalogItem> Items { get; set; } = [];

    /// <summary>
    /// Total matches.
    /// </summary>
    public int TotalItems { get; set; }
}

/// <summary>
/// One catalog item whose name starts with the search text.
/// </summary>
public sealed class SearchCatalogItem
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
    /// Picture URL.
    /// </summary>
    public string? PictureUrl { get; set; }

    /// <summary>
    /// Available stock.
    /// </summary>
    public int AvailableStock { get; set; }

    /// <summary>
    /// Whether item is listed and in stock.
    /// </summary>
    public bool IsAvailable { get; set; }
}
