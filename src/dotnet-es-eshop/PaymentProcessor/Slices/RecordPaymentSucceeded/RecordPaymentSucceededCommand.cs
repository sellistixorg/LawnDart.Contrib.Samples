using LawnDart;

namespace LawnDart.Samples.EShop.Payment.Commands;

/// <summary>
/// Marks the payment attempt succeeded and notifies Ordering.
/// </summary>
/// <remarks>
/// A later Translate Gateway Succeeded slice issues this command. This host has no HTTP command route for it.
/// </remarks>
/// <param name="CommandId">Command identifier.</param>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="GatewayReference">Gateway transaction reference.</param>
/// <param name="Amount">Charged amount.</param>
public sealed record RecordPaymentSucceededCommand(
    Guid CommandId,
    Guid AttemptId,
    Guid OrderId,
    string? GatewayReference,
    decimal? Amount) : ICommand
{
    /// <summary>
    /// Command identifier.
    /// </summary>
    public Guid Id => CommandId;
}
