using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.Commands;

/// <summary>
/// Decrements stock for each paid order line.
/// Namespace ends in <c>Commands</c> so HTTP maps
/// <c>POST /api/catalog/deduct-catalog-stock</c>.
/// </summary>
/// <param name="Id">Command identifier</param>
/// <param name="OrderId">Paid order id</param>
/// <param name="OrderStockItems">Product id + units to deduct</param>
public sealed record DeductCatalogStockCommand(
    Guid Id,
    Guid OrderId,
    IReadOnlyList<OrderStockItem> OrderStockItems) : ICommand;

/// <summary>
/// Product id + units to deduct.
/// </summary>
/// <param name="ProductId">Product id</param>
/// <param name="Units">Units to deduct</param>
public sealed record OrderStockItem(Guid ProductId, int Units);
