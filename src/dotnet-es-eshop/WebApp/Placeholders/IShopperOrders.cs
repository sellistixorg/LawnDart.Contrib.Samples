using LawnDart.Samples.EShop.WebApp.Models;

namespace LawnDart.Samples.EShop.WebApp.Placeholders;

/// <summary>
/// Order list.
/// </summary>
/// <remarks>
/// Reads GET <see cref="ViewEndpoints.OrdersByShopper"/> on the ordering client.
/// The response DTO is OrdersByShopperView.
/// Buyer id is the shopper account id from the authentication cookie.
/// OrderSummary is GET <see cref="ViewEndpoints.OrderSummary"/>/{orderNumber} when a detail page is added.
/// The response DTO is OrderSummaryView.
/// </remarks>
public interface IShopperOrders
{
    /// <summary>
    /// Reads the signed-in shopper's orders, newest first.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The rows. Empty when the shopper is signed out or the shopper has no orders.</returns>
    Task<OrderRecord[]> GetOrders(CancellationToken cancellationToken = default);
}
