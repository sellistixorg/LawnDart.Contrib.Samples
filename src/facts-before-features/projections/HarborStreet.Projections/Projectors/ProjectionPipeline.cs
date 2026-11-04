using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Projections.Projectors;

/// <summary>
/// Catch-up style rebuild: read all events (or by query) and fan out to projectors.
/// Teaching stand-in for a live projection runner — same Apply methods, full replay.
/// </summary>
public sealed class ProjectionPipeline
{
    private readonly InMemoryViewStore _store;
    private readonly CatalogViewProjector _catalog;
    private readonly StockDashboardProjector _stock;
    private readonly OrderHistoryProjector _orders;

    public ProjectionPipeline(InMemoryViewStore store)
    {
        _store = store;
        _catalog = new CatalogViewProjector(store);
        _stock = new StockDashboardProjector(store);
        _orders = new OrderHistoryProjector(store);
    }

    public InMemoryViewStore Store => _store;

    public void Apply(IEvent @event)
    {
        _catalog.Apply(@event);
        _stock.Apply(@event);
        _orders.Apply(@event);
    }

// start snippet rebuild-from-store
    /// <summary>Clear views and rebuild from the entire store (replay safety net).</summary>
    public async Task RebuildAsync(IEventStore eventStore, CancellationToken cancellationToken = default)
    {
        _store.Clear();
        var history = await eventStore.ReadByQueryAsync(Query.All(), cancellationToken: cancellationToken);
        foreach (var recorded in history.Events)
            Apply(recorded.Event);
    }
// end snippet rebuild-from-store
}
