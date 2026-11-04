using HarborStreet.Projections.Events;
using HarborStreet.Projections.Views;
using LawnDart;

namespace HarborStreet.Projections.Projectors;

/// <summary>Projects order history from Order Placed facts.</summary>
public sealed class OrderHistoryProjector
{
    private readonly InMemoryViewStore _store;

    public OrderHistoryProjector(InMemoryViewStore store) => _store = store;

    public void Apply(IEvent @event)
    {
        if (@event is not OrderPlaced e)
            return;

        _store.UpsertOrder(new OrderHistoryItem
        {
            OrderId = e.OrderId,
            CustomerId = e.CustomerId,
            TitleId = e.TitleId,
            Quantity = e.Quantity,
            PlacedAt = e.Timestamp
        });
    }
}
