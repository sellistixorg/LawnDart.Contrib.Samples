using LawnDart;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.Shared.Events;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;

public sealed partial class PaymentAttempt
{
    /// <summary>
    /// Marks the payment attempt succeeded and notifies Ordering.
    /// </summary>
    /// <param name="command">Record Payment Succeeded.</param>
    /// <remarks>
    /// An empty command id is rejected with <c>PAYMENT_COMMAND_ID_REQUIRED</c>.
    /// An empty attempt id is rejected with <c>PAYMENT_ATTEMPT_ID_REQUIRED</c>.
    /// An empty order id is rejected with <c>PAYMENT_ORDER_ID_REQUIRED</c>.
    /// An attempt with no Payment Charge Requested is rejected with <c>PAYMENT_ATTEMPT_NOT_FOUND</c>.
    /// An attempt whose status is <see cref="SucceededStatus"/> or <see cref="FailedStatus"/> writes no event.
    /// An order id that does not match the attempt is rejected with <c>PAYMENT_ORDER_ID_MISMATCH</c>.
    /// The command appends Payment Succeeded on stream <c>PaymentAttempt:{attemptId}</c>.
    /// Applying it sets the status to <see cref="SucceededStatus"/>.
    /// The attempt order id stays as it was.
    /// A gateway reference on the command is stored. A charged amount on the command replaces the requested amount.
    /// </remarks>
    public void Handle(RecordPaymentSucceededCommand command)
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

        Apply(new PaymentSucceeded(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.AttemptId,
            command.OrderId,
            RecordPaymentSucceededText.ReferenceOrNull(command.GatewayReference),
            command.Amount));
    }
}

public sealed partial class PaymentAttemptState
{
    /// <summary>
    /// Records gateway success on the attempt.
    /// </summary>
    /// <param name="succeeded">Payment Succeeded.</param>
    internal void Apply(PaymentSucceeded succeeded)
    {
        AttemptId = succeeded.AttemptId;
        Status = PaymentAttempt.SucceededStatus;
        if (succeeded.GatewayReference is not null)
            GatewayReference = succeeded.GatewayReference;
        if (succeeded.Amount is not null)
            Amount = succeeded.Amount.Value;
    }
}

file static class RecordPaymentSucceededText
{
    public static string? ReferenceOrNull(string? reference)
    {
        var trimmed = reference?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
