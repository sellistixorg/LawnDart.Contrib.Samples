using HarborStreet.Checkout.Dcb.Commands;
using HarborStreet.Checkout.Dcb.Events;
using LawnDart;
using LawnDart.Dcb;
using LawnDart.EventStore;

namespace HarborStreet.Checkout.Dcb;

/// <summary>
/// DCB checkout decision for one title.
/// Place Order emits <see cref="OrderPlaced"/> and <see cref="StockReserved"/> in one append.
/// Concurrent last-copy attempts: exactly one wins. The loser gets no events.
/// </summary>
/// <remarks>
/// The decision query is one item: the title tag. Each event is stored with that title tag only,
/// because the title is the event's subject. Order and customer ids live on the event payloads.
/// The repository guards the append with the same query.
/// </remarks>
public class CheckoutDecision : DcbEntity<CheckoutState>
{
    /// <summary>Tag for events about this title.</summary>
    public static string TitleTag(Guid titleId) => $"title:{titleId}";

    /// <summary>
    /// Decision query for one title. One item, so the title tag is the whole boundary.
    /// </summary>
    public static Query ForTitle(Guid titleId) =>
        Query.FromItems(QueryItem.ByTags(TitleTag(titleId)));

    private Task Handle(AddTitleToCatalogCommand cmd)
    {
        if (State.Exists)
            throw new InvalidOperationException("Title is already in the catalog.");

        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new ArgumentException("Title name is required.", nameof(cmd));

        Emit(new TitleAddedToCatalog(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.Name.Trim(),
            cmd.Price,
            cmd.IsScarce), TitleTag(cmd.TitleId));

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
            cmd.Quantity), TitleTag(cmd.TitleId));

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

        // One decision: both facts or neither (atomic DCB append).
        // Each event carries the title tag only. That tag is its subject.
        Emit(new OrderPlaced(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.OrderId,
            cmd.CustomerId,
            cmd.TitleId,
            cmd.Quantity), TitleTag(cmd.TitleId));

        Emit(new StockReserved(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.OrderId,
            cmd.Quantity), TitleTag(cmd.TitleId));

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
