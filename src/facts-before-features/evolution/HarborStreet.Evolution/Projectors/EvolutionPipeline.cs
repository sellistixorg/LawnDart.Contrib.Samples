using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Evolution.Projectors;

/// <summary>
/// Rebuilds all projection families from the event store (replay safety net after upcasters change).
/// </summary>
public sealed class EvolutionPipeline
{
    private readonly EvolutionViewStore _store;
    private readonly CatalogProjectorV1 _catalogV1;
    private readonly CatalogProjectorV2 _catalogV2;
    private readonly OrderHistoryProjectorV1 _ordersV1;
    private readonly OrderHistoryProjectorV2 _ordersV2;

    public EvolutionPipeline(EvolutionViewStore store)
    {
        _store = store;
        _catalogV1 = new CatalogProjectorV1(store);
        _catalogV2 = new CatalogProjectorV2(store);
        _ordersV1 = new OrderHistoryProjectorV1(store);
        _ordersV2 = new OrderHistoryProjectorV2(store);
    }

    public EvolutionViewStore Store => _store;

    public void Apply(IEvent @event)
    {
        _catalogV1.Apply(@event);
        _catalogV2.Apply(@event);
        _ordersV1.Apply(@event);
        _ordersV2.Apply(@event);
    }

// start snippet rebuild-views
    public async Task RebuildAsync(IEventStore eventStore, CancellationToken cancellationToken = default)
    {
        _store.Clear();
        var history = await eventStore.ReadByQueryAsync(Query.All(), cancellationToken: cancellationToken);
        foreach (var recorded in history.Events)
            Apply(recorded.Event);
    }
// end snippet rebuild-views
}
