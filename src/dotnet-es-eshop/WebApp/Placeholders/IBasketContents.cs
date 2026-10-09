using LawnDart.Samples.EShop.WebApp.Models;
using Microsoft.AspNetCore.Components;

namespace LawnDart.Samples.EShop.WebApp.Placeholders;

/// <summary>
/// Shopping bag in the header, on the item page, and on the cart page.
/// </summary>
/// <remarks>
/// Reads GET <see cref="ViewEndpoints.BasketContents"/> on the basket client.
/// The response DTO is BasketContentsView.
/// Add posts to /api/basket/add-item-to-basket.
/// A quantity above 0 posts to /api/basket/change-basket-item-quantity.
/// A quantity of 0 posts to /api/basket/remove-item-from-basket.
/// </remarks>
public interface IBasketContents
{
    /// <summary>
    /// Reads the current bag.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The lines. Empty when the shopper is signed out.</returns>
    Task<IReadOnlyCollection<BasketItem>> GetBasketItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one unit of the catalog item.
    /// </summary>
    /// <param name="productId">Catalog item id.</param>
    /// <param name="name">Display name snapshot.</param>
    /// <param name="price">Unit price snapshot.</param>
    /// <param name="pictureUrl">Picture URL snapshot.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task AddAsync(
        Guid productId,
        string name,
        decimal price,
        string? pictureUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the quantity for the item. A quantity of zero removes the line.
    /// </summary>
    /// <param name="productId">Catalog item id.</param>
    /// <param name="quantity">Quantity. Zero removes the line.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task SetQuantityAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="callback"/> after a bag write on this shopper's client.
    /// </summary>
    /// <param name="callback">
    /// Callback that reloads the bag. A component method calls <c>InvokeAsync</c>
    /// so the reload and <c>StateHasChanged</c> run on that component's renderer.
    /// </param>
    /// <returns>A subscription. Dispose it when the component goes away.</returns>
    /// <remarks>
    /// The client is scoped to one request or circuit. A write notifies only the
    /// components that subscribed on that instance. The client awaits each callback.
    /// A listener that throws is logged. The bag write still finishes.
    /// </remarks>
    IDisposable NotifyOnChange(Func<Task> callback);

    /// <summary>
    /// Runs <paramref name="callback"/> after a bag write on this shopper's client.
    /// </summary>
    /// <param name="callback">Callback that reloads the bag.</param>
    /// <returns>A subscription. Dispose it when the component goes away.</returns>
    /// <remarks>
    /// The client runs <paramref name="callback"/> on the renderer that was current
    /// when the component subscribed. A listener that throws is logged. The bag write still finishes.
    /// </remarks>
    IDisposable NotifyOnChange(EventCallback callback);
}
