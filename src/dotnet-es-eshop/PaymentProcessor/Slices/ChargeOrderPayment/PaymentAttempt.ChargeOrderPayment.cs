using LawnDart;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;

public sealed partial class PaymentAttempt
{
    /// <summary>
    /// Command identifier is required.
    /// </summary>
    public const string CommandIdRequiredMessage = "Command identifier is required.";

    /// <summary>
    /// Error code <c>PAYMENT_COMMAND_ID_REQUIRED</c>.
    /// </summary>
    public const string CommandIdRequiredTitle = "PAYMENT_COMMAND_ID_REQUIRED";

    /// <summary>
    /// Payment attempt id is required.
    /// </summary>
    public const string AttemptIdRequiredMessage = "Payment attempt id is required.";

    /// <summary>
    /// Error code <c>PAYMENT_ATTEMPT_ID_REQUIRED</c>.
    /// </summary>
    public const string AttemptIdRequiredTitle = "PAYMENT_ATTEMPT_ID_REQUIRED";

    /// <summary>
    /// Order to charge is required.
    /// </summary>
    public const string OrderIdRequiredMessage = "Order to charge is required.";

    /// <summary>
    /// Error code <c>PAYMENT_ORDER_ID_REQUIRED</c>.
    /// </summary>
    public const string OrderIdRequiredTitle = "PAYMENT_ORDER_ID_REQUIRED";

    /// <summary>
    /// Amount to charge must be greater than zero.
    /// </summary>
    public const string AmountInvalidMessage = "Amount to charge must be greater than zero.";

    /// <summary>
    /// Error code <c>PAYMENT_AMOUNT_INVALID</c>.
    /// </summary>
    public const string AmountInvalidTitle = "PAYMENT_AMOUNT_INVALID";

    /// <summary>
    /// Creates a payment attempt and sends a charge request to the gateway.
    /// </summary>
    /// <param name="command">Charge Order Payment.</param>
    /// <remarks>
    /// An empty command id is rejected with <c>PAYMENT_COMMAND_ID_REQUIRED</c>.
    /// An empty attempt id is rejected with <c>PAYMENT_ATTEMPT_ID_REQUIRED</c>.
    /// An empty order id is rejected with <c>PAYMENT_ORDER_ID_REQUIRED</c>.
    /// An amount that is not greater than zero is rejected with <c>PAYMENT_AMOUNT_INVALID</c>.
    /// An attempt that already has events writes no event.
    /// The command appends Payment Charge Requested.
    /// Applying it sets the attempt id, the order id, the amount, the currency code, and the status to
    /// <see cref="RequestedStatus"/>.
    /// </remarks>
    public void Handle(ChargeOrderPaymentCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(CommandIdRequiredMessage, CommandIdRequiredTitle);

        if (command.AttemptId == Guid.Empty)
            throw new DomainException(AttemptIdRequiredMessage, AttemptIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (command.Amount <= 0)
            throw new DomainException(AmountInvalidMessage, AmountInvalidTitle);

        if (Version != 0)
            return;

        Apply(new PaymentChargeRequested(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.AttemptId,
            command.OrderId,
            command.Amount,
            RequestedStatus,
            ChargeOrderPaymentText.CurrencyOrNull(command.Currency)));
    }
}

public sealed partial class PaymentAttemptState
{
    /// <summary>
    /// Records the charge request on the attempt.
    /// </summary>
    /// <param name="requested">Payment Charge Requested.</param>
    internal void Apply(PaymentChargeRequested requested)
    {
        AttemptId = requested.AttemptId;
        OrderId = requested.OrderId;
        Amount = requested.Amount;
        Currency = requested.Currency;
        Status = requested.Status;
    }
}

file static class ChargeOrderPaymentText
{
    public static string? CurrencyOrNull(string? currency)
    {
        var trimmed = currency?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
