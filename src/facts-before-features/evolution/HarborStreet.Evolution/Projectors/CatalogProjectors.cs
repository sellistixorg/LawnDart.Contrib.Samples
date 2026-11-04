using HarborStreet.Evolution.Events;
using HarborStreet.Evolution.Views;
using LawnDart;

namespace HarborStreet.Evolution.Projectors;

public sealed class CatalogProjectorV1
{
    private readonly EvolutionViewStore _store;
    public CatalogProjectorV1(EvolutionViewStore store) => _store = store;

    public void Apply(IEvent @event)
    {
        if (@event is not TitleAddedToCatalog e)
            return;

        _store.CatalogV1[e.TitleId] = new CatalogTitleViewV1
        {
            TitleId = e.TitleId,
            Name = e.Name,
            Price = e.Price
        };
    }
}

public sealed class CatalogProjectorV2
{
    private readonly EvolutionViewStore _store;
    public CatalogProjectorV2(EvolutionViewStore store) => _store = store;

    public void Apply(IEvent @event)
    {
        if (@event is not TitleAddedToCatalog e)
            return;

        _store.CatalogV2[e.TitleId] = new CatalogTitleViewV2
        {
            TitleId = e.TitleId,
            Name = e.Name,
            Price = e.Price,
            IsScarce = e.IsScarce,
            ScarcityLabel = e.IsScarce ? "Scarce — last-copy rules apply" : "Ordinary"
        };
    }
}
