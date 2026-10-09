using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;

/// <summary>
/// Catalog item unit price changed (integration signal for baskets/other contexts).
/// </summary>
/// <param name="Id">Event identifier</param>
/// <param name="Timestamp">When price changed</param>
/// <param name="ItemId">Item id</param>
/// <param name="OldPrice">Previous unit price</param>
/// <param name="NewPrice">New unit price</param>
[EventTypeName("catalog-item-price-changed")]
public sealed record CatalogItemPriceChanged(
    Guid Id,
    DateTime Timestamp,
    Guid ItemId,
    decimal OldPrice,
    decimal NewPrice) : IEvent;
