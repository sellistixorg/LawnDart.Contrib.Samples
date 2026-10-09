using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Tells the orders page that one shopper's order status changed.
/// </summary>
/// <remarks>
/// The registry is process-wide so a circuit can be reached after the render request ends.
/// A buyer message notifies only the listeners registered for that buyer id (the sub claim).
/// A shipped or cancelled message notifies only the listeners whose last orders read contains that order id.
/// Each listener belongs to one circuit. Disposing it removes that circuit only.
/// The callback runs on the renderer that was current when the component subscribed.
/// </remarks>
public sealed class OrderStatusNotificationService(ILogger<OrderStatusNotificationService> logger)
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, List<Subscription>> _byBuyer = [];

    /// <summary>
    /// Runs <paramref name="callback"/> when a status message arrives for <paramref name="buyerId"/>.
    /// </summary>
    /// <param name="buyerId">Shopper account id. The sub claim.</param>
    /// <param name="callback">
    /// Callback that reloads the orders list. A component method calls <c>InvokeAsync</c>
    /// so the reload and <c>StateHasChanged</c> run on that component's renderer.
    /// </param>
    /// <param name="context">
    /// Renderer context captured at subscribe time.
    /// Null uses <see cref="SynchronizationContext.Current"/>.
    /// </param>
    /// <returns>A subscription. Dispose it when the component goes away.</returns>
    public IDisposable Subscribe(Guid buyerId, Func<Task> callback, SynchronizationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return Subscribe(buyerId, new EventCallback(null, callback), context);
    }

    /// <summary>
    /// Runs <paramref name="callback"/> when a status message arrives for <paramref name="buyerId"/>.
    /// </summary>
    /// <param name="buyerId">Shopper account id. The sub claim.</param>
    /// <param name="callback">Callback that reloads the orders list.</param>
    /// <param name="context">
    /// Renderer context captured at subscribe time.
    /// Null uses <see cref="SynchronizationContext.Current"/>.
    /// </param>
    /// <returns>A subscription. Dispose it when the component goes away.</returns>
    /// <remarks>
    /// The callback runs on the renderer that was current when the component subscribed.
    /// </remarks>
    public IDisposable Subscribe(Guid buyerId, EventCallback callback, SynchronizationContext? context = null)
    {
        if (buyerId == Guid.Empty)
            return EmptySubscription.Instance;

        var subscription = new Subscription(this, buyerId, callback, context ?? SynchronizationContext.Current);
        lock (_gate)
        {
            if (!_byBuyer.TryGetValue(buyerId, out var list))
            {
                list = [];
                _byBuyer[buyerId] = list;
            }

            list.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// Stores the order ids from this subscription's latest orders read.
    /// </summary>
    /// <param name="subscription">The subscription returned by <see cref="Subscribe(Guid, EventCallback, SynchronizationContext?)"/>.</param>
    /// <param name="orderIds">Order ids on the list that read just returned. Null is an empty set.</param>
    /// <remarks>
    /// The read is the one made with that shopper's token.
    /// A later shipped or cancelled message uses this set.
    /// </remarks>
    public void RememberOrders(IDisposable subscription, IEnumerable<Guid>? orderIds)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (subscription is not Subscription typed)
            return;

        typed.ReplaceOrders(orderIds ?? []);
    }

    /// <summary>
    /// Runs the listeners whose latest orders read contains <paramref name="orderId"/>.
    /// </summary>
    /// <param name="orderId">Order id from a shipped or cancelled message.</param>
    /// <returns>A task that finishes after each matching listener has been awaited.</returns>
    /// <remarks>
    /// An order id that no page loaded does nothing.
    /// A listener that throws is logged. The next listener still runs.
    /// </remarks>
    public Task NotifyListedOrderAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            return Task.CompletedTask;

        List<Subscription> snapshot;
        lock (_gate)
        {
            snapshot = [];
            foreach (var list in _byBuyer.Values)
            {
                foreach (var subscription in list)
                {
                    if (subscription.ContainsOrder(orderId))
                        snapshot.Add(subscription);
                }
            }
        }

        return NotifyAllAsync(snapshot);
    }

    /// <summary>
    /// Runs the listeners registered for <paramref name="buyerId"/>.
    /// </summary>
    /// <param name="buyerId">Shopper account id. The sub claim.</param>
    /// <returns>A task that finishes after each listener has been awaited.</returns>
    /// <remarks>
    /// A listener that throws is logged. The next listener still runs.
    /// An empty buyer id does nothing.
    /// </remarks>
    public async Task NotifyOrderStatusChangedAsync(Guid buyerId)
    {
        if (buyerId == Guid.Empty)
            return;

        List<Subscription> snapshot;
        lock (_gate)
        {
            if (!_byBuyer.TryGetValue(buyerId, out var list) || list.Count == 0)
                return;

            snapshot = [.. list];
        }

        await NotifyAllAsync(snapshot).ConfigureAwait(false);
    }

    private async Task NotifyAllAsync(List<Subscription> snapshot)
    {
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
                logger.LogError(ex, "Order status notification failed.");
            }
        }
    }

    private void Unsubscribe(Subscription subscription)
    {
        lock (_gate)
        {
            if (!_byBuyer.TryGetValue(subscription.BuyerId, out var list))
                return;

            list.Remove(subscription);
            if (list.Count == 0)
                _byBuyer.Remove(subscription.BuyerId);
        }
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

    private sealed class Subscription : IDisposable
    {
        private readonly OrderStatusNotificationService _owner;
        private readonly object _ordersGate = new();
        private readonly EventCallback _callback;
        private readonly SynchronizationContext? _context;
        private HashSet<Guid> _orders = [];
        private int _disposed;

        public Subscription(
            OrderStatusNotificationService owner,
            Guid buyerId,
            EventCallback callback,
            SynchronizationContext? context)
        {
            _owner = owner;
            BuyerId = buyerId;
            _callback = callback;
            _context = context;
        }

        public Guid BuyerId { get; }

        public void ReplaceOrders(IEnumerable<Guid> orderIds)
        {
            var next = new HashSet<Guid>();
            foreach (var orderId in orderIds)
            {
                if (orderId != Guid.Empty)
                    next.Add(orderId);
            }

            lock (_ordersGate)
                _orders = next;
        }

        public bool ContainsOrder(Guid orderId)
        {
            lock (_ordersGate)
                return _orders.Contains(orderId);
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

    private sealed class EmptySubscription : IDisposable
    {
        public static readonly EmptySubscription Instance = new();

        public void Dispose()
        {
        }
    }
}
