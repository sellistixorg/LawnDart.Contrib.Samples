using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Checkout.Aggregates.Orders.Events;

[EventTypeName("HarborStreet.OrderPlaced")]
public sealed record OrderPlaced(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid CustomerId,
    Guid TitleId,
    int Quantity) : IEvent;

[EventTypeName("HarborStreet.OrderCancelled")]
public sealed record OrderCancelled(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    string Reason) : IEvent;
