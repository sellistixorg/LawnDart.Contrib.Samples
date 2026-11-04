using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Checkout.Aggregates.Catalog.Events;

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

/// <summary>Stock Reserved — emitted on Title after a successful reserve step.</summary>
[EventTypeName("HarborStreet.StockReserved")]
public sealed record StockReserved(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    Guid OrderId,
    int Quantity) : IEvent;
