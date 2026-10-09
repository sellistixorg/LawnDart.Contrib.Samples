using LawnDart;

namespace LawnDart.Samples.EShop.Basket.Commands;

/// <summary>
/// Sets quantity for a product already in the basket.
/// </summary>
/// <remarks>
/// POST /api/basket/change-basket-item-quantity.
/// </remarks>
/// <param name="Id">Command identifier.</param>
/// <param name="BuyerId">Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="ProductId">Catalog item id.</param>
/// <param name="Quantity">New quantity (&gt;= 1).</param>
public sealed record ChangeBasketItemQuantityCommand(
    Guid Id,
    Guid BuyerId,
    Guid ProductId,
    int Quantity) : ICommand;
