using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;

/// <summary>
/// Catalog item details were updated.
/// </summary>
/// <param name="Id">Event identifier</param>
/// <param name="Timestamp">When updated</param>
/// <param name="ItemId">Item id</param>
/// <param name="Name">Product name</param>
/// <param name="Description">Description</param>
/// <param name="Price">Unit price</param>
/// <param name="BrandId">Brand id</param>
/// <param name="TypeId">Type id</param>
/// <param name="PictureFileName">Picture file name</param>
[EventTypeName("catalog-item-updated")]
public sealed record CatalogItemUpdated(
    Guid Id,
    DateTime Timestamp,
    Guid ItemId,
    string Name,
    string? Description,
    decimal Price,
    Guid BrandId,
    Guid TypeId,
    string? PictureFileName) : IEvent;
