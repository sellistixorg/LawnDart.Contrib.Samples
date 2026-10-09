using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;

/// <summary>
/// Shopper basket keyed by buyer/account id (eShop CustomerBasket in Redis). Stream key: basket:{buyerId}
/// </summary>
public sealed partial class Basket : AggregateRoot<BasketState>
{
    /// <summary>
    /// Quantity must be at least 1 (eShop BasketItem validation).
    /// </summary>
    public const string QuantityInvalidMessage =
        "Quantity must be at least 1 (eShop BasketItem validation).";

    /// <summary>
    /// Error code <c>BASKET_QUANTITY_INVALID</c>.
    /// </summary>
    public const string QuantityInvalidTitle = "BASKET_QUANTITY_INVALID";

    /// <summary>
    /// Change Basket Item Quantity, Remove Item From Basket, and Clear Basket reject a basket that has no events.
    /// </summary>
    public const string NotFoundMessage = "Basket does not exist.";

    /// <summary>
    /// Error code <c>BASKET_NOT_FOUND</c>.
    /// </summary>
    public const string NotFoundTitle = "BASKET_NOT_FOUND";

    /// <summary>
    /// Change Basket Item Quantity and Remove Item From Basket reject a product that is not on the basket.
    /// </summary>
    public const string ItemNotFoundMessage = "Product is not in the basket.";

    /// <summary>
    /// Error code <c>BASKET_ITEM_NOT_FOUND</c>.
    /// </summary>
    public const string ItemNotFoundTitle = "BASKET_ITEM_NOT_FOUND";

    /// <inheritdoc />
    protected override void ApplyEventToState(IEvent @event) => EShopStateApplier.Apply(State, @event);

    private BasketItem? Find(Guid productId)
    {
        var index = State.IndexOf(productId);
        return index < 0 ? null : State.Items[index];
    }
}
