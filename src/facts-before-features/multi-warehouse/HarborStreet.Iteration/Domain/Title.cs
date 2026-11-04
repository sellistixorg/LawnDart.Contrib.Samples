using HarborStreet.Iteration.Commands;
using HarborStreet.Iteration.Events;
using LawnDart;
using LawnDart.Aggregates;

namespace HarborStreet.Iteration.Domain;

/// <summary>v2 Title — catalog identity only (no global stock).</summary>
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

    protected override void ApplyEventToState(IEvent @event)
    {
        if (@event is TitleAddedToCatalog e)
        {
            State.TitleId = e.TitleId;
            State.Name = e.Name;
            State.Price = e.Price;
            State.IsScarce = e.IsScarce;
        }
    }
}

public class TitleState : IState
{
    public Guid TitleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsScarce { get; set; }
    public bool Exists => TitleId != Guid.Empty;
}
