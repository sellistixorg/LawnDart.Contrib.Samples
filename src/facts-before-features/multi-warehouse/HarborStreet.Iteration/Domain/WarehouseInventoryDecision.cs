using HarborStreet.Iteration.Commands;
using HarborStreet.Iteration.Events;
using LawnDart;
using LawnDart.Dcb;
using LawnDart.EventStore;

namespace HarborStreet.Iteration.Domain;

/// <summary>
/// v2 inventory and Place Order decision for one title at one warehouse.
/// The decision query is one item with the warehouse tag and the title tag, so both are required.
/// Concurrent last-copy races are per warehouse.
/// Each event is stored with those two tags, because the fact is about that title at that warehouse.
/// </summary>
// start snippet place-order-at-warehouse
public class WarehouseInventoryDecision : DcbEntity<WarehouseInventoryState>
{
    /// <summary>Tag for events about this warehouse.</summary>
    public static string WarehouseTag(Guid warehouseId) => $"warehouse:{warehouseId}";

    /// <summary>Tag for events about this title.</summary>
    public static string TitleTag(Guid titleId) => $"title:{titleId}";

    /// <summary>
    /// Decision query for one title at one warehouse.
    /// Both tags sit in one item, so an event must carry both.
    /// </summary>
    public static Query ForWarehouseTitle(Guid warehouseId, Guid titleId) =>
        Query.FromItems(QueryItem.ByTags(WarehouseTag(warehouseId), TitleTag(titleId)));

    private Task Handle(RestockTitleAtWarehouseCommand cmd)
    {
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Restock quantity must be positive.");

        Emit(new TitleRestockedAtWarehouse(
            Guid.NewGuid(), DateTime.UtcNow, cmd.WarehouseId, cmd.TitleId, cmd.Quantity),
            WarehouseTag(cmd.WarehouseId), TitleTag(cmd.TitleId));
        return Task.CompletedTask;
    }

    private Task Handle(PlaceOrderCommand cmd)
    {
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Quantity must be positive.");

        if (State.AvailableUnits < cmd.Quantity)
            throw new InvalidOperationException("Title unavailable at that warehouse.");

        Emit(new OrderPlaced(
            Guid.NewGuid(), DateTime.UtcNow, cmd.OrderId, cmd.CustomerId,
            cmd.WarehouseId, cmd.TitleId, cmd.Quantity),
            WarehouseTag(cmd.WarehouseId), TitleTag(cmd.TitleId));

        Emit(new StockReservedAtWarehouse(
            Guid.NewGuid(), DateTime.UtcNow, cmd.WarehouseId, cmd.TitleId,
            cmd.OrderId, cmd.Quantity),
            WarehouseTag(cmd.WarehouseId), TitleTag(cmd.TitleId));

        return Task.CompletedTask;
// end snippet place-order-at-warehouse
    }

    protected override void ApplyEventToState(IEvent @event)
    {
        switch (@event)
        {
            case TitleRestockedAtWarehouse e:
                State.WarehouseId = e.WarehouseId;
                State.TitleId = e.TitleId;
                State.AvailableUnits += e.Quantity;
                break;
            case StockReservedAtWarehouse e:
                State.WarehouseId = e.WarehouseId;
                State.TitleId = e.TitleId;
                State.AvailableUnits -= e.Quantity;
                State.ReservedUnits += e.Quantity;
                break;
            case OrderPlaced:
                break;
        }
    }
}

public class WarehouseInventoryState : IState
{
    public Guid WarehouseId { get; set; }
    public Guid TitleId { get; set; }
    public int AvailableUnits { get; set; }
    public int ReservedUnits { get; set; }
}
