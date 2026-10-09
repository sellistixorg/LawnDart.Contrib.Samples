using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;

public sealed class DefineCatalogTypeHandler : ICommandHandler<DefineCatalogTypeCommand>
{
    private readonly IAggregateRepository _types;

    public DefineCatalogTypeHandler(IAggregateRepository types) => _types = types;

    public async Task HandleAsync(
        DefineCatalogTypeCommand command,
        CancellationToken cancellationToken = default)
    {
        var catalogType = await _types
            .GetOrCreateAsync<CatalogType>(command.TypeId, cancellationToken)
            .ConfigureAwait(false);
        await _types
            .HandleCommandAsync(catalogType, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
