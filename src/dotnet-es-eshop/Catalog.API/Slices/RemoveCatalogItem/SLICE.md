# Remove Catalog Item

Status: Ready

A catalog admin removes a catalog item from sale.

`RemoveCatalogItemCommand` appends `CatalogItemRemoved` on stream `CatalogItem:{itemId}`.

The item is no longer present.

POST `/api/catalog/remove-catalog-item`.

Remove rejects an item id that is not on the stream with `CATALOG_ITEM_NOT_FOUND`.

Removal is final, and a removed item id cannot be added again.
