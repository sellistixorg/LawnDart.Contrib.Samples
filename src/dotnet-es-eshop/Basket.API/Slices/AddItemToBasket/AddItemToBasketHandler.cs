using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Basket.Commands;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Slices.AddItemToBasket;

/// <summary>
/// Adds a product line to the shopper basket with quantity and price snapshot.
/// </summary>
public sealed class AddItemToBasketHandler : ICommandHandler<AddItemToBasketCommand>
{
    private readonly IAggregateRepository _baskets;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="baskets">Aggregate repository for the basket stream.</param>
    public AddItemToBasketHandler(IAggregateRepository baskets) => _baskets = baskets;

    /// <summary>
    /// Adds a product line to the shopper basket with quantity and price snapshot.
    /// </summary>
    /// <param name="command">Add Item To Basket.</param>
    /// <param name="cancellationToken">Token used to cancel the load and the write.</param>
    /// <returns>A task that completes when the basket event is written.</returns>
    public async Task HandleAsync(
        AddItemToBasketCommand command,
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
