using HarborStreet.Checkout.Aggregates.Catalog.Events;
using HarborStreet.Checkout.Aggregates.Orders.Events;

namespace HarborStreet.Checkout.Aggregates;

public static class CheckoutEventTypes
{
    public static readonly Type[] All =
    [
        typeof(TitleAddedToCatalog),
        typeof(TitleRestocked),
        typeof(StockReserved),
        typeof(OrderPlaced),
        typeof(OrderCancelled)
    ];
}
