using LawnDart;
using LawnDart.EventStore;

namespace HarborStreet.Evolution.BuiltInVersioning;

// Second way to version Order Placed: keep ONE event name and number the shapes.
// The stored record carries the version. The tooling picks the matching type on read,
// then runs the registered upcaster so readers only ever see the current shape.

// start snippet order-placed-versions
/// <summary>Yesterday's Order Placed (no warehouse). Version 1 of the same event name.</summary>
[EventTypeName("HarborStreet.OrderPlaced", version: 1)]
public sealed record OrderPlacedV1(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid CustomerId,
    Guid TitleId,
    int Quantity) : IEvent;

/// <summary>Today's Order Placed. Version 2 of the same event name, marked current.</summary>
[EventTypeName("HarborStreet.OrderPlaced", version: 2, current: true)]
public sealed record OrderPlaced(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid CustomerId,
    Guid TitleId,
    int Quantity,
    Guid WarehouseId,
    string? CustomerDisplayName) : IEvent;
// end snippet order-placed-versions
