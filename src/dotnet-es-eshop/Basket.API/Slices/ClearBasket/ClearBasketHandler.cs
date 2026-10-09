using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Basket.Commands;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Slices.ClearBasket;

/// <summary>
/// Deletes all lines for the shopper basket.
/// </summary>
public sealed class ClearBasketHandler : ICommandHandler<ClearBasketCommand>
{
    private readonly IAggregateRepository _baskets;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="baskets">Aggregate repository for the basket stream.</param>
    public ClearBasketHandler(IAggregateRepository baskets) => _baskets = baskets;

    /// <summary>
    /// Deletes all lines for the shopper basket.
    /// </summary>
    /// <param name="command">Clear Basket.</param>
    /// <param name="cancellationToken">Token used to cancel the load and the write.</param>
    /// <returns>A task that completes when the basket event is written.</returns>
    public async Task HandleAsync(
        ClearBasketCommand command,
        CancellationToken cancellationToken = default)
    {
        var basket = await _baskets
            .GetOrCreateAsync<BasketAggregate>(command.BuyerId, cancellationToken)
            .ConfigureAwait(false);
        await _baskets
            .HandleCommandAsync(basket, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
