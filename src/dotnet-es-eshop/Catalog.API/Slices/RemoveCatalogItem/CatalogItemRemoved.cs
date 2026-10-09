using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;

/// <summary>
/// Catalog item was removed.
/// </summary>
/// <param name="Id">Event identifier</param>
/// <param name="Timestamp">When removed</param>
/// <param name="ItemId">Item id</param>
[EventTypeName("catalog-item-removed")]
public sealed record CatalogItemRemoved(
    Guid Id,
    DateTime Timestamp,
    Guid ItemId) : IEvent;
