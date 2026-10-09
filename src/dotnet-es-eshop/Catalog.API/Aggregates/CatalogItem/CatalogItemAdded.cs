using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;

/// <summary>
/// A catalog item was added. Tagged: catalog-item:{itemId}
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When added.</param>
/// <param name="ItemId">Item id.</param>
/// <param name="Name">Product name.</param>
/// <param name="Description">Description.</param>
/// <param name="Price">Unit price.</param>
/// <param name="PictureFileName">Picture file name.</param>
/// <param name="BrandId">Brand id.</param>
/// <param name="TypeId">Type id.</param>
/// <param name="AvailableStock">Initial stock.</param>
/// <param name="RestockThreshold">Reorder threshold.</param>
/// <param name="MaxStockThreshold">Maximum stock capacity.</param>
[EventTypeName("catalog-item-added")]
public sealed record CatalogItemAdded(
    Guid Id,
    DateTime Timestamp,
    Guid ItemId,
    string Name,
    string? Description,
    decimal Price,
    string? PictureFileName,
    Guid BrandId,
    Guid TypeId,
    int AvailableStock,
    int? RestockThreshold,
    int? MaxStockThreshold) : IEvent;
