using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;

/// <summary>
/// Loads the catalog item and handles <see cref="UpdateCatalogItemCommand"/>.
/// </summary>
public sealed class UpdateCatalogItemHandler : ICommandHandler<UpdateCatalogItemCommand>
{
    private readonly IAggregateRepository _items;

    /// <summary>
    /// Creates a handler for the catalog item store.
    /// </summary>
    /// <param name="items">Catalog item aggregate repository.</param>
    public UpdateCatalogItemHandler(IAggregateRepository items) => _items = items;

    /// <summary>
    /// Replaces catalog item details; emits Price Changed when price differs.
    /// </summary>
    /// <param name="command">Command that replaces the item details.</param>
    /// <param name="cancellationToken">Token that cancels the store call.</param>
    /// <returns>A task that completes when the command is stored.</returns>
    public async Task HandleAsync(
        UpdateCatalogItemCommand command,
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
