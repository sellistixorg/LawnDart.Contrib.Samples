using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.SearchCatalog;

/// <summary>
/// Catalog items whose names start with the search text.
/// </summary>
[GlobalProjection(Name)]
public sealed class SearchCatalogProjection : ProjectionBase<SearchCatalogState>
{
    /// <summary>
    /// Logical projection name. The view store key is this name plus <c>:v1</c>.
    /// </summary>
    public const string Name = "SearchCatalog";

    /// <summary>
    /// View store key for version 1 of <see cref="Name"/>.
    /// </summary>
    public const string StorageKey = Name + ":v1";

    /// <summary>
    /// Number of items to return in a single page of results when the caller omits a page size.
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Largest number of items returned in one page. A larger page size is clamped to this.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// The index of the page of results to return when the caller omits a page index.
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
            row = new SearchCatalogRow { ItemId = added.ItemId };
            State.Items.Add(row);
        }

        row.Name = added.Name ?? "";
        row.Description = added.Description;
        row.Price = added.Price;
        row.PictureUrl = added.PictureFileName;
        row.AvailableStock = added.AvailableStock;
        row.Removed = false;
        row.IsAvailable = added.AvailableStock > 0;
    }

    /// <summary>
    /// Catalog item details were updated.
    /// </summary>
    /// <param name="updated">Catalog Item Updated.</param>
    /// <remarks>
    /// The stored name becomes the updated name. A later search uses that name. Removal is final.
    /// </remarks>
    public void Handle(CatalogItemUpdated updated)
    {
        var row = Find(updated.ItemId);
        if (row is null || row.Removed)
            return;

        row.Name = updated.Name ?? "";
        row.Description = updated.Description;
        row.Price = updated.Price;
        row.PictureUrl = updated.PictureFileName;
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
        row.IsAvailable = restocked.AvailableStock > 0;
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
            row.IsAvailable = row.AvailableStock > 0;
        }
    }

    /// <summary>
    /// Catalog item was removed.
    /// </summary>
    /// <param name="removed">Catalog Item Removed.</param>
    /// <remarks>
    /// Removal is final. Search results omit this item.
    /// </remarks>
    public void Handle(CatalogItemRemoved removed)
    {
        var row = Find(removed.ItemId);
        if (row is null)
        {
            row = new SearchCatalogRow { ItemId = removed.ItemId };
            State.Items.Add(row);
        }

        row.Removed = true;
        row.IsAvailable = false;
    }

    /// <summary>
    /// Catalog items whose names start with <paramref name="text"/>.
    /// </summary>
    /// <param name="state">Stored catalog index.</param>
    /// <param name="text">Search text (matched as a name prefix).</param>
    /// <param name="pageIndex">The index of the page of results to return.</param>
    /// <param name="pageSize">Number of items to return in a single page of results.</param>
    /// <returns>The page of matches and the total match count.</returns>
    /// <remarks>
    /// The prefix match is case-sensitive, the same as eShop Catalog.API <c>Name.StartsWith</c> on PostgreSQL.
    /// Removed items never appear. An empty search text returns no rows.
    /// Matches are ordered by name, then item id. Page index starts at 0. Page size defaults to 10 and is capped at 100.
    /// </remarks>
    public static SearchCatalogView Match(SearchCatalogState? state, string? text, int pageIndex, int pageSize)
    {
        var query = text ?? "";
        if (query.Length == 0)
        {
            return new SearchCatalogView
            {
                Query = "",
                Items = [],
                TotalItems = 0
            };
        }

        if (pageIndex < 0)
            pageIndex = DefaultPageIndex;

        if (pageSize < 1)
            pageSize = DefaultPageSize;
        else if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;

        var matches = new List<SearchCatalogRow>();
        if (state?.Items is not null)
        {
            foreach (var row in state.Items)
            {
                if (row.Removed)
                    continue;

                if (row.Name.StartsWith(query, StringComparison.Ordinal))
                    matches.Add(row);
            }
        }

        matches.Sort(static (left, right) =>
        {
            var name = string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            return name != 0 ? name : left.ItemId.CompareTo(right.ItemId);
        });

        var skip = (long)pageIndex * pageSize;
        var page = new List<SearchCatalogItem>();
        if (skip < matches.Count)
        {
            var start = (int)skip;
            var end = start + pageSize;
            if (end > matches.Count)
                end = matches.Count;

            for (var i = start; i < end; i++)
                page.Add(ToItem(matches[i]));
        }

        return new SearchCatalogView
        {
            Query = query,
            Items = page,
            TotalItems = matches.Count
        };
    }

    private SearchCatalogRow? Find(Guid itemId)
    {
        foreach (var row in State.Items)
        {
            if (row.ItemId == itemId)
                return row;
        }

        return null;
    }

    private static SearchCatalogItem ToItem(SearchCatalogRow row) =>
        new()
        {
            ItemId = row.ItemId,
            Name = row.Name,
            Description = row.Description,
            Price = row.Price,
            PictureUrl = row.PictureUrl,
            AvailableStock = row.AvailableStock,
            IsAvailable = row.IsAvailable
        };
}
