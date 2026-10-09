using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;

/// <summary>
/// Stock was added to a catalog item.
/// </summary>
/// <param name="Id">Event identifier</param>
/// <param name="Timestamp">When restocked</param>
/// <param name="ItemId">Item id</param>
/// <param name="QuantityAdded">Units actually added</param>
/// <param name="AvailableStock">Stock after restock</param>
[EventTypeName("catalog-item-restocked")]
public sealed record CatalogItemRestocked(
    Guid Id,
    DateTime Timestamp,
    Guid ItemId,
    int QuantityAdded,
    int AvailableStock) : IEvent;
