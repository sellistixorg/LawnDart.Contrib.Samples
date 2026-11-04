using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Iteration.Events;

[EventTypeName("HarborStreet.TitleAddedToCatalog")]
public sealed record TitleAddedToCatalog(
    Guid Id,
    DateTime Timestamp,
    Guid TitleId,
    string Name,
    decimal Price,
    bool IsScarce) : IEvent;

[EventTypeName("HarborStreet.WarehouseOpened")]
public sealed record WarehouseOpened(
    Guid Id,
    DateTime Timestamp,
    Guid WarehouseId,
    string Name) : IEvent;

[EventTypeName("HarborStreet.TitleRestockedAtWarehouse")]
public sealed record TitleRestockedAtWarehouse(
    Guid Id,
    DateTime Timestamp,
    Guid WarehouseId,
    Guid TitleId,
    int Quantity) : IEvent;

[EventTypeName("HarborStreet.OrderPlaced")]
public sealed record OrderPlaced(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid CustomerId,
    Guid WarehouseId,
    Guid TitleId,
    int Quantity) : IEvent;

[EventTypeName("HarborStreet.StockReservedAtWarehouse")]
public sealed record StockReservedAtWarehouse(
    Guid Id,
    DateTime Timestamp,
    Guid WarehouseId,
    Guid TitleId,
    Guid OrderId,
    int Quantity) : IEvent;
