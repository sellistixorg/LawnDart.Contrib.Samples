using LawnDart;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.Shared.Events;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;

public sealed partial class PaymentAttempt
{
    /// <summary>
    /// Payment attempt was not found.
    /// </summary>
    public const string AttemptNotFoundMessage = "Payment attempt was not found.";

    /// <summary>
    /// Error code <c>PAYMENT_ATTEMPT_NOT_FOUND</c>.
    /// </summary>
    public const string AttemptNotFoundTitle = "PAYMENT_ATTEMPT_NOT_FOUND";

    /// <summary>
    /// Order id does not match the payment attempt.
    /// </summary>
    public const string OrderMismatchMessage = "Order id does not match the payment attempt.";

    /// <summary>
    /// Error code <c>PAYMENT_ORDER_ID_MISMATCH</c>.
    /// </summary>
    public const string OrderMismatchTitle = "PAYMENT_ORDER_ID_MISMATCH";

    /// <summary>
    /// Marks the payment attempt failed and notifies Ordering.
    /// </summary>
    /// <param name="command">Record Payment Failed.</param>
    /// <remarks>
    /// An empty command id is rejected with <c>PAYMENT_COMMAND_ID_REQUIRED</c>.
    /// An empty attempt id is rejected with <c>PAYMENT_ATTEMPT_ID_REQUIRED</c>.
    /// An empty order id is rejected with <c>PAYMENT_ORDER_ID_REQUIRED</c>.
    /// An attempt with no Payment Charge Requested is rejected with <c>PAYMENT_ATTEMPT_NOT_FOUND</c>.
    /// An attempt whose status is <see cref="SucceededStatus"/> or <see cref="FailedStatus"/> writes no event.
    /// An order id that does not match the attempt is rejected with <c>PAYMENT_ORDER_ID_MISMATCH</c>.
    /// The command appends Payment Failed on stream <c>PaymentAttempt:{attemptId}</c>.
    /// Applying it sets the status to <see cref="FailedStatus"/> and stores the failure detail.
    /// The failure detail is the command reason.
    /// </remarks>
    public void Handle(RecordPaymentFailedCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(CommandIdRequiredMessage, CommandIdRequiredTitle);

        if (command.AttemptId == Guid.Empty)
            throw new DomainException(AttemptIdRequiredMessage, AttemptIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (Version == 0)
            throw new DomainException(AttemptNotFoundMessage, AttemptNotFoundTitle);

        if (State.Status is SucceededStatus or FailedStatus)
            return;

        if (command.OrderId != State.OrderId)
            throw new DomainException(OrderMismatchMessage, OrderMismatchTitle);

        Apply(new PaymentFailed(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.AttemptId,
            command.OrderId,
            RecordPaymentFailedText.ReasonOrNull(command.FailureReason)));
    }
}

public sealed partial class PaymentAttemptState
{
    /// <summary>
    /// Records the failed charge on the attempt.
    /// </summary>
    /// <param name="failed">Payment Failed.</param>
    internal void Apply(PaymentFailed failed)
    {
        AttemptId = failed.AttemptId;
        OrderId = failed.OrderId;
        Status = PaymentAttempt.FailedStatus;
        FailureReason = failed.FailureReason;
    }
}

file static class RecordPaymentFailedText
{
    public static string? ReasonOrNull(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
