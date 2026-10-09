using LawnDart;

namespace LawnDart.Samples.EShop.Payment.Commands;

/// <summary>
/// Marks the payment attempt failed and notifies Ordering.
/// </summary>
/// <remarks>
/// Translate Gateway Failed issues this command. PaymentProcessor has no HTTP route for it.
/// </remarks>
/// <param name="CommandId">Command identifier.</param>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="FailureReason">Why the charge failed.</param>
public sealed record RecordPaymentFailedCommand(
    Guid CommandId,
    Guid AttemptId,
    Guid OrderId,
    string? FailureReason) : ICommand
{
    /// <summary>
    /// Command identifier.
    /// </summary>
    public Guid Id => CommandId;
}
