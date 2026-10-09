namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Reads the buyer id of an order for the cancel-order HTTP check.
/// </summary>
public interface IShopperOrderBuyerLookup
{
    /// <summary>
    /// Returns the buyer id stored on the order.
    /// </summary>
    /// <param name="orderId">Order id from the cancel command.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The buyer id, or null when that order does not exist.</returns>
    Task<Guid?> FindBuyerIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}
