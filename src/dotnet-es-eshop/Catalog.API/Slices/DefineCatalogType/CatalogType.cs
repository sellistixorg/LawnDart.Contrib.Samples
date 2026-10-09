using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;

public sealed class CatalogType : AggregateRoot<CatalogTypeState>
{
    public void Handle(DefineCatalogTypeCommand command)
    {
        if (command.TypeId == Guid.Empty)
            throw new DomainException("Type id is required.");

        var name = command.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new DomainException("Type name is required.");

        if (State.Exists)
            throw new DomainException("Catalog type is already defined.");

        Apply(new CatalogTypeDefined(Guid.NewGuid(), DateTime.UtcNow, command.TypeId, name));
    }

    protected override void ApplyEventToState(IEvent @event)
    {
        switch (@event)
        {
            case CatalogTypeDefined defined:
                State.TypeId = defined.TypeId;
                State.Name = defined.Name;
                State.Exists = true;
                break;
        }
    }
}
