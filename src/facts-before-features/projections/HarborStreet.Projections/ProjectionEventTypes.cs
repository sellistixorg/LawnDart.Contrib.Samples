using HarborStreet.Projections.Events;

namespace HarborStreet.Projections;

public static class ProjectionEventTypes
{
    public static readonly Type[] All =
    [
        typeof(TitleAddedToCatalog),
        typeof(TitleRestocked),
        typeof(TitlePriceUpdated),
        typeof(OrderPlaced),
        typeof(StockReserved)
    ];
}
