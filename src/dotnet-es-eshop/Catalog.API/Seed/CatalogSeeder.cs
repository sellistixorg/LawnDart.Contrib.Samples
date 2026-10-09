using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.Catalog.API.Seed;

/// <summary>
/// Sends Define Catalog Brand, Define Catalog Type, and Add Catalog Item for the seed catalog.
/// </summary>
public sealed class CatalogSeeder
{
    private readonly CatalogSeedCatalog _catalog;
    private readonly ICommandHandler<DefineCatalogBrandCommand> _brands;
    private readonly ICommandHandler<DefineCatalogTypeCommand> _types;
    private readonly ICommandHandler<AddCatalogItemCommand> _items;
    private readonly IAggregateRepository _repository;
    private readonly EShopSeedOptions _options;
    private readonly ILogger<CatalogSeeder> _logger;

    /// <summary>
    /// Creates the seeder.
    /// </summary>
    /// <param name="catalog">Seed rows.</param>
    /// <param name="brands">Define Catalog Brand handler.</param>
    /// <param name="types">Define Catalog Type handler.</param>
    /// <param name="items">Add Catalog Item handler.</param>
    /// <param name="repository">Aggregate repository used to see whether the seed is already stored.</param>
    /// <param name="options">Seed switch.</param>
    /// <param name="logger">Logger.</param>
    public CatalogSeeder(
        CatalogSeedCatalog catalog,
        ICommandHandler<DefineCatalogBrandCommand> brands,
        ICommandHandler<DefineCatalogTypeCommand> types,
        ICommandHandler<AddCatalogItemCommand> items,
        IAggregateRepository repository,
        IOptions<EShopSeedOptions> options,
        ILogger<CatalogSeeder> logger)
    {
        _catalog = catalog;
        _brands = brands;
        _types = types;
        _items = items;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Seeds brands, types, and items when they are missing.
    /// A second call writes nothing when every seeded brand and type exists and every seeded item already has a stream.
    /// A removed item counts as present, so removing one item does not send the catalog again.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the seed.</param>
    /// <returns>A task that completes when the seed has finished or been skipped.</returns>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Catalog seed is disabled.");
            return;
        }

        if (await AlreadySeededAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "Catalog seed is already present ({BrandCount} brands, {TypeCount} types, {ItemCount} items).",
                _catalog.Brands.Count,
                _catalog.Types.Count,
                _catalog.Items.Count);
            return;
        }

        foreach (var brand in _catalog.Brands)
        {
            await SendAsync(
                () => _brands.HandleAsync(CatalogSeedCatalog.BrandCommand(brand), cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var type in _catalog.Types)
        {
            await SendAsync(
                () => _types.HandleAsync(CatalogSeedCatalog.TypeCommand(type), cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var item in _catalog.Items)
        {
            await SendAsync(
                () => _items.HandleAsync(CatalogSeedCatalog.ItemCommand(item), cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Catalog seed stored {BrandCount} brands, {TypeCount} types, and {ItemCount} items.",
            _catalog.Brands.Count,
            _catalog.Types.Count,
            _catalog.Items.Count);
    }

    private async Task<bool> AlreadySeededAsync(CancellationToken cancellationToken)
    {
        foreach (var brand in _catalog.Brands)
        {
            var stored = await _repository
                .GetOrCreateAsync<CatalogBrand>(brand.BrandId, cancellationToken)
                .ConfigureAwait(false);
            if (!stored.State.Exists)
                return false;
        }

        foreach (var type in _catalog.Types)
        {
            var stored = await _repository
                .GetOrCreateAsync<CatalogType>(type.TypeId, cancellationToken)
                .ConfigureAwait(false);
            if (!stored.State.Exists)
                return false;
        }

        foreach (var item in _catalog.Items)
        {
            var stored = await _repository
                .GetOrCreateAsync<CatalogItem>(item.ItemId, cancellationToken)
                .ConfigureAwait(false);
            if (stored.State.Removed || stored.Version > 0)
                continue;

            return false;
        }

        return true;
    }

    private async Task SendAsync(Func<Task> send, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await send().ConfigureAwait(false);
        }
        catch (DomainException ex) when (EShopSeedRejections.IsAlreadyPresent(ex))
        {
            _logger.LogInformation(
                "Catalog seed skipped a command that is already stored: {Message} ({Title}).",
                ex.Message,
                ex.Title);
        }
        catch (ConcurrencyException)
        {
            _logger.LogInformation("Catalog seed skipped a command that another writer stored first.");
        }
    }
}
