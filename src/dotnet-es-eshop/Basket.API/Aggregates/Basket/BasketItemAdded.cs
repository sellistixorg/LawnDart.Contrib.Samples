using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;

/// <summary>
/// A line was added (or qty increased) on the shopper basket.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When added.</param>
/// <param name="BuyerId">Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="ProductId">Catalog item id.</param>
/// <param name="ProductName">Display name snapshot.</param>
/// <param name="UnitPrice">Unit price snapshot.</param>
/// <param name="Quantity">Quantity after add.</param>
/// <param name="PictureUrl">Picture URL snapshot.</param>
[EventTypeName("basket-item-added")]
public sealed record BasketItemAdded(
    Guid Id,
    DateTime Timestamp,
    Guid BuyerId,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string? PictureUrl) : IEvent;
