using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;

public sealed class DefineCatalogBrandHandler : ICommandHandler<DefineCatalogBrandCommand>
{
    private readonly IAggregateRepository _brands;

    public DefineCatalogBrandHandler(IAggregateRepository brands) => _brands = brands;

    public async Task HandleAsync(
        DefineCatalogBrandCommand command,
        CancellationToken cancellationToken = default)
    {
        var brand = await _brands
            .GetOrCreateAsync<CatalogBrand>(command.BrandId, cancellationToken)
            .ConfigureAwait(false);
        await _brands
            .HandleCommandAsync(brand, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
