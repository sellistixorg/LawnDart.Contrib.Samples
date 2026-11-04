using HarborStreet.Checkout.Dcb.Commands;
using HarborStreet.Checkout.Dcb.Events;
using LawnDart;
using LawnDart.Dcb;

namespace HarborStreet.Checkout.Dcb;

/// <summary>
/// DCB checkout decision over tag <c>title:{titleId}</c>.
/// Place Order emits <see cref="OrderPlaced"/> and <see cref="StockReserved"/> in one append.
/// Concurrent last-copy attempts: exactly one wins via AppendCondition; loser gets no events.
/// </summary>
/// <remarks>
/// Events are tagged with the title tag only. The repository appends with FailIfMatches on the
/// load tags (ConsistencyTags), so concurrent Place Order calls contest the shared scarce unit. Order/customer ids live on the event payloads.
/// </remarks>
public class CheckoutDecision : DcbEntity<CheckoutState>
{
    public static string[] TitleTags(Guid titleId) => [$"title:{titleId}"];

    private Task Handle(AddTitleToCatalogCommand cmd)
    {
        if (State.Exists)
            throw new InvalidOperationException("Title is already in the catalog.");

        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new ArgumentException("Title name is required.", nameof(cmd));

        var tags = TitleTags(cmd.TitleId);
        Emit(new TitleAddedToCatalog(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.Name.Trim(),
            cmd.Price,
            cmd.IsScarce), tags);

        return Task.CompletedTask;
    }

    private Task Handle(RestockTitleCommand cmd)
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");

        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Restock quantity must be positive.");

        Emit(new TitleRestocked(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.Quantity), TitleTags(cmd.TitleId));

        return Task.CompletedTask;
    }

// start snippet place-order
    private Task Handle(PlaceOrderCommand cmd)
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");

        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Quantity must be positive.");

        if (State.AvailableUnits < cmd.Quantity)
            throw new InvalidOperationException("Title unavailable.");

        var tags = TitleTags(cmd.TitleId);

        // One decision: both facts or neither (atomic DCB append).
        Emit(new OrderPlaced(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.OrderId,
            cmd.CustomerId,
            cmd.TitleId,
            cmd.Quantity), tags);

        Emit(new StockReserved(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.OrderId,
            cmd.Quantity), tags);

        return Task.CompletedTask;
// end snippet place-order
    }

    protected override void ApplyEventToState(IEvent @event)
    {
        switch (@event)
        {
            case TitleAddedToCatalog e:
                State.TitleId = e.TitleId;
                State.Name = e.Name;
                State.Price = e.Price;
                State.IsScarce = e.IsScarce;
                State.AvailableUnits = 0;
                State.ReservedUnits = 0;
                break;

            case TitleRestocked e:
                State.AvailableUnits += e.Quantity;
                break;

            case StockReserved e:
                State.AvailableUnits -= e.Quantity;
                State.ReservedUnits += e.Quantity;
                break;

            case OrderPlaced:
                // Commitment fact; stock impact is on StockReserved.
                break;
        }
    }
}
