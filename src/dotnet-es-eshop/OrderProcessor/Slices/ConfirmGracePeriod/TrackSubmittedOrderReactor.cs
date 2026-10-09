using LawnDart;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;

/// <summary>
/// Records Order Started so the grace-period timer can confirm that order.
/// </summary>
public sealed class TrackSubmittedOrderReactor : IReactor<OrderStarted>
{
    private readonly PendingGraceOrders _pending;
    private readonly ILogger<TrackSubmittedOrderReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="pending">Orders waiting out the grace period.</param>
    /// <param name="logger">Logger used when an Order Started message is skipped.</param>
    public TrackSubmittedOrderReactor(
        PendingGraceOrders pending,
        ILogger<TrackSubmittedOrderReactor> logger)
    {
        _pending = pending;
        _logger = logger;
    }

    /// <summary>
    /// Subscribes to Order Started and saves the order id and the event time.
    /// </summary>
    /// <param name="event">Order Started from Ordering.</param>
    /// <param name="context">Transport message context.</param>
    /// <param name="cancellationToken">Token used to cancel the view write.</param>
    /// <returns>No commands. The timer publishes Grace Period Confirmed later.</returns>
    /// <remarks>
    /// The row is saved before this method returns. The inbox acknowledges Order Started only after that.
    /// An empty order id is logged and skipped.
    /// The first event time is kept. A redelivery does not move the clock.
    /// </remarks>
    public async Task<IEnumerable<ICommand>> ReactAsync(
        OrderStarted @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        if (@event.OrderId == Guid.Empty)
        {
            _logger.LogWarning(
                "Order Started {EventId} has an empty order id. Skipping.",
                @event.Id);
            return [];
        }

        await _pending.RememberAsync(@event.OrderId, @event.Timestamp, cancellationToken)
            .ConfigureAwait(false);
        return [];
    }
}
