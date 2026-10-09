using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Basket.API.Slices.ClearBasket;

/// <summary>
/// Shopper basket was emptied.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When cleared.</param>
/// <param name="BuyerId">Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="Reason">Why the basket was cleared.</param>
[EventTypeName("basket-cleared")]
public sealed record BasketCleared(
    Guid Id,
    DateTime Timestamp,
    Guid BuyerId,
    string? Reason) : IEvent;
