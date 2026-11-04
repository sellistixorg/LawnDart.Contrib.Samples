using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Catalog.Events;

/// <summary>Title Added to Catalog</summary>
[EventTypeName("HarborStreet.TitleAddedToCatalog")]
public sealed record TitleAddedToCatalog(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    string Name,
    decimal Price,
    bool IsScarce) : IEvent;

/// <summary>Title Price Updated</summary>
[EventTypeName("HarborStreet.TitlePriceUpdated")]
public sealed record TitlePriceUpdated(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    decimal NewPrice) : IEvent;

/// <summary>Title Restocked</summary>
[EventTypeName("HarborStreet.TitleRestocked")]
public sealed record TitleRestocked(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    int Quantity) : IEvent;

/// <summary>Title Marked Scarce</summary>
[EventTypeName("HarborStreet.TitleMarkedScarce")]
public sealed record TitleMarkedScarce(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId) : IEvent;
