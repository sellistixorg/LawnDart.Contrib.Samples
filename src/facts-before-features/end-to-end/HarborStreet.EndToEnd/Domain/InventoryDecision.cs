using HarborStreet.EndToEnd.Commands;
using HarborStreet.EndToEnd.Events;
using LawnDart;
using LawnDart.Dcb;

namespace HarborStreet.EndToEnd.Domain;

/// <summary>
/// Place Order and stock reservation as one decision on <c>title:{id}</c>, plus Release Stock after payment failure.
/// </summary>
public class InventoryDecision : DcbEntity<InventoryState>
{
    public static string[] TitleTags(Guid titleId) => [$"title:{titleId}"];

    private Task Handle(AddTitleToCatalogCommand cmd)
    {
        if (State.Exists)
            throw new InvalidOperationException("Title is already in the catalog.");
        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new ArgumentException("Title name is required.", nameof(cmd));

        Emit(new TitleAddedToCatalog(
            Guid.NewGuid(), DateTime.UtcNow, cmd.TitleId, cmd.Name.Trim(), cmd.Price, cmd.IsScarce),
            TitleTags(cmd.TitleId));
        return Task.CompletedTask;
    }

    private Task Handle(RestockTitleCommand cmd)
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Restock quantity must be positive.");

        Emit(new TitleRestocked(Guid.NewGuid(), DateTime.UtcNow, cmd.TitleId, cmd.Quantity),
            TitleTags(cmd.TitleId));
        return Task.CompletedTask;
    }

    private Task Handle(PlaceOrderCommand cmd)
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Quantity must be positive.");
        if (State.AvailableUnits < cmd.Quantity)
            throw new InvalidOperationException("Title unavailable.");

        var tags = TitleTags(cmd.TitleId);
        Emit(new OrderPlaced(
            Guid.NewGuid(), DateTime.UtcNow, cmd.OrderId, cmd.CustomerId, cmd.TitleId, cmd.Quantity), tags);
        Emit(new StockReserved(
            Guid.NewGuid(), DateTime.UtcNow, cmd.TitleId, cmd.OrderId, cmd.Quantity), tags);
        return Task.CompletedTask;
    }

    private Task Handle(ReleaseStockCommand cmd)
    {
        if (!State.Exists)
            throw new InvalidOperationException("Title is not in the catalog.");
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Release quantity must be positive.");
        if (State.ReservedUnits < cmd.Quantity)
            throw new InvalidOperationException("Cannot release more stock than reserved.");

        Emit(new StockReleased(
            Guid.NewGuid(), DateTime.UtcNow, cmd.TitleId, cmd.OrderId, cmd.Quantity),
            TitleTags(cmd.TitleId));
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
            case StockReleased e:
                State.ReservedUnits -= e.Quantity;
                State.AvailableUnits += e.Quantity;
                break;
            case OrderPlaced:
                break;
        }
    }
}
