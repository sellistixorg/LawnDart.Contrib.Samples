using HarborStreet.Iteration.Commands;
using HarborStreet.Iteration.Events;
using LawnDart;
using LawnDart.Dcb;

namespace HarborStreet.Iteration.Domain;

/// <summary>
/// v2 inventory + Place Order decision for one title at one warehouse.
/// Tags: title:{id} + warehouse:{id}. Concurrent last-copy races are per warehouse.
/// </summary>
// start snippet place-order-at-warehouse
public class WarehouseInventoryDecision : DcbEntity<WarehouseInventoryState>
{
    public static string[] BoundaryTags(Guid warehouseId, Guid titleId) =>
        [$"warehouse:{warehouseId}", $"title:{titleId}"];

    private Task Handle(RestockTitleAtWarehouseCommand cmd)
    {
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Restock quantity must be positive.");

        var tags = BoundaryTags(cmd.WarehouseId, cmd.TitleId);
        Emit(new TitleRestockedAtWarehouse(
            Guid.NewGuid(), DateTime.UtcNow, cmd.WarehouseId, cmd.TitleId, cmd.Quantity), tags);
        return Task.CompletedTask;
    }

    private Task Handle(PlaceOrderCommand cmd)
    {
        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Quantity must be positive.");

        if (State.AvailableUnits < cmd.Quantity)
            throw new InvalidOperationException("Title unavailable at that warehouse.");

        var tags = BoundaryTags(cmd.WarehouseId, cmd.TitleId);

        Emit(new OrderPlaced(
            Guid.NewGuid(), DateTime.UtcNow, cmd.OrderId, cmd.CustomerId,
            cmd.WarehouseId, cmd.TitleId, cmd.Quantity), tags);

        Emit(new StockReservedAtWarehouse(
            Guid.NewGuid(), DateTime.UtcNow, cmd.WarehouseId, cmd.TitleId,
            cmd.OrderId, cmd.Quantity), tags);

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
