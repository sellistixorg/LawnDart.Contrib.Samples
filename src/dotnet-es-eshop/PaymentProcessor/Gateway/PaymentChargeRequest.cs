namespace LawnDart.Samples.EShop.PaymentProcessor.Gateway;

/// <summary>
/// Outbound charge request to the payment gateway.
/// </summary>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order to charge.</param>
/// <param name="Amount">Amount to charge.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="BuyerId">Buyer id.</param>
public sealed record PaymentChargeRequest(
    Guid AttemptId,
    Guid OrderId,
    decimal Amount,
    string? Currency,
    Guid? BuyerId);
