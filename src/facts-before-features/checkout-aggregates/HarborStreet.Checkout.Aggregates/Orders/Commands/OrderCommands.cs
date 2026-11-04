using LawnDart;

namespace HarborStreet.Checkout.Aggregates.Orders.Commands;

/// <summary>Place Order — commits the order stream only (stock is a separate Title command).</summary>
public record PlaceOrderCommand(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    Guid TitleId,
    int Quantity) : ICommand;

/// <summary>
/// Cancel an order that placed but failed to reserve stock (choreography compensation).
/// </summary>
public record CancelOrderForFailedReservationCommand(
    Guid Id,
    Guid OrderId,
    string Reason) : ICommand;
