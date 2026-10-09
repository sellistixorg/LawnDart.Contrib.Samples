namespace LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;

/// <summary>
/// Basket line items (product, qty, price snapshot).
/// </summary>
/// <param name="ProductId">Catalog item id.</param>
/// <param name="ProductName">Display name snapshot.</param>
/// <param name="UnitPrice">Unit price snapshot.</param>
/// <param name="Quantity">Quantity on the line.</param>
/// <param name="PictureUrl">Picture URL snapshot.</param>
public sealed record BasketItem(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string? PictureUrl);
