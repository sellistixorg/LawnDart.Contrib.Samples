using HarborStreet.Projections.Events;
using HarborStreet.Projections.Views;
using LawnDart;

namespace HarborStreet.Projections.Projectors;

/// <summary>Projects available / reserved stock for the dashboard.</summary>
public sealed class StockDashboardProjector
{
    private readonly InMemoryViewStore _store;

    public StockDashboardProjector(InMemoryViewStore store) => _store = store;

// start snippet apply-stock-events
    public void Apply(IEvent @event)
    {
        switch (@event)
        {
            case TitleAddedToCatalog e:
                _store.GetOrAddStock(e.TitleId, () => new StockDashboardRow
                {
                    TitleId = e.TitleId,
                    Name = e.Name,
                    IsScarce = e.IsScarce,
                    Available = 0,
                    Reserved = 0
                });
                break;

            case TitleRestocked e:
                var afterRestock = _store.GetOrAddStock(e.TitleId, () => new StockDashboardRow { TitleId = e.TitleId });
                afterRestock.Available += e.Quantity;
                break;

            case StockReserved e:
                var afterReserve = _store.GetOrAddStock(e.TitleId, () => new StockDashboardRow { TitleId = e.TitleId });
                afterReserve.Available -= e.Quantity;
                afterReserve.Reserved += e.Quantity;
                break;
        }
// end snippet apply-stock-events
    }
}
