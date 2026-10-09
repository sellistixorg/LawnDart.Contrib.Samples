using System.Globalization;
using System.Text.Json;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared;
using Microsoft.AspNetCore.Hosting;

namespace LawnDart.Samples.EShop.Catalog.API.Seed;

/// <summary>
/// Brands, types, and items read from the eShop catalog.json seed file.
/// </summary>
public sealed class CatalogSeedCatalog
{
    /// <summary>
    /// Units on hand for each seeded item.
    /// </summary>
    public const int AvailableStock = 100;

    /// <summary>
    /// Restock threshold for each seeded item.
    /// </summary>
    public const int RestockThreshold = 10;

    /// <summary>
    /// Maximum stock for each seeded item.
    /// </summary>
    public const int MaxStockThreshold = 200;

    private CatalogSeedCatalog(
        IReadOnlyList<CatalogSeedBrand> brands,
        IReadOnlyList<CatalogSeedType> types,
        IReadOnlyList<CatalogSeedItem> items)
    {
        Brands = brands;
        Types = types;
        Items = items;
    }

    /// <summary>
    /// Brands in first-seen order.
    /// </summary>
    public IReadOnlyList<CatalogSeedBrand> Brands { get; }

    /// <summary>
    /// Types in first-seen order.
    /// </summary>
    public IReadOnlyList<CatalogSeedType> Types { get; }

    /// <summary>
    /// Items that have a name, a brand, and a type.
    /// </summary>
    public IReadOnlyList<CatalogSeedItem> Items { get; }

    /// <summary>
    /// Reads catalog.json from the host content root, the output directory, or the embedded copy.
    /// </summary>
    /// <param name="environment">Host environment. Null uses the output directory or the embedded copy.</param>
    /// <returns>The seed catalog.</returns>
    public static CatalogSeedCatalog Load(IWebHostEnvironment? environment = null)
    {
        var json = ReadText(environment);
        var source = JsonSerializer.Deserialize<CatalogSourceEntry[]>(json) ?? [];
        return FromSource(source);
    }

    /// <summary>
    /// Define Catalog Brand for one seeded brand.
    /// </summary>
    /// <param name="brand">Seeded brand.</param>
    /// <returns>The command.</returns>
    public static DefineCatalogBrandCommand BrandCommand(CatalogSeedBrand brand)
    {
        ArgumentNullException.ThrowIfNull(brand);
        return new DefineCatalogBrandCommand(
            EShopSeedIds.Create("define-catalog-brand:" + brand.Name),
            brand.BrandId,
            brand.Name);
    }

    /// <summary>
    /// Define Catalog Type for one seeded type.
    /// </summary>
    /// <param name="type">Seeded type.</param>
    /// <returns>The command.</returns>
    public static DefineCatalogTypeCommand TypeCommand(CatalogSeedType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return new DefineCatalogTypeCommand(
            EShopSeedIds.Create("define-catalog-type:" + type.Name),
            type.TypeId,
            type.Name);
    }

    /// <summary>
    /// Add Catalog Item for one seeded item, including stock and the picture file name.
    /// </summary>
    /// <param name="item">Seeded item.</param>
    /// <returns>The command.</returns>
    public static AddCatalogItemCommand ItemCommand(CatalogSeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new AddCatalogItemCommand(
            EShopSeedIds.Create("add-catalog-item:" + item.SourceId.ToString(CultureInfo.InvariantCulture)),
            item.ItemId,
            item.Name,
            item.Price,
            item.BrandId,
            item.TypeId,
            item.AvailableStock,
            item.Description,
            item.PictureFileName,
            item.RestockThreshold,
            item.MaxStockThreshold);
    }

    private static CatalogSeedCatalog FromSource(IReadOnlyList<CatalogSourceEntry> source)
    {
        var brands = Distinct(source.Select(entry => entry.Brand))
            .Select(name => new CatalogSeedBrand(name, EShopSeedIds.Create("catalog-brand:" + name)))
            .ToArray();
        var types = Distinct(source.Select(entry => entry.Type))
            .Select(name => new CatalogSeedType(name, EShopSeedIds.Create("catalog-type:" + name)))
            .ToArray();
        var brandIds = brands.ToDictionary(brand => brand.Name, brand => brand.BrandId, StringComparer.Ordinal);
        var typeIds = types.ToDictionary(type => type.Name, type => type.TypeId, StringComparer.Ordinal);

        var items = new List<CatalogSeedItem>();
        foreach (var entry in source)
        {
            if (entry.Name is null || entry.Brand is null || entry.Type is null)
                continue;

            items.Add(new CatalogSeedItem(
                entry.Id,
                EShopSeedIds.Create("catalog-item:" + entry.Id.ToString(CultureInfo.InvariantCulture)),
                brandIds[entry.Brand],
                typeIds[entry.Type],
                entry.Name,
                entry.Description,
                entry.Price,
                entry.Id.ToString(CultureInfo.InvariantCulture) + ".webp",
                AvailableStock,
                RestockThreshold,
                MaxStockThreshold));
        }

        return new CatalogSeedCatalog(brands, types, items);
    }

    private static List<string> Distinct(IEnumerable<string?> names)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (name is null)
                continue;
            if (seen.Add(name))
                list.Add(name);
        }

        return list;
    }

    private static string ReadText(IWebHostEnvironment? environment)
    {
        foreach (var path in CandidatePaths(environment))
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        return ReadEmbedded();
    }

    private static IEnumerable<string> CandidatePaths(IWebHostEnvironment? environment)
    {
        if (environment is not null)
            yield return Path.Combine(environment.ContentRootPath, "Seed", "catalog.json");

        yield return Path.Combine(AppContext.BaseDirectory, "Seed", "catalog.json");
    }

    private static string ReadEmbedded()
    {
        var assembly = typeof(CatalogSeedCatalog).Assembly;
        var name = assembly.GetManifestResourceNames()
            .Single(resource => resource.EndsWith("catalog.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Embedded catalog.json is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class CatalogSourceEntry
    {
        public int Id { get; set; }

        public string? Type { get; set; }

        public string? Brand { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public decimal Price { get; set; }
    }
}

/// <summary>
/// One seeded brand.
/// </summary>
/// <param name="Name">Brand name from catalog.json.</param>
/// <param name="BrandId">Deterministic brand id.</param>
public sealed record CatalogSeedBrand(string Name, Guid BrandId);

/// <summary>
/// One seeded type.
/// </summary>
/// <param name="Name">Type name from catalog.json.</param>
/// <param name="TypeId">Deterministic type id.</param>
public sealed record CatalogSeedType(string Name, Guid TypeId);

/// <summary>
/// One seeded catalog item.
/// </summary>
/// <param name="SourceId">Integer id from catalog.json. The picture file is <c>{SourceId}.webp</c>.</param>
/// <param name="ItemId">Deterministic item id.</param>
/// <param name="BrandId">Brand id.</param>
/// <param name="TypeId">Type id.</param>
/// <param name="Name">Product name.</param>
/// <param name="Description">Description.</param>
/// <param name="Price">Unit price.</param>
/// <param name="PictureFileName">Picture file name.</param>
/// <param name="AvailableStock">Units on hand.</param>
/// <param name="RestockThreshold">Restock threshold.</param>
/// <param name="MaxStockThreshold">Maximum stock.</param>
public sealed record CatalogSeedItem(
    int SourceId,
    Guid ItemId,
    Guid BrandId,
    Guid TypeId,
    string Name,
    string? Description,
    decimal Price,
    string PictureFileName,
    int AvailableStock,
    int RestockThreshold,
    int MaxStockThreshold);
