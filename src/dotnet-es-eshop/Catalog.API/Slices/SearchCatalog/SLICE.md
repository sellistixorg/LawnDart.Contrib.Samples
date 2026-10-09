# Search Catalog

Status: Ready

Plain name-prefix search: matches catalog items whose names start with the query text (upstream Catalog.API name filter).

`SearchCatalogProjection` folds `CatalogItemAdded`, `CatalogItemUpdated`, `CatalogItemPriceChanged`, `CatalogItemRestocked`, `CatalogStockDeducted`, and `CatalogItemRemoved` into one catalog index. Those event types stay where they already are. `CatalogItem` stays in `Aggregates/CatalogItem/`.

GET `/views/search-catalog?text=&pageIndex=&pageSize=`.

The prefix match is case-sensitive. eShop Catalog.API filters with `Name.StartsWith` on PostgreSQL, and that comparison is case-sensitive. This route uses the same rule: ordinal `StartsWith`. Removed items never appear. Matches are ordered by name, then item id. `pageIndex` starts at 0. `pageSize` defaults to 10 and is capped at 100. `TotalItems` is the match count before the page is cut.

An empty search text returns no rows. Browse Catalog lists the catalog.

The storefront page is `/catalog/search`. It calls this route through the `catalog` HTTP client. Item links use `/item/{guid}`. The catalog page reads its rows and facet counts from Browse Catalog. Each row returns item id, name, description, price, picture URL, available stock, and `IsAvailable`.
