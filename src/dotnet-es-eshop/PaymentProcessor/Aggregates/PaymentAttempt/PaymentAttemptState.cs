using LawnDart;

namespace LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;

/// <summary>
/// Decision state for one charge attempt.
/// </summary>
public sealed partial class PaymentAttemptState : IState
{
    /// <summary>
    /// Payment attempt id.
    /// </summary>
    public Guid AttemptId { get; set; }

    /// <summary>
    /// Order being charged.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Charge amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code.
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Requested|Succeeded|Failed.
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    /// External gateway reference.
    /// </summary>
    public string? GatewayReference { get; set; }

    /// <summary>
    /// Failure detail if any.
    /// </summary>
    public string? FailureReason { get; set; }
}
