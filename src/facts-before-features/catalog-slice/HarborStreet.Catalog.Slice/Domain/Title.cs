using HarborStreet.Catalog.Slice.Domain.Commands;
using HarborStreet.Catalog.Slice.Domain.Events;
using LawnDart;
using LawnDart.Aggregates;

namespace HarborStreet.Catalog.Slice.Domain;

/// <summary>
/// Title aggregate — catalog identity and stock quantity (Catalog slice).
/// </summary>
public class Title : AggregateRoot<TitleState>
{
    public Title()
    {
        State = new TitleState();
    }

// start snippet add-title
    private Task Handle(AddTitleToCatalogCommand cmd)
    {
        if (State.Exists)
            throw new InvalidOperationException("Title is already in the catalog.");

        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new ArgumentException("Title name is required.", nameof(cmd));

        if (cmd.Price < 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Price cannot be negative.");

        Apply(new TitleAddedToCatalog(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.Name.Trim(),
            cmd.Price,
            cmd.IsScarce));

        return Task.CompletedTask;
// end snippet add-title
    }

    private Task Handle(UpdateTitlePriceCommand cmd)
    {
        EnsureExists();

        if (cmd.NewPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Price cannot be negative.");

        Apply(new TitlePriceUpdated(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.NewPrice));

        return Task.CompletedTask;
    }

    private Task Handle(RestockTitleCommand cmd)
    {
        EnsureExists();

        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Restock quantity must be positive.");

        Apply(new TitleRestocked(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId,
            cmd.Quantity));

        return Task.CompletedTask;
    }

    private Task Handle(MarkTitleScarceCommand cmd)
    {
        EnsureExists();

        if (State.IsScarce)
            throw new InvalidOperationException("Title is already marked scarce.");

        Apply(new TitleMarkedScarce(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.TitleId));

        return Task.CompletedTask;
    }

    private void EnsureExists()
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");
    }

// start snippet apply-events
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
                break;

            case TitlePriceUpdated e:
                State.Price = e.NewPrice;
                break;

            case TitleRestocked e:
                State.AvailableUnits += e.Quantity;
                break;

            case TitleMarkedScarce:
                State.IsScarce = true;
                break;
        }
// end snippet apply-events
    }
}
