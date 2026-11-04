using HarborStreet.Iteration.Events;

namespace HarborStreet.Iteration;

public static class IterationEventTypes
{
    public static readonly Type[] All =
    [
        typeof(TitleAddedToCatalog),
        typeof(WarehouseOpened),
        typeof(TitleRestockedAtWarehouse),
        typeof(OrderPlaced),
        typeof(StockReservedAtWarehouse)
    ];
}
