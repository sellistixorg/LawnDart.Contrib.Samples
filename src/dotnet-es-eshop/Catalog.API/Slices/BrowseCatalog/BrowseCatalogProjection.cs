using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.BrowseCatalog;

/// <summary>
/// Paginated item list plus facet counts for brands and types.
/// </summary>
[GlobalProjection(Name)]
public sealed class BrowseCatalogProjection : ProjectionBase<BrowseCatalogState>
{
    /// <summary>
    /// Logical projection name. The view store key is this name plus <c>:v1</c>.
    /// </summary>
    public const string Name = "BrowseCatalog";

    /// <summary>
    /// View store key for version 1 of <see cref="Name"/>.
    /// </summary>
    public const string StorageKey = Name + ":v1";

    /// <summary>
    /// Number of items to return in a single page when the caller omits a page size.
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Largest number of items returned in one page. A larger page size is clamped to this.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// The index of the page to return when the caller omits a page index.
    /// </summary>
    public const int DefaultPageIndex = 0;

    /// <summary>
    /// A catalog item was added. Tagged: catalog-item:{itemId}
    /// </summary>
    /// <param name="added">Catalog Item Added.</param>
    /// <remarks>
    /// Removal is final. An add after remove does not list the item again.
    /// </remarks>
    public void Handle(CatalogItemAdded added)
    {
        var row = Find(added.ItemId);
        if (row is { Removed: true })
            return;

        if (row is null)
        {
            row = new BrowseCatalogRow { ItemId = added.ItemId };
            State.Items.Add(row);
        }

        row.Name = added.Name ?? "";
        row.Price = added.Price;
        row.PictureUrl = added.PictureFileName;
        row.AvailableStock = added.AvailableStock;
        row.BrandId = added.BrandId;
        row.TypeId = added.TypeId;
        row.Removed = false;
        RememberBrand(added.BrandId);
        RememberType(added.TypeId);
    }

    /// <summary>
    /// Catalog item details were updated.
    /// </summary>
    /// <param name="updated">Catalog Item Updated.</param>
    /// <remarks>
    /// The stored name, price, picture, brand, and type become the updated values. Removal is final.
    /// </remarks>
    public void Handle(CatalogItemUpdated updated)
    {
        var row = Find(updated.ItemId);
        if (row is null || row.Removed)
            return;

        row.Name = updated.Name ?? "";
        row.Price = updated.Price;
        row.PictureUrl = updated.PictureFileName;
        row.BrandId = updated.BrandId;
        row.TypeId = updated.TypeId;
        RememberBrand(updated.BrandId);
        RememberType(updated.TypeId);
    }

    /// <summary>
    /// Catalog item unit price changed (integration signal for baskets/other contexts).
    /// </summary>
    /// <param name="changed">Catalog Item Price Changed.</param>
    public void Handle(CatalogItemPriceChanged changed)
    {
        var row = Find(changed.ItemId);
        if (row is null || row.Removed)
            return;

        row.Price = changed.NewPrice;
    }

    /// <summary>
    /// Stock was added to a catalog item.
    /// </summary>
    /// <param name="restocked">Catalog Item Restocked.</param>
    public void Handle(CatalogItemRestocked restocked)
    {
        var row = Find(restocked.ItemId);
        if (row is null || row.Removed)
            return;

        row.AvailableStock = restocked.AvailableStock;
    }

    /// <summary>
    /// Stock units were removed after a paid order.
    /// </summary>
    /// <param name="deducted">Catalog Stock Deducted. One event per order. Lines name the item id and the units removed.</param>
    /// <remarks>
    /// Only listed items change. Stock does not go below zero.
    /// </remarks>
    public void Handle(CatalogStockDeducted deducted)
    {
        if (deducted.Deductions is null)
            return;

        foreach (var line in deducted.Deductions)
        {
            var row = Find(line.ItemId);
            if (row is null || row.Removed)
                continue;

            var stock = row.AvailableStock - line.UnitsRemoved;
            row.AvailableStock = stock < 0 ? 0 : stock;
        }
    }

    /// <summary>
    /// Catalog item was removed.
    /// </summary>
    /// <param name="removed">Catalog Item Removed.</param>
    /// <remarks>
    /// Removal is final. The page and the facet counts omit this item.
    /// </remarks>
    public void Handle(CatalogItemRemoved removed)
    {
        var row = Find(removed.ItemId);
        if (row is null)
        {
            row = new BrowseCatalogRow { ItemId = removed.ItemId };
            State.Items.Add(row);
        }

        row.Removed = true;
    }

    /// <summary>
    /// A catalog brand was defined.
    /// </summary>
    /// <param name="defined">Catalog Brand Defined.</param>
    /// <remarks>
    /// The id is kept so the brand facet exists before any item uses it. The display name stays on Catalog Brands And Types.
    /// </remarks>
    public void Handle(CatalogBrandDefined defined) => RememberBrand(defined.BrandId);

    /// <summary>
    /// A catalog type was defined.
    /// </summary>
    /// <param name="defined">Catalog Type Defined.</param>
    /// <remarks>
    /// The id is kept so the type facet exists before any item uses it. The display name stays on Catalog Brands And Types.
    /// </remarks>
    public void Handle(CatalogTypeDefined defined) => RememberType(defined.TypeId);

    /// <summary>
    /// One page of catalog items for the active brand and type filters.
    /// </summary>
    /// <param name="state">Stored catalog index.</param>
    /// <param name="brandIds">Active brand filters. Several ids match any of those brands.</param>
    /// <param name="typeIds">Active type filters. Several ids match any of those types.</param>
    /// <param name="pageIndex">Current page index.</param>
    /// <param name="pageSize">Page size.</param>
    /// <returns>The page, the total match count, and the facet counts.</returns>
    /// <remarks>
    /// Removed items never appear. Rows are ordered by name, then item id.
    /// Page index starts at 0. Page size defaults to 10 and is capped at 100.
    /// A brand count uses the type filter and ignores the brand filter.
    /// A type count uses the brand filter and ignores the type filter.
    /// Brand names and type names come from the Catalog Brands And Types view.
    /// </remarks>
    public static BrowseCatalogView Page(
        BrowseCatalogState? state,
        IReadOnlyList<Guid>? brandIds,
        IReadOnlyList<Guid>? typeIds,
        int pageIndex,
        int pageSize)
    {
        if (pageIndex < 0)
            pageIndex = DefaultPageIndex;

        if (pageSize < 1)
            pageSize = DefaultPageSize;
        else if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;

        var brands = Normalize(brandIds);
        var types = Normalize(typeIds);
        var items = state?.Items ?? [];
        var matches = new List<BrowseCatalogRow>();
        foreach (var row in items)
        {
            if (Matches(row, brands, types))
                matches.Add(row);
        }

        matches.Sort(static (left, right) =>
        {
            var name = string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            return name != 0 ? name : left.ItemId.CompareTo(right.ItemId);
        });

        var skip = (long)pageIndex * pageSize;
        var page = new List<BrowseCatalogItem>();
        if (skip < matches.Count)
        {
            var start = (int)skip;
            var end = start + pageSize;
            if (end > matches.Count)
                end = matches.Count;

            for (var i = start; i < end; i++)
                page.Add(ToItem(matches[i]));
        }

        return new BrowseCatalogView
        {
            Items = page,
            TotalItems = matches.Count,
            PageIndex = pageIndex,
            PageSize = pageSize,
            BrandFacets = BrandFacets(state, types),
            TypeFacets = TypeFacets(state, brands),
            SelectedBrandIds = brands,
            SelectedTypeIds = types
        };
    }

    private void RememberBrand(Guid brandId)
    {
        if (brandId == Guid.Empty || State.BrandIds.Contains(brandId))
            return;

        State.BrandIds.Add(brandId);
    }

    private void RememberType(Guid typeId)
    {
        if (typeId == Guid.Empty || State.TypeIds.Contains(typeId))
            return;

        State.TypeIds.Add(typeId);
    }

    private BrowseCatalogRow? Find(Guid itemId)
    {
        foreach (var row in State.Items)
        {
            if (row.ItemId == itemId)
                return row;
        }

        return null;
    }

    private static List<BrowseCatalogFacet> BrandFacets(BrowseCatalogState? state, List<Guid> types)
    {
        var facets = new List<BrowseCatalogFacet>();
        foreach (var brandId in KnownIds(state?.BrandIds, state?.Items, static row => row.BrandId))
        {
            facets.Add(new BrowseCatalogFacet
            {
                Id = brandId,
                Count = Count(state?.Items, brandId, types, static row => row.BrandId, static row => row.TypeId)
            });
        }

        return facets;
    }

    private static List<BrowseCatalogFacet> TypeFacets(BrowseCatalogState? state, List<Guid> brands)
    {
        var facets = new List<BrowseCatalogFacet>();
        foreach (var typeId in KnownIds(state?.TypeIds, state?.Items, static row => row.TypeId))
        {
            facets.Add(new BrowseCatalogFacet
            {
                Id = typeId,
                Count = Count(state?.Items, typeId, brands, static row => row.TypeId, static row => row.BrandId)
            });
        }

        return facets;
    }

    private static List<Guid> KnownIds(
        List<Guid>? defined,
        List<BrowseCatalogRow>? items,
        Func<BrowseCatalogRow, Guid> id)
    {
        var ids = new List<Guid>();
        if (defined is not null)
        {
            foreach (var definedId in defined)
                AddId(ids, definedId);
        }

        if (items is not null)
        {
            foreach (var row in items)
                AddId(ids, id(row));
        }

        ids.Sort();
        return ids;
    }

    private static int Count(
        List<BrowseCatalogRow>? items,
        Guid id,
        List<Guid> otherFilter,
        Func<BrowseCatalogRow, Guid> ownId,
        Func<BrowseCatalogRow, Guid> otherId)
    {
        if (items is null)
            return 0;

        var count = 0;
        foreach (var row in items)
        {
            if (row.Removed || ownId(row) != id)
                continue;

            if (otherFilter.Count > 0 && !otherFilter.Contains(otherId(row)))
                continue;

            count++;
        }

        return count;
    }

    private static bool Matches(BrowseCatalogRow row, List<Guid> brands, List<Guid> types)
    {
        if (row.Removed)
            return false;

        if (brands.Count > 0 && !brands.Contains(row.BrandId))
            return false;

        if (types.Count > 0 && !types.Contains(row.TypeId))
            return false;

        return true;
    }

    private static List<Guid> Normalize(IReadOnlyList<Guid>? ids)
    {
        var result = new List<Guid>();
        if (ids is null)
            return result;

        foreach (var id in ids)
            AddId(result, id);

        return result;
    }

    private static void AddId(List<Guid> ids, Guid id)
    {
        if (id == Guid.Empty || ids.Contains(id))
            return;

        ids.Add(id);
    }

    private static BrowseCatalogItem ToItem(BrowseCatalogRow row) =>
        new()
        {
            ItemId = row.ItemId,
            Name = row.Name,
            Price = row.Price,
            PictureUrl = row.PictureUrl,
            AvailableStock = row.AvailableStock
        };
}
