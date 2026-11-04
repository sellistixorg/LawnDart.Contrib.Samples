using LawnDart;

namespace HarborStreet.Checkout.Aggregates.Catalog.Commands;

/// <summary>Add Title to Catalog</summary>
public record AddTitleToCatalogCommand(
    Guid Id,
    Guid TitleId,
    string Name,
    decimal Price,
    bool IsScarce = false) : ICommand;

/// <summary>Restock Title</summary>
public record RestockTitleCommand(
    Guid Id,
    Guid TitleId,
    int Quantity) : ICommand;

/// <summary>
/// Reserve stock for an order (choreography step after Order Placed).
/// Maps to event <c>Stock Reserved</c> on the Title stream.
/// </summary>
public record ReserveStockCommand(
    Guid Id,
    Guid TitleId,
    Guid OrderId,
    int Quantity) : ICommand;
