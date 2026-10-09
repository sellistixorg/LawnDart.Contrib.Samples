using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;

/// <summary>
/// State of a catalog item.
/// </summary>
public sealed partial class CatalogItemState : IState
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
    public string? PictureFileName { get; set; }

    /// <summary>
    /// Catalog brand id.
    /// </summary>
    public Guid BrandId { get; set; }

    /// <summary>
    /// Catalog type id.
    /// </summary>
    public Guid TypeId { get; set; }

    /// <summary>
    /// Units currently available.
    /// </summary>
    public int AvailableStock { get; set; }

    /// <summary>
    /// Reorder threshold.
    /// </summary>
    public int? RestockThreshold { get; set; }

    /// <summary>
    /// Maximum stock capacity.
    /// </summary>
    public int? MaxStockThreshold { get; set; }

    /// <summary>
    /// Whether a restock is in progress.
    /// </summary>
    public bool OnReorder { get; set; }

    /// <summary>
    /// True after the item is added and before it is removed.
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// True after the item is removed. Removal is final.
    /// </summary>
    public bool Removed { get; set; }
}
