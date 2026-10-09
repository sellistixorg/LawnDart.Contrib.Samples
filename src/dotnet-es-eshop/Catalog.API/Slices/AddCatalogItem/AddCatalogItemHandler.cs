using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.AddCatalogItem;

public sealed class AddCatalogItemHandler : ICommandHandler<AddCatalogItemCommand>
{
    private readonly IAggregateRepository _items;

    public AddCatalogItemHandler(IAggregateRepository items) => _items = items;

    public async Task HandleAsync(
        AddCatalogItemCommand command,
        CancellationToken cancellationToken = default)
    {
        var item = await _items
            .GetOrCreateAsync<CatalogItem>(command.ItemId, cancellationToken)
            .ConfigureAwait(false);
        await _items
            .HandleCommandAsync(item, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
