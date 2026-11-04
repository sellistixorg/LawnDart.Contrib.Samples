using HarborStreet.Projections.Views;

namespace HarborStreet.Projections.Projectors;

/// <summary>
/// In-memory read models for the chapter sample. Rebuild by clearing and replaying events.
/// </summary>
public sealed class InMemoryViewStore
{
    private readonly Dictionary<Guid, CatalogTitleView> _catalog = new();
    private readonly Dictionary<Guid, StockDashboardRow> _stock = new();
    private readonly Dictionary<Guid, OrderHistoryItem> _orders = new();

    public IReadOnlyDictionary<Guid, CatalogTitleView> Catalog => _catalog;
    public IReadOnlyDictionary<Guid, StockDashboardRow> Stock => _stock;
    public IReadOnlyDictionary<Guid, OrderHistoryItem> Orders => _orders;

    public void Clear()
    {
        _catalog.Clear();
        _stock.Clear();
        _orders.Clear();
    }

    public CatalogTitleView GetOrAddCatalog(Guid titleId, Func<CatalogTitleView> factory)
    {
        if (!_catalog.TryGetValue(titleId, out var view))
        {
            view = factory();
            _catalog[titleId] = view;
        }

        return view;
    }

    public StockDashboardRow GetOrAddStock(Guid titleId, Func<StockDashboardRow> factory)
    {
        if (!_stock.TryGetValue(titleId, out var row))
        {
            row = factory();
            _stock[titleId] = row;
        }

        return row;
    }

    public void UpsertOrder(OrderHistoryItem item) => _orders[item.OrderId] = item;
}
