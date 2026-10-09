using LawnDart;

namespace LawnDart.Samples.EShop.Ordering.Commands;

/// <summary>
/// Cancels an order that is not yet Paid or Shipped.
/// </summary>
/// <remarks>
/// POST /api/ordering/cancel-order.
/// </remarks>
/// <param name="CommandId">Command / request id.</param>
/// <param name="OrderId">Order to cancel.</param>
/// <param name="Reason">Optional cancel reason.</param>
public sealed record CancelOrderCommand(
    Guid CommandId,
    Guid OrderId,
    string? Reason = null) : ICommand
{
    /// <summary>
    /// Command / request id.
    /// </summary>
    public Guid Id => CommandId;
}
