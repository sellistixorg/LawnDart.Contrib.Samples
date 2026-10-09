# Deduct Catalog Stock

Status: Ready

Removes units from available stock after payment (eShop OrderStatusChangedToPaid handler RemoveStock).

`DeductCatalogStockCommand` appends `CatalogStockDeducted` on stream `CatalogItem:{itemId}`.

POST `/api/catalog/deduct-catalog-stock`.

The command carries the paid order id and the order stock items (product id and units to deduct). An unknown product is skipped. A removed product is skipped. Other lines on that command still deduct. Removal sets Exists to false and is final. For a product that exists, available stock that is already 0 is rejected with `CATALOG_ITEM_OUT_OF_STOCK`. That check runs before a quantity of zero or less is rejected. Lines for the same item id are then combined by summing their quantities. Each product is its own catalog item stream. The command loads every item and checks every line before it appends. A concurrency or infrastructure failure partway through can still leave a partial result. Validation failures no longer cause it.

The command deducts the smaller of the requested units and the available stock. Two units on hand and five requested removes two. Available stock does not go below zero. Catalog Stock Deducted stores the order id and the units removed. It does not store the requested quantity.

Deduct does not record order ids, so a second Deduct for the same order removes stock again. The When Order Paid Deduct Stock reactor relies on the SQL inbox to drop repeat deliveries.
