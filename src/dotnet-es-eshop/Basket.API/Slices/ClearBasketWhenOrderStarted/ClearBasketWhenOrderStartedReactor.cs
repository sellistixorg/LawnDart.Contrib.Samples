using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Messaging;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Basket.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Slices.ClearBasketWhenOrderStarted;

/// <summary>
/// Checkout handoff: when Ordering emits Order Started, issue Clear Basket. Maps to OrderStartedIntegrationEventHandler.
/// </summary>
public sealed class ClearBasketWhenOrderStartedReactor : IReactor<OrderStarted>
{
    /// <summary>
    /// Why the basket was cleared. Pass buyer/user id from Order Started; reason OrderStarted.
    /// </summary>
    public const string Reason = "OrderStarted";

    private readonly IAggregateRepository _baskets;
    private readonly ILogger<ClearBasketWhenOrderStartedReactor> _logger;

    /// <summary>
    /// Creates the reactor.
    /// </summary>
    /// <param name="baskets">Aggregate repository for the basket stream.</param>
    /// <param name="logger">Logger used when an Order Started message is skipped.</param>
    public ClearBasketWhenOrderStartedReactor(
        IAggregateRepository baskets,
        ILogger<ClearBasketWhenOrderStartedReactor> logger)
    {
        _baskets = baskets;
        _logger = logger;
    }

    /// <summary>
    /// Subscribes to Ordering Order Started; issues Clear Basket for that buyer.
    /// </summary>
    /// <param name="event">Order Started from Ordering.</param>
    /// <param name="context">Transport message context.</param>
    /// <param name="cancellationToken">Token used to cancel the basket load.</param>
    /// <returns>
    /// One Clear Basket command for <see cref="OrderStarted.BuyerId"/>, or none when the message is skipped.
    /// </returns>
    /// <remarks>
    /// Pass buyer/user id from Order Started; reason OrderStarted.
    /// <see cref="OrderStarted.BuyerId"/> is the basket stream id <c>Basket:{buyerId}</c>.
    /// An empty buyer id is logged and skipped.
    /// A buyer with no basket is skipped. Clear Basket rejects an unknown basket, and this delivery must not be retried.
    /// A basket that is already empty is issued to Clear Basket, which writes no event.
    /// A redelivered Order Started does not fail.
    /// </remarks>
    public async Task<IEnumerable<ICommand>> ReactAsync(
        OrderStarted @event,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        if (@event.BuyerId == Guid.Empty)
        {
            _logger.LogWarning(
                "Order Started {EventId} has an empty buyer id. Skipping.",
                @event.Id);
            return [];
        }

        var basket = await _baskets
            .GetOrCreateAsync<BasketAggregate>(@event.BuyerId, cancellationToken)
            .ConfigureAwait(false);
        if (basket.Version == 0)
        {
            _logger.LogInformation(
                "Order Started {EventId} for buyer {BuyerId} has no basket. Skipping.",
                @event.Id,
                @event.BuyerId);
            return [];
        }

        return [new ClearBasketCommand(Guid.NewGuid(), @event.BuyerId, Reason)];
    }
}
