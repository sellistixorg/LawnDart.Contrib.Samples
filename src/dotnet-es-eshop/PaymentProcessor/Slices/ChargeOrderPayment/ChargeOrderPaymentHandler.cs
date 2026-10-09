using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;

/// <summary>
/// Creates a payment attempt and sends a charge request to the gateway.
/// </summary>
public sealed class ChargeOrderPaymentHandler : ICommandHandler<ChargeOrderPaymentCommand>
{
    private readonly IAggregateRepository _attempts;
    private readonly IPaymentGateway _gateway;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="attempts">Aggregate repository for the payment attempt stream.</param>
    /// <param name="gateway">Outbound charge port.</param>
    public ChargeOrderPaymentHandler(IAggregateRepository attempts, IPaymentGateway gateway)
    {
        _attempts = attempts;
        _gateway = gateway;
    }

    /// <summary>
    /// Creates a payment attempt and sends a charge request to the gateway.
    /// </summary>
    /// <param name="command">Charge Order Payment.</param>
    /// <param name="cancellationToken">Token used to cancel the load, the write, and the charge.</param>
    /// <returns>A task that completes when the attempt is written and the gateway has been called.</returns>
    /// <remarks>
    /// Payment Charge Requested is appended once.
    /// The gateway is called while the attempt status is still <see cref="PaymentAttempt.RequestedStatus"/>.
    /// A retry after a gateway failure calls the gateway again. The gateway dedupes by attempt id.
    /// A rejected command does not call the gateway.
    /// A later slice that records success or failure changes the status, and this handler stops calling.
    /// </remarks>
    public async Task HandleAsync(
        ChargeOrderPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _attempts
            .GetOrCreateAsync<PaymentAttempt>(command.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        await _attempts
            .HandleCommandAsync(attempt, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (attempt.State.Status != PaymentAttempt.RequestedStatus)
            return;

        await _gateway
            .ChargeAsync(ChargeOrderPaymentRequest.From(attempt.State, command.BuyerId), cancellationToken)
            .ConfigureAwait(false);
    }
}

file static class ChargeOrderPaymentRequest
{
    public static PaymentChargeRequest From(PaymentAttemptState state, Guid? buyerId) =>
        new(
            state.AttemptId,
            state.OrderId,
            state.Amount,
            state.Currency,
            buyerId is null || buyerId.Value == Guid.Empty ? null : buyerId);
}
