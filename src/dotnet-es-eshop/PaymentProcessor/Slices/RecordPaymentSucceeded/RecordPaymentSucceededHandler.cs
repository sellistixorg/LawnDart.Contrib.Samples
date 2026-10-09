using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.RecordPaymentSucceeded;

/// <summary>
/// Marks the payment attempt succeeded and notifies Ordering.
/// </summary>
public sealed class RecordPaymentSucceededHandler : ICommandHandler<RecordPaymentSucceededCommand>
{
    private readonly IAggregateRepository _attempts;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="attempts">Aggregate repository for the payment attempt stream.</param>
    public RecordPaymentSucceededHandler(IAggregateRepository attempts) => _attempts = attempts;

    /// <summary>
    /// Marks the payment attempt succeeded and notifies Ordering.
    /// </summary>
    /// <param name="command">Record Payment Succeeded.</param>
    /// <param name="cancellationToken">Token used to cancel the load and the write.</param>
    /// <returns>A task that completes when the attempt events are written.</returns>
    public async Task HandleAsync(
        RecordPaymentSucceededCommand command,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _attempts
            .GetOrCreateAsync<PaymentAttempt>(command.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        await _attempts
            .HandleCommandAsync(attempt, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
