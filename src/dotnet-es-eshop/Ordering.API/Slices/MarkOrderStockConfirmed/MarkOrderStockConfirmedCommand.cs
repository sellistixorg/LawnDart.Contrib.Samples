using LawnDart;

namespace LawnDart.Samples.EShop.Ordering.Commands;

/// <summary>
/// Sets order status to StockConfirmed.
/// </summary>
/// <remarks>
/// POST /api/ordering/mark-order-stock-confirmed.
/// </remarks>
/// <param name="CommandId">Command identifier.</param>
/// <param name="OrderId">Order id.</param>
public sealed record MarkOrderStockConfirmedCommand(
    Guid CommandId,
    Guid OrderId) : ICommand
{
    /// <summary>
    /// Command identifier.
    /// </summary>
    public Guid Id => CommandId;
}
