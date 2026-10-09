using System.Net.Http.Json;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Shopper order list / history (GET api/orders).
/// </summary>
public sealed class OrdersByShopperClient(
    IHttpClientFactory httpClientFactory,
    ShopperSession shopper) : IShopperOrders
{
    /// <summary>
    /// Reads the signed-in shopper's orders, newest first.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The rows. Empty when the shopper is signed out.</returns>
    /// <remarks>
    /// GET /views/orders-by-shopper?buyerId= on the ordering HTTP client.
    /// Buyer id is the shopper account id from the authentication cookie.
    /// The page catches a refused connection, unreadable JSON, a timeout, and an unsupported address.
    /// Cancellation of <paramref name="cancellationToken"/> still throws.
    /// This client keeps no order rows.
    /// </remarks>
    public async Task<OrderRecord[]> GetOrders(CancellationToken cancellationToken = default)
    {
        if (shopper.AccountId is not Guid buyerId)
            return [];

        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Ordering);
        if (client.BaseAddress is null)
            throw new NotSupportedException("The ordering client has no base address.");

        using var response = await client.GetAsync(
            $"{ViewEndpoints.OrdersByShopper}?buyerId={buyerId:D}",
            cancellationToken).ConfigureAwait(false);
        ShopperSessionEndedException.ThrowIfEnded(response);
        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<OrdersByShopperView>(cancellationToken)
            .ConfigureAwait(false);
        return Map(view);
    }

    private static OrderRecord[] Map(OrdersByShopperView? view)
    {
        if (view?.Orders is null || view.Orders.Count == 0)
            return [];

        var records = new OrderRecord[view.Orders.Count];
        for (var i = 0; i < view.Orders.Count; i++)
        {
            var row = view.Orders[i];
            records[i] = new OrderRecord(row.Id, row.Date, row.Status, row.Total);
        }

        return records;
    }
}
