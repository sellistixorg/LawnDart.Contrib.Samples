using LawnDart;

namespace LawnDart.Samples.EShop.Basket.Commands;

/// <summary>
/// Adds a product line to the shopper basket with quantity and price snapshot.
/// </summary>
/// <remarks>
/// POST /api/basket/add-item-to-basket.
/// </remarks>
/// <param name="Id">Command identifier.</param>
/// <param name="BuyerId">Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="ProductId">Catalog item id.</param>
/// <param name="ProductName">Display name snapshot.</param>
/// <param name="UnitPrice">Unit price snapshot.</param>
/// <param name="Quantity">Units to add (&gt;= 1).</param>
/// <param name="PictureUrl">Picture URL snapshot.</param>
public sealed record AddItemToBasketCommand(
    Guid Id,
    Guid BuyerId,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string? PictureUrl = null) : ICommand;
