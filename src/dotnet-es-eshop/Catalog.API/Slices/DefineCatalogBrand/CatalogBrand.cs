using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;

public sealed class CatalogBrand : AggregateRoot<CatalogBrandState>
{
    public void Handle(DefineCatalogBrandCommand command)
    {
        if (command.BrandId == Guid.Empty)
            throw new DomainException("Brand id is required.");

        var name = command.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new DomainException("Brand name is required.");

        if (State.Exists)
            throw new DomainException("Catalog brand is already defined.");

        Apply(new CatalogBrandDefined(Guid.NewGuid(), DateTime.UtcNow, command.BrandId, name));
    }

    protected override void ApplyEventToState(IEvent @event)
    {
        switch (@event)
        {
            case CatalogBrandDefined defined:
                State.BrandId = defined.BrandId;
                State.Name = defined.Name;
                State.Exists = true;
                break;
        }
    }
}
