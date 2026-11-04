namespace HarborStreet.Checkout.Dcb.Checkout;

public enum DcbCheckoutStatus
{
    Succeeded,
    RejectedUnavailable,
    /// <summary>Lost the DCB AppendCondition race — no events appended.</summary>
    ConflictLost
}

public sealed record DcbCheckoutOutcome(
    DcbCheckoutStatus Status,
    Guid OrderId,
    Guid? CustomerId = null,
    string? Detail = null);
