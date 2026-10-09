namespace LawnDart.Samples.EShop.WebApp.Models;

/// <summary>
/// Shopper-facing item detail read model.
/// </summary>
public sealed class ViewCatalogItemView
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
    /// Brand display name.
    /// </summary>
    public string? BrandName { get; set; }

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
}
