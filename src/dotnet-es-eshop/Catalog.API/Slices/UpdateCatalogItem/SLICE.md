# Update Catalog Item

Status: Ready

A catalog admin replaces the details of a catalog item.

`UpdateCatalogItemCommand` appends `CatalogItemUpdated` on stream `CatalogItem:{itemId}`.

When the unit price differs, the command also appends `CatalogItemPriceChanged` on that stream.

POST `/api/catalog/update-catalog-item`.

Update rejects an item id that is not on the stream.
