# Restock Catalog Item

Status: Ready

A catalog admin adds units to available stock up to MaxStockThreshold.

`RestockCatalogItemCommand` appends `CatalogItemRestocked` on stream `CatalogItem:{itemId}`.

QuantityAdded is the units actually added. AvailableStock on the event is the stock after restock.

POST `/api/catalog/restock-catalog-item`.

Restock rejects an unknown item, including one already removed, with `CATALOG_ITEM_NOT_FOUND`. Removal sets Exists to false and is final.

An empty MaxStockThreshold has no cap. When MaxStockThreshold is set, the command adds only the units that fit.

Restock sets OnReorder to false. A negative quantity is rejected. A quantity of zero stores Catalog Item Restocked with QuantityAdded 0. Restock leaves RestockThreshold as it is. When stock is already at MaxStockThreshold, QuantityAdded is 0.
