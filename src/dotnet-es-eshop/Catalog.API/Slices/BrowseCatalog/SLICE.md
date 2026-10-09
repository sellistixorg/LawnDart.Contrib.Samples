# Browse Catalog

Status: Ready

Shopper catalog list with brand/type facets and filters (WebApp Catalog page / GET items + facets).

`BrowseCatalogProjection` folds `CatalogItemAdded`, `CatalogItemUpdated`, `CatalogItemPriceChanged`, `CatalogItemRestocked`, `CatalogStockDeducted`, `CatalogItemRemoved`, `CatalogBrandDefined`, and `CatalogTypeDefined` into one catalog index. Those event types stay where they already are. `CatalogItem` stays in `Aggregates/CatalogItem/`.

GET `/views/browse-catalog?pageIndex=&pageSize=&brand=&type=`.

`brand` and `type` are optional Guid filters. Several values of one filter match any of those ids. The two filters intersect. Removed items never appear. Rows are ordered by name, then item id. `pageIndex` starts at 0. `pageSize` defaults to 10 and is capped at 100. `TotalItems` is the match count before the page is cut.

A brand count uses the active type filter and ignores the active brand filter. A type count uses the active brand filter and ignores the active type filter. Brand and type names come from `GET /views/catalog-brands-and-types`.

A brand or type defined after items already use that id still counts those items. The name shows once Catalog Brands And Types has the id. An item that uses a brand or type that was never defined stays in the rows and in the All count. The filter list has no button for that id. All can be larger than the sum of the visible buttons. A brand or type whose items were all removed stays as a facet with count 0. The product card shows the name and the price.

The catalog page calls both routes through the `catalog` HTTP client. Item links use `/item/{guid}`.

Removal is final. An add after remove does not list the item again.
