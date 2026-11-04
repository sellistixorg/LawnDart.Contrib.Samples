using HarborStreet.Projections.Events;
using HarborStreet.Projections.Views;
using LawnDart;

namespace HarborStreet.Projections.Projectors;

/// <summary>Projects catalog browse rows from Title facts.</summary>
public sealed class CatalogViewProjector
{
    private readonly InMemoryViewStore _store;

    public CatalogViewProjector(InMemoryViewStore store) => _store = store;

    public void Apply(IEvent @event)
    {
        switch (@event)
        {
            case TitleAddedToCatalog e:
                _store.GetOrAddCatalog(e.TitleId, () => new CatalogTitleView
                {
                    TitleId = e.TitleId,
                    Name = e.Name,
                    Price = e.Price,
                    IsScarce = e.IsScarce
                });
                break;

            case TitlePriceUpdated e:
                if (_store.Catalog.TryGetValue(e.TitleId, out var row))
                    row.Price = e.NewPrice;
                break;
        }
    }
}
