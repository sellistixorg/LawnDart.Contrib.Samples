using LawnDart;

namespace LawnDart.Samples.EShop.Ordering.Commands;

/// <summary>
/// Marks a paid order as shipped.
/// </summary>
/// <remarks>
/// POST /api/ordering/ship-order.
/// </remarks>
/// <param name="CommandId">Command / request id.</param>
/// <param name="OrderId">Order to ship.</param>
public sealed record ShipOrderCommand(
    Guid CommandId,
    Guid OrderId) : ICommand
{
    /// <summary>
    /// Command / request id.
    /// </summary>
    public Guid Id => CommandId;
}
