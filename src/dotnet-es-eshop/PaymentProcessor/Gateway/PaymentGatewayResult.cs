namespace LawnDart.Samples.EShop.PaymentProcessor.Gateway;

/// <summary>
/// Outcome of one charge request.
/// </summary>
public abstract record PaymentGatewayResult;

/// <summary>
/// Inbound success signal from the payment gateway.
/// </summary>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="GatewayReference">Gateway transaction reference.</param>
/// <param name="Amount">Charged amount.</param>
public sealed record PaymentGatewaySucceeded(
    Guid AttemptId,
    Guid OrderId,
    string? GatewayReference,
    decimal? Amount) : PaymentGatewayResult;

/// <summary>
/// Inbound failure signal from the payment gateway.
/// </summary>
/// <param name="AttemptId">Payment attempt id.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="FailureReason">Why the charge failed.</param>
public sealed record PaymentGatewayFailed(
    Guid AttemptId,
    Guid OrderId,
    string? FailureReason) : PaymentGatewayResult;
