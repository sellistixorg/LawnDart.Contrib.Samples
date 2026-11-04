using HarborStreet.Evolution.Views;

namespace HarborStreet.Evolution.Projectors;

public sealed class EvolutionViewStore
{
    public Dictionary<Guid, CatalogTitleViewV1> CatalogV1 { get; } = new();
    public Dictionary<Guid, CatalogTitleViewV2> CatalogV2 { get; } = new();
    public Dictionary<Guid, OrderHistoryItemV1> OrdersV1 { get; } = new();
    public Dictionary<Guid, OrderHistoryItemV2> OrdersV2 { get; } = new();

    public void Clear()
    {
        CatalogV1.Clear();
        CatalogV2.Clear();
        OrdersV1.Clear();
        OrdersV2.Clear();
    }
}
