using LawnDart;

namespace LawnDart.Samples.EShop.Ordering.Commands;

/// <summary>
/// Sets order status to Paid and signals Catalog to deduct stock.
/// </summary>
/// <remarks>
/// POST /api/ordering/mark-order-paid.
/// </remarks>
/// <param name="CommandId">Command identifier.</param>
/// <param name="OrderId">Order id.</param>
public sealed record MarkOrderPaidCommand(
    Guid CommandId,
    Guid OrderId) : ICommand
{
    /// <summary>
    /// Command identifier.
    /// </summary>
    public Guid Id => CommandId;
}
