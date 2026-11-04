using HarborStreet.Iteration.Commands;
using HarborStreet.Iteration.Events;
using LawnDart;
using LawnDart.Aggregates;

namespace HarborStreet.Iteration.Domain;

public class Warehouse : AggregateRoot<WarehouseState>
{
    public Warehouse() => State = new WarehouseState();

    private Task Handle(OpenWarehouseCommand cmd)
    {
        if (State.Exists)
            throw new InvalidOperationException("Warehouse is already open.");

        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new ArgumentException("Warehouse name is required.", nameof(cmd));

        Apply(new WarehouseOpened(Guid.NewGuid(), DateTime.UtcNow, cmd.WarehouseId, cmd.Name.Trim()));
        return Task.CompletedTask;
    }

    protected override void ApplyEventToState(IEvent @event)
    {
        if (@event is WarehouseOpened e)
        {
            State.WarehouseId = e.WarehouseId;
            State.Name = e.Name;
        }
    }
}

public class WarehouseState : IState
{
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Exists => WarehouseId != Guid.Empty;
}
