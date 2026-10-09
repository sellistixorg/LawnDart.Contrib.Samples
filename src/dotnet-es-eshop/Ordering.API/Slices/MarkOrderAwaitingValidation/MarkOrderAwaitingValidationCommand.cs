using LawnDart;

namespace LawnDart.Samples.EShop.Ordering.Commands;

/// <summary>
/// Sets order status to AwaitingValidation and notifies Catalog.
/// </summary>
/// <remarks>
/// POST /api/ordering/mark-order-awaiting-validation.
/// </remarks>
/// <param name="CommandId">Command identifier.</param>
/// <param name="OrderId">Order to advance.</param>
public sealed record MarkOrderAwaitingValidationCommand(
    Guid CommandId,
    Guid OrderId) : ICommand
{
    /// <summary>
    /// Command identifier.
    /// </summary>
    public Guid Id => CommandId;
}
