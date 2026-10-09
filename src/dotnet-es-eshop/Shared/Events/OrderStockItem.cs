namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Product id + units to validate.
/// </summary>
/// <param name="ProductId">Catalog item id.</param>
/// <param name="Units">Units to validate.</param>
public sealed record OrderStockItem(Guid ProductId, int Units);
