using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Basket.API.Slices.ChangeBasketItemQuantity;

/// <summary>
/// Basket line quantity was changed.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When changed.</param>
/// <param name="BuyerId">Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="ProductId">Catalog item id.</param>
/// <param name="OldQuantity">Previous quantity.</param>
/// <param name="NewQuantity">New quantity.</param>
[EventTypeName("basket-item-quantity-changed")]
public sealed record BasketItemQuantityChanged(
    Guid Id,
    DateTime Timestamp,
    Guid BuyerId,
    Guid ProductId,
    int OldQuantity,
    int NewQuantity) : IEvent;
