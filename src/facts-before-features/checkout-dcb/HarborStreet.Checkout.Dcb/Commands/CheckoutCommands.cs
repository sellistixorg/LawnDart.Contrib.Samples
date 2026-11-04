using LawnDart;

namespace HarborStreet.Checkout.Dcb.Commands;

/// <summary>Add Title to Catalog (seed / catalog slice on the title tag).</summary>
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
/// Place Order — one decision: Order Placed + Stock Reserved (or reject / conflict).
/// </summary>
public record PlaceOrderCommand(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    Guid TitleId,
    int Quantity) : ICommand;
