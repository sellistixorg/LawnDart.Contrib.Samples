using HarborStreet.EndToEnd.Events;

namespace HarborStreet.EndToEnd;

public static class EndToEndEventTypes
{
    public static readonly Type[] All =
    [
        typeof(TitleAddedToCatalog),
        typeof(TitleRestocked),
        typeof(OrderPlaced),
        typeof(StockReserved),
        typeof(StockReleased),
        typeof(PaymentCaptured),
        typeof(PaymentFailed),
        typeof(OrderCancelled),
        typeof(FulfillmentStarted)
    ];
}
