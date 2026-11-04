using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.EndToEnd.Events;

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

[EventTypeName("HarborStreet.StockReleased")]
public sealed record StockReleased(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    Guid OrderId,
    int Quantity) : IEvent;

[EventTypeName("HarborStreet.PaymentCaptured")]
public sealed record PaymentCaptured(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    string PaymentReference) : IEvent;

[EventTypeName("HarborStreet.PaymentFailed")]
public sealed record PaymentFailed(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    string Reason) : IEvent;

[EventTypeName("HarborStreet.OrderCancelled")]
public sealed record OrderCancelled(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    string Reason) : IEvent;

[EventTypeName("HarborStreet.FulfillmentStarted")]
public sealed record FulfillmentStarted(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId) : IEvent;
