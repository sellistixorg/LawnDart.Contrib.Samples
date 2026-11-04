namespace HarborStreet.EndToEnd.Checkout;

public enum CheckoutStatus
{
    Succeeded,
    RejectedUnavailable,
    ConflictLost
}

public sealed record CheckoutOutcome(
    CheckoutStatus Status,
    Guid OrderId,
    Guid? CustomerId = null,
    string? Detail = null);
