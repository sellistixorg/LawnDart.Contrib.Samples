namespace HarborStreet.Checkout.Aggregates.Checkout;

public enum CheckoutStatus
{
    Succeeded,
    RejectedUnavailable,
    /// <summary>
    /// Order was committed but Title reserve failed — classic multi-aggregate pain.
    /// </summary>
    NeedsCompensation
}

public sealed record CheckoutOutcome(
    CheckoutStatus Status,
    Guid OrderId,
    Guid? CustomerId = null,
    string? Detail = null);
