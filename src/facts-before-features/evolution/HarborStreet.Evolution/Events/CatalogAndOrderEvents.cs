using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Evolution.Events;

[EventTypeName("HarborStreet.TitleAddedToCatalog")]
public sealed record TitleAddedToCatalog(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    string Name,
    decimal Price,
    bool IsScarce) : IEvent;

/// <summary>Historical Order Placed shape (no warehouse) — still on the stream forever.</summary>
[EventTypeName("HarborStreet.OrderPlaced")]
public sealed record OrderPlaced(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid CustomerId,
    Guid TitleId,
    int Quantity) : IEvent;

/// <summary>
/// Additive evolution: new writes use V2 type name. Old OrderPlaced rows are upcast at read time.
/// </summary>
[EventTypeName("HarborStreet.OrderPlaced.V2")]
public sealed record OrderPlacedV2(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid CustomerId,
    Guid TitleId,
    int Quantity,
    Guid WarehouseId,
    string? CustomerDisplayName) : IEvent;
