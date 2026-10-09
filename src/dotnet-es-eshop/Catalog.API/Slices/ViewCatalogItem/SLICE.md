# View Catalog Item

Status: Ready

Shopper item detail including picture (GET item + pic).

`ViewCatalogItemProjection` folds `CatalogItemAdded`, `CatalogItemUpdated`, `CatalogItemPriceChanged`, `CatalogItemRestocked`, `CatalogStockDeducted`, and `CatalogItemRemoved` into one row per stream `CatalogItem:{itemId}`.

Those event types stay where they already are. `CatalogItem` stays in `Aggregates/CatalogItem/`.

GET `/views/view-catalog-item/{itemId}`.

An unknown id returns 404. A removed item is a final soft delete and returns 404. The response has the nine model fields.

The storefront page is `/item/{itemId}`. It calls this route through the `catalog` HTTP client. The item id on that page is a Guid. Catalog rows link to `/item/{guid}`.

Brand and type names are filled when the route reads Catalog Brands And Types. `IsAvailable` is false when available stock is 0. The route still returns that item.
