# Add Catalog Item

Status: Ready

A catalog admin adds a sellable item.

`AddCatalogItemCommand` appends `CatalogItemAdded` on stream `CatalogItem:{itemId}`.

The event records the name, description, price, picture file name, brand, type, available stock, and optional stock thresholds.

POST `/api/catalog/add-catalog-item`.

An item id that already exists is rejected with `CATALOG_ITEM_ALREADY_EXISTS`.

Removal is final, and a removed item id cannot be added again.
