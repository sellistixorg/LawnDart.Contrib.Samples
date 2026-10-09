using LawnDart;

namespace LawnDart.Samples.EShop.Payment.Commands;

/// <summary>
/// Creates a payment attempt and sends a charge request to the gateway.
/// </summary>
/// <remarks>
/// A later reactor issues this command. This host has no HTTP command route for it.
/// </remarks>
/// <param name="CommandId">Command identifier.</param>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order to charge.</param>
/// <param name="Amount">Amount to charge.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="BuyerId">Buyer id.</param>
public sealed record ChargeOrderPaymentCommand(
    Guid CommandId,
    Guid AttemptId,
    Guid OrderId,
    decimal Amount,
    string? Currency,
    Guid? BuyerId) : ICommand
{
    /// <summary>
    /// Command identifier.
    /// </summary>
    public Guid Id => CommandId;
}
