using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Checkout.Dcb.Events;

[EventTypeName("HarborStreet.TitleAddedToCatalog")]
public sealed record TitleAddedToCatalog(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    string Name,
    decimal Price,
    bool IsScarce) : IEvent;

[EventTypeName("HarborStreet.TitleRestocked")]
public sealed record TitleRestocked(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    int Quantity) : IEvent;

[EventTypeName("HarborStreet.OrderPlaced")]
public sealed record OrderPlaced(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid CustomerId,
    Guid TitleId,
    int Quantity) : IEvent;

[EventTypeName("HarborStreet.StockReserved")]
public sealed record StockReserved(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    Guid OrderId,
    int Quantity) : IEvent;
