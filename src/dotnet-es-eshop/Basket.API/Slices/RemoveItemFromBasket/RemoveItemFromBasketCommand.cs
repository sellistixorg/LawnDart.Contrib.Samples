using LawnDart;

namespace LawnDart.Samples.EShop.Basket.Commands;

/// <summary>
/// Removes a product line from the shopper basket.
/// </summary>
/// <remarks>
/// POST /api/basket/remove-item-from-basket.
/// </remarks>
/// <param name="Id">Command identifier.</param>
/// <param name="BuyerId">Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="ProductId">Catalog item id to remove.</param>
public sealed record RemoveItemFromBasketCommand(
    Guid Id,
    Guid BuyerId,
    Guid ProductId) : ICommand;
