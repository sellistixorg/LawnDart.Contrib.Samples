using HarborStreet.Checkout.Dcb.Events;

namespace HarborStreet.Checkout.Dcb;

public static class CheckoutEventTypes
{
    public static readonly Type[] All =
    [
        typeof(TitleAddedToCatalog),
        typeof(TitleRestocked),
        typeof(OrderPlaced),
        typeof(StockReserved)
    ];
}
