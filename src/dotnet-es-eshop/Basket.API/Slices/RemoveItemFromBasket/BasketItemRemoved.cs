using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Basket.API.Slices.RemoveItemFromBasket;

/// <summary>
/// A product line was removed from the basket.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When removed.</param>
/// <param name="BuyerId">Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="ProductId">Removed catalog item id.</param>
[EventTypeName("basket-item-removed")]
public sealed record BasketItemRemoved(
    Guid Id,
    DateTime Timestamp,
    Guid BuyerId,
    Guid ProductId) : IEvent;
