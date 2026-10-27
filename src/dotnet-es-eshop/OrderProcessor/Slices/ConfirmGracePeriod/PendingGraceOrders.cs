using System.Text.Json;
using LawnDart.Projections.Storage;

namespace LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;

/// <summary>
/// Orders waiting out the grace period, stored as LawnDart views and fed by Order Started.
/// </summary>
/// <remarks>
/// Rows use view type <see cref="ViewType"/> in the LawnDart view table
/// (<c>dbo.ProjectionViews</c> on SQL Server, <c>lawndart.projection_views</c> on Postgres).
/// Ordering projections use other type names in that table.
/// The first Order Started time is kept. A redelivery does not move the clock.
/// A successful publish deletes the row.
/// </remarks>
public sealed class PendingGraceOrders
{
    /// <summary>
    /// View type for pending grace orders. Distinct from Ordering projection names.
    /// </summary>
    public const string ViewType = "order-processor-pending-grace";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IViewStore _views;
    private readonly object _gate = new();
    private readonly HashSet<Guid> _inFlight = [];

    /// <summary>
    /// Creates the list over the host view store.
    /// </summary>
    /// <param name="views">SQL view store for <c>orderingdb</c>, or the in-memory store in tests.</param>
    public PendingGraceOrders(IViewStore views)
    {
        ArgumentNullException.ThrowIfNull(views);
        _views = views;
    }

    /// <summary>
    /// Saves <paramref name="orderId"/> at <paramref name="startedAtUtc"/> when no row exists yet.
    /// </summary>
    /// <param name="orderId">Order id from Order Started.</param>
    /// <param name="startedAtUtc">When that order started.</param>
    /// <param name="cancellationToken">Token used to cancel the view write.</param>
    /// <returns>A task that completes when the row is stored, or immediately when the id is empty or already stored.</returns>
    public async Task RememberAsync(
        Guid orderId,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
            return;

        var instanceId = InstanceId(orderId);
        var existing = await _views.GetViewAsync(ViewType, instanceId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return;

        var utc = startedAtUtc.Kind switch
        {
            DateTimeKind.Utc => startedAtUtc,
            DateTimeKind.Local => startedAtUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc)
        };
        var json = JsonSerializer.Serialize(new StoredGraceOrder(utc), JsonOptions);
        await _views.SaveViewAsync(ViewType, instanceId, json, checkpoint: 0, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Orders whose age is at least <paramref name="grace"/> and that this process is not already publishing.
    /// </summary>
    /// <param name="now">Clock reading from <see cref="TimeProvider"/>.</param>
    /// <param name="grace">GracePeriodTime.</param>
    /// <param name="cancellationToken">Token used to cancel the view read.</param>
    /// <returns>Order ids claimed for this check. Call <see cref="CompleteAsync"/> or <see cref="Release"/> for each.</returns>
    public async Task<List<Guid>> ClaimDueAsync(
        DateTimeOffset now,
        TimeSpan grace,
        CancellationToken cancellationToken = default)
    {
        var rows = await _views.GetViewsByTypeAsync(ViewType, cancellationToken).ConfigureAwait(false);
        var due = new List<Guid>();
        lock (_gate)
        {
            foreach (var (instanceId, viewData) in rows)
            {
                if (!Guid.TryParse(instanceId, out var orderId))
                    continue;
                if (_inFlight.Contains(orderId))
                    continue;

                var stored = JsonSerializer.Deserialize<StoredGraceOrder>(viewData, JsonOptions);
                if (stored is null)
                    continue;

                var startedAt = stored.StartedAtUtc.Kind == DateTimeKind.Utc
                    ? stored.StartedAtUtc
                    : DateTime.SpecifyKind(stored.StartedAtUtc, DateTimeKind.Utc);
                if (now - new DateTimeOffset(startedAt) < grace)
                    continue;

                _inFlight.Add(orderId);
                due.Add(orderId);
            }
        }

        return due;
    }

    /// <summary>
    /// Deletes the row after a successful publish.
    /// </summary>
    /// <param name="orderId">Order id that was published.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    /// <returns>A task that completes when the row is gone.</returns>
    public async Task CompleteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await _views.DeleteViewAsync(ViewType, InstanceId(orderId), cancellationToken).ConfigureAwait(false);
        lock (_gate)
            _inFlight.Remove(orderId);
    }

    /// <summary>
    /// Leaves the row in place after a failed publish so the next check can retry.
    /// </summary>
    /// <param name="orderId">Order id whose publish failed.</param>
    public void Release(Guid orderId)
    {
        lock (_gate)
            _inFlight.Remove(orderId);
    }

    private static string InstanceId(Guid orderId) => orderId.ToString("D");

    private sealed record StoredGraceOrder(DateTime StartedAtUtc);
}
