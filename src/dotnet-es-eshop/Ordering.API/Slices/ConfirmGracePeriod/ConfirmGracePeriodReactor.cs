using LawnDart;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.ConfirmGracePeriod;

/// <summary>
/// When the grace period elapses it hands off to Ordering API, which issues Mark Order Awaiting Validation.
/// </summary>
public sealed class ConfirmGracePeriodReactor : IReactor<GracePeriodConfirmed>
{
    private readonly ILogger<ConfirmGracePeriodReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="logger">Logger used when a confirmation is skipped.</param>
    public ConfirmGracePeriodReactor(ILogger<ConfirmGracePeriodReactor> logger) => _logger = logger;

    /// <summary>
    /// Issues Mark Order Awaiting Validation for the confirmed order.
    /// </summary>
    /// <param name="event">Grace Period Confirmed from OrderProcessor.</param>
    /// <param name="context">Transport message context.</param>
    /// <param name="cancellationToken">Token unused by this handoff.</param>
    /// <returns>
    /// One <see cref="MarkOrderAwaitingValidationCommand"/> for <see cref="GracePeriodConfirmed.OrderId"/>,
    /// or none when the order id is empty.
    /// </returns>
    /// <remarks>
    /// Pass order id.
    /// An empty order id is logged and skipped. Mark Order Awaiting Validation would reject it with <c>ORDER_ID_REQUIRED</c>.
    /// Only a Submitted order moves to AwaitingValidation. Any other status writes no event.
    /// A second confirmation writes no second event once the order has left Submitted.
    /// </remarks>
    public Task<IEnumerable<ICommand>> ReactAsync(
        GracePeriodConfirmed @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        if (@event.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Grace Period Confirmed {EventId} has an empty order id. Skipping.",
                @event.Id);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }

        return Task.FromResult<IEnumerable<ICommand>>(
            [new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), @event.OrderId)]);
    }
}
