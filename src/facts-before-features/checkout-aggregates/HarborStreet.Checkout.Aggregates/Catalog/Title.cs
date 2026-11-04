using HarborStreet.Checkout.Aggregates.Catalog.Commands;
using HarborStreet.Checkout.Aggregates.Catalog.Events;
using LawnDart;
using LawnDart.Aggregates;

namespace HarborStreet.Checkout.Aggregates.Catalog;

/// <summary>
/// Title aggregate — catalog identity and stock. Reservation is a separate command
/// from Place Order so checkout must choreograph across Title + Order streams.
/// </summary>
public class Title : AggregateRoot<TitleState>
{
    public Title()
    {
        State = new TitleState();
    }

    private Task Handle(AddTitleToCatalogCommand cmd)
    {
        if (State.Exists)
            throw new InvalidOperationException("Title is already in the catalog.");

        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new ArgumentException("Title name is required.", nameof(cmd));

        Apply(new TitleAddedToCatalog(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.Name.Trim(),
            cmd.Price,
            cmd.IsScarce));

        return Task.CompletedTask;
    }

    private Task Handle(RestockTitleCommand cmd)
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");

        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Restock quantity must be positive.");

        Apply(new TitleRestocked(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.Quantity));

        return Task.CompletedTask;
    }

    private Task Handle(ReserveStockCommand cmd)
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");

        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Reserve quantity must be positive.");

        if (State.AvailableUnits < cmd.Quantity)
            throw new InvalidOperationException("Title unavailable.");

        Apply(new StockReserved(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.OrderId,
            cmd.Quantity));

        return Task.CompletedTask;
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
        }
    }
}
