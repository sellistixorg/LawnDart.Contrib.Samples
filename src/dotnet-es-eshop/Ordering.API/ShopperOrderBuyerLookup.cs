using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

namespace LawnDart.Samples.EShop.Ordering.API;

/// <summary>
/// Reads the buyer id from the order stream for the cancel-order HTTP check.
/// </summary>
public sealed class ShopperOrderBuyerLookup : IShopperOrderBuyerLookup
{
    private readonly IAggregateRepository _orders;

    /// <summary>
    /// Creates the lookup.
    /// </summary>
    /// <param name="orders">Order aggregate repository.</param>
    public ShopperOrderBuyerLookup(IAggregateRepository orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        _orders = orders;
    }

    /// <inheritdoc />
    public async Task<Guid?> FindBuyerIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
            return null;

        var order = await _orders.GetOrCreateAsync<Order>(orderId, cancellationToken).ConfigureAwait(false);
        if (order.Version == 0 || order.State.BuyerId == Guid.Empty)
            return null;

        return order.State.BuyerId;
    }
}
