using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Shopping bag in the header, on the item page, and on the cart page.
/// </summary>
/// <remarks>
/// The client is scoped. A bag write notifies only the components that subscribed on that instance.
/// </remarks>
public sealed class BasketContentsClient(
    IHttpClientFactory httpClientFactory,
    ShopperSession shopper,
    ILogger<BasketContentsClient> logger) : IBasketContents
{
    private readonly object _gate = new();
    private readonly List<BasketSubscription> _subscriptions = [];

    /// <summary>
    /// Reads the current bag.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The lines. An empty bag when the shopper is signed out.</returns>
    /// <remarks>
    /// GET /views/basket-contents?buyerId= on the basket HTTP client.
    /// Buyer id is the shopper account id from the authentication cookie.
    /// The page catches a refused connection, unreadable JSON, a timeout, and an unsupported address.
    /// Cancellation of <paramref name="cancellationToken"/> still throws.
    /// This client keeps no basket lines.
    /// </remarks>
    public async Task<IReadOnlyCollection<BasketItem>> GetBasketItemsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!TryBuyerId(out var buyerId))
            return [];

        return await ReadAsync(buyerId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds one unit of the catalog item.
    /// </summary>
    /// <param name="productId">Catalog item id.</param>
    /// <param name="name">Display name snapshot.</param>
    /// <param name="price">Unit price snapshot.</param>
    /// <param name="pictureUrl">Picture URL snapshot.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <remarks>
    /// POST /api/basket/add-item-to-basket.
    /// A signed-out shopper does not post.
    /// A domain rejection throws <see cref="BasketCommandRejectedException"/> with shopper-facing text and refreshes listeners.
    /// A concurrency conflict uses a fixed retry message and does not show the response body.
    /// A refused connection, a timeout, or an unsupported address still throws.
    /// </remarks>
    public async Task AddAsync(
        Guid productId,
        string name,
        decimal price,
        string? pictureUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuyerId(out var buyerId))
            return;

        var before = await ReadAsync(buyerId, cancellationToken).ConfigureAwait(false);
        var current = QuantityOf(before, productId);
        await PostAsync(
            "/api/basket/add-item-to-basket",
            new
            {
                Id = Guid.NewGuid(),
                BuyerId = buyerId,
                ProductId = productId,
                ProductName = name,
                UnitPrice = price,
                Quantity = 1,
                PictureUrl = string.IsNullOrWhiteSpace(pictureUrl) ? null : pictureUrl
            },
            cancellationToken).ConfigureAwait(false);

        await WaitUntilAsync(
            buyerId,
            items => QuantityOf(items, productId) == current + 1,
            cancellationToken).ConfigureAwait(false);
        await NotifyAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the quantity for the item. A quantity of zero removes the line.
    /// </summary>
    /// <param name="productId">Catalog item id.</param>
    /// <param name="quantity">Quantity. Zero removes the line.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <remarks>
    /// A quantity above 0 posts to /api/basket/change-basket-item-quantity.
    /// A quantity of 0 posts to /api/basket/remove-item-from-basket.
    /// A signed-out shopper does not post.
    /// A domain rejection throws <see cref="BasketCommandRejectedException"/> with shopper-facing text and refreshes listeners.
    /// A concurrency conflict uses a fixed retry message and does not show the response body.
    /// A refused connection, a timeout, or an unsupported address still throws.
    /// </remarks>
    public async Task SetQuantityAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuyerId(out var buyerId))
            return;

        if (quantity > 0)
        {
            await PostAsync(
                "/api/basket/change-basket-item-quantity",
                new
                {
                    Id = Guid.NewGuid(),
                    BuyerId = buyerId,
                    ProductId = productId,
                    Quantity = quantity
                },
                cancellationToken).ConfigureAwait(false);
            await WaitUntilAsync(
                buyerId,
                items => QuantityOf(items, productId) == quantity,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await PostAsync(
                "/api/basket/remove-item-from-basket",
                new
                {
                    Id = Guid.NewGuid(),
                    BuyerId = buyerId,
                    ProductId = productId
                },
                cancellationToken).ConfigureAwait(false);
            await WaitUntilAsync(
                buyerId,
                items => QuantityOf(items, productId) == 0,
                cancellationToken).ConfigureAwait(false);
        }

        await NotifyAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <paramref name="callback"/> after a bag write on this shopper's client.
    /// </summary>
    /// <param name="callback">
    /// Callback that reloads the bag. A component method calls <c>InvokeAsync</c>
    /// so the reload and <c>StateHasChanged</c> run on that component's renderer.
    /// </param>
    /// <returns>A subscription. Dispose it when the component goes away.</returns>
    public IDisposable NotifyOnChange(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return Subscribe(new EventCallback(null, callback));
    }

    /// <summary>
    /// Runs <paramref name="callback"/> after a bag write on this shopper's client.
    /// </summary>
    /// <param name="callback">Callback that reloads the bag.</param>
    /// <returns>A subscription. Dispose it when the component goes away.</returns>
    /// <remarks>
    /// The callback runs on the renderer that was current when the component subscribed.
    /// </remarks>
    public IDisposable NotifyOnChange(EventCallback callback)
        => Subscribe(callback);

    private IDisposable Subscribe(EventCallback callback)
    {
        var subscription = new BasketSubscription(this, callback, SynchronizationContext.Current);
        lock (_gate)
            _subscriptions.Add(subscription);

        return subscription;
    }

    private bool TryBuyerId(out Guid buyerId)
    {
        if (shopper.AccountId is Guid id)
        {
            buyerId = id;
            return true;
        }

        buyerId = Guid.Empty;
        return false;
    }

    private async Task<IReadOnlyList<BasketItem>> ReadAsync(Guid buyerId, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        using var response = await client.GetAsync(
            $"{ViewEndpoints.BasketContents}?buyerId={buyerId:D}",
            cancellationToken).ConfigureAwait(false);
        ShopperSessionEndedException.ThrowIfEnded(response);
        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<BasketContentsView>(cancellationToken)
            .ConfigureAwait(false);
        return Map(view);
    }

    private async Task PostAsync<T>(string route, T body, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync(route, body, cancellationToken).ConfigureAwait(false);
        ShopperSessionEndedException.ThrowIfEnded(response);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            await NotifyAsync().ConfigureAwait(false);
            throw new BasketCommandRejectedException("Your bag changed. Please try again.");
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var message = await ReadRejectionAsync(response, cancellationToken).ConfigureAwait(false);
            await NotifyAsync().ConfigureAwait(false);
            throw new BasketCommandRejectedException(message);
        }

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> ReadRejectionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String)
            {
                return MapRejection(title.GetString());
            }
        }
        catch (JsonException)
        {
            // An unreadable body still uses the generic rejection.
        }
        catch (NotSupportedException)
        {
            // An unreadable body still uses the generic rejection.
        }

        return GenericRejection;
    }

    private const string GenericRejection = "That change could not be made.";

    private static string MapRejection(string? code) => code switch
    {
        "BASKET_QUANTITY_INVALID" => "Quantity must be at least 1.",
        "BASKET_NOT_FOUND" => "That bag was not found.",
        "BASKET_ITEM_NOT_FOUND" => "That item is not in your bag.",
        _ => GenericRejection
    };

    private async Task WaitUntilAsync(
        Guid buyerId,
        Func<IReadOnlyList<BasketItem>, bool> matches,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var items = await ReadAsync(buyerId, cancellationToken).ConfigureAwait(false);
            if (matches(items))
                return;

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Basket);
        if (client.BaseAddress is null)
            throw new NotSupportedException("The basket client has no base address.");

        return client;
    }

    private static List<BasketItem> Map(BasketContentsView? view)
    {
        if (view?.Items is null || view.Items.Count == 0)
            return [];

        var items = new List<BasketItem>(view.Items.Count);
        foreach (var item in view.Items)
        {
            items.Add(new BasketItem
            {
                Id = item.ProductId.ToString("D"),
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                PictureUrl = item.PictureUrl
            });
        }

        return items;
    }

    private static int QuantityOf(IReadOnlyList<BasketItem> items, Guid productId)
    {
        foreach (var item in items)
        {
            if (item.ProductId == productId)
                return item.Quantity;
        }

        return 0;
    }

    private async Task NotifyAsync()
    {
        List<BasketSubscription> snapshot;
        lock (_gate)
            snapshot = [.. _subscriptions];

        foreach (var subscription in snapshot)
        {
            try
            {
                await subscription.NotifyAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The listener went away during the refresh.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Basket change notification failed.");
            }
        }
    }

    private void Unsubscribe(BasketSubscription subscription)
    {
        lock (_gate)
            _subscriptions.Remove(subscription);
    }

    private static Task InvokeOnRenderer(SynchronizationContext? context, Func<Task> work)
    {
        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
            return work();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            context.Post(static state =>
            {
                var posted = (NotifyWork)state!;
                try
                {
                    var pending = posted.Work();
                    if (pending.IsCompleted)
                    {
                        Finish(posted.Completion, pending);
                        return;
                    }

                    pending.ContinueWith(
                        static (task, boxed) => Finish(((NotifyWork)boxed!).Completion, task),
                        posted,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    posted.Completion.TrySetException(ex);
                }
            }, new NotifyWork(work, completion));
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }

        return completion.Task;
    }

    private static void Finish(TaskCompletionSource completion, Task task)
    {
        if (task.IsCanceled)
            completion.TrySetCanceled();
        else if (task.IsFaulted)
            completion.TrySetException(task.Exception!.InnerExceptions);
        else
            completion.TrySetResult();
    }

    private sealed class NotifyWork(Func<Task> work, TaskCompletionSource completion)
    {
        public Func<Task> Work { get; } = work;

        public TaskCompletionSource Completion { get; } = completion;
    }

    private sealed class BasketSubscription : IDisposable
    {
        private readonly BasketContentsClient _owner;
        private readonly EventCallback _callback;
        private readonly SynchronizationContext? _context;
        private int _disposed;

        public BasketSubscription(
            BasketContentsClient owner,
            EventCallback callback,
            SynchronizationContext? context)
        {
            _owner = owner;
            _callback = callback;
            _context = context;
        }

        public Task NotifyAsync()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return Task.CompletedTask;

            // Component state has to run on the renderer that subscribed.
            return InvokeOnRenderer(_context, () => _callback.InvokeAsync());
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _owner.Unsubscribe(this);
        }
    }
}
