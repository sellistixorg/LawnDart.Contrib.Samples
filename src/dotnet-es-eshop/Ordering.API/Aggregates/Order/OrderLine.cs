namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

/// <summary>
/// Basket lines (product, name, price, units).
/// </summary>
/// <param name="ProductId">Catalog item id.</param>
/// <param name="ProductName">Display name snapshot.</param>
/// <param name="UnitPrice">Unit price snapshot captured in the basket.</param>
/// <param name="Units">Units on the line.</param>
public sealed record OrderLine(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Units);
