namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.PaymentAttemptStatus;

/// <summary>
/// Projection of payment attempt status for an order.
/// </summary>
public sealed class PaymentAttemptStatusView
{
    /// <summary>
    /// Attempt id.
    /// </summary>
    public Guid AttemptId { get; set; }

    /// <summary>
    /// Order id.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Requested|Succeeded|Failed.
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    /// Gateway reference.
    /// </summary>
    public string? GatewayReference { get; set; }

    /// <summary>
    /// Failure detail.
    /// </summary>
    public string? FailureReason { get; set; }
}
