using HarborStreet.Evolution.Evolution;
using HarborStreet.Evolution.Events;
using HarborStreet.Evolution.Views;
using LawnDart;

namespace HarborStreet.Evolution.Projectors;

public sealed class OrderHistoryProjectorV1
{
    private readonly EvolutionViewStore _store;
    public OrderHistoryProjectorV1(EvolutionViewStore store) => _store = store;

    public void Apply(IEvent @event)
    {
        // v1 family only understands the original shape (ignores V2 writes unless upcast first).
        if (@event is OrderPlaced e)
        {
            _store.OrdersV1[e.OrderId] = new OrderHistoryItemV1
            {
                OrderId = e.OrderId,
                CustomerId = e.CustomerId,
                TitleId = e.TitleId,
                Quantity = e.Quantity,
                PlacedAt = e.Timestamp
            };
            return;
        }

        // Optional bridge: V2 writes still appear in v1 history without warehouse.
        if (@event is OrderPlacedV2 v2)
        {
            _store.OrdersV1[v2.OrderId] = new OrderHistoryItemV1
            {
                OrderId = v2.OrderId,
                CustomerId = v2.CustomerId,
                TitleId = v2.TitleId,
                Quantity = v2.Quantity,
                PlacedAt = v2.Timestamp
            };
        }
    }
}

public sealed class OrderHistoryProjectorV2
{
    private readonly EvolutionViewStore _store;
    public OrderHistoryProjectorV2(EvolutionViewStore store) => _store = store;

    public void Apply(IEvent @event)
    {
        if (!OrderPlacedUpcaster.IsOrderPlacedFamily(@event))
            return;

        var canonical = OrderPlacedUpcaster.ToV2(@event);
        _store.OrdersV2[canonical.OrderId] = new OrderHistoryItemV2
        {
            OrderId = canonical.OrderId,
            CustomerId = canonical.CustomerId,
            TitleId = canonical.TitleId,
            Quantity = canonical.Quantity,
            PlacedAt = canonical.Timestamp,
            WarehouseId = canonical.WarehouseId,
            CustomerDisplayName = canonical.CustomerDisplayName
        };
    }
}
