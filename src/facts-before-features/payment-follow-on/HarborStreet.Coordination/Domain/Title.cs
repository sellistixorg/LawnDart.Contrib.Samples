using HarborStreet.Coordination.Commands;
using HarborStreet.Coordination.Events;
using LawnDart;
using LawnDart.Aggregates;

namespace HarborStreet.Coordination.Domain;

public class Title : AggregateRoot<TitleState>
{
    public Title() => State = new TitleState();

    private Task Handle(AddTitleToCatalogCommand cmd)
    {
        if (State.Exists)
            throw new InvalidOperationException("Title is already in the catalog.");

        Apply(new TitleAddedToCatalog(
            Guid.NewGuid(), DateTime.UtcNow, cmd.TitleId, cmd.Name.Trim(), cmd.Price, cmd.IsScarce));
        return Task.CompletedTask;
    }

    private Task Handle(RestockTitleCommand cmd)
    {
        EnsureExists();
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Restock quantity must be positive.");

        Apply(new TitleRestocked(Guid.NewGuid(), DateTime.UtcNow, cmd.TitleId, cmd.Quantity));
        return Task.CompletedTask;
    }

    private Task Handle(ReserveStockCommand cmd)
    {
        EnsureExists();
        if (State.AvailableUnits < cmd.Quantity)
            throw new InvalidOperationException("Title unavailable.");

        Apply(new StockReserved(Guid.NewGuid(), DateTime.UtcNow, cmd.TitleId, cmd.OrderId, cmd.Quantity));
        return Task.CompletedTask;
    }

    private Task Handle(ReleaseStockCommand cmd)
    {
        EnsureExists();
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Release quantity must be positive.");
        if (State.ReservedUnits < cmd.Quantity)
            throw new InvalidOperationException("Cannot release more stock than reserved.");

        Apply(new StockReleased(Guid.NewGuid(), DateTime.UtcNow, cmd.TitleId, cmd.OrderId, cmd.Quantity));
        return Task.CompletedTask;
    }

    private void EnsureExists()
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");
    }

    protected override void ApplyEventToState(IEvent @event)
    {
        switch (@event)
        {
            case TitleAddedToCatalog e:
                State.TitleId = e.TitleId;
                State.Name = e.Name;
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
            case StockReleased e:
                State.ReservedUnits -= e.Quantity;
                State.AvailableUnits += e.Quantity;
                break;
        }
    }
}
