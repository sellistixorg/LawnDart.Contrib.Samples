using LawnDart;

namespace LawnDart.Samples.EShop.Ordering.Commands;

/// <summary>
/// Cancels order because stock validation failed.
/// </summary>
/// <remarks>
/// POST /api/ordering/cancel-order-for-insufficient-stock.
/// </remarks>
/// <param name="CommandId">Command identifier.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="RejectedProductIds">Products lacking stock.</param>
public sealed record CancelOrderForInsufficientStockCommand(
    Guid CommandId,
    Guid OrderId,
    IReadOnlyList<Guid>? RejectedProductIds = null) : ICommand
{
    /// <summary>
    /// Command identifier.
    /// </summary>
    public Guid Id => CommandId;
}
