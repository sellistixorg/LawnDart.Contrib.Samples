namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Per-line hasStock flags.
/// </summary>
/// <param name="ProductId">Catalog item id for this order line.</param>
/// <param name="HasStock">True when that line had available stock for the requested units.</param>
public sealed record ConfirmedOrderStockItem(Guid ProductId, bool HasStock);
