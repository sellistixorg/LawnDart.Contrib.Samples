using HarborStreet.Coordination.Events;

namespace HarborStreet.Coordination;

public static class CoordinationEventTypes
{
    public static readonly Type[] All =
    [
        typeof(TitleAddedToCatalog),
        typeof(TitleRestocked),
        typeof(StockReserved),
        typeof(StockReleased),
        typeof(OrderPlaced),
        typeof(PaymentCaptured),
        typeof(PaymentFailed),
        typeof(OrderCancelled),
        typeof(FulfillmentStarted)
    ];
}
