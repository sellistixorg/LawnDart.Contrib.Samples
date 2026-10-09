# Catalog Item Detail (admin)

Status: Ready

Admin read model for a single catalog item after add/update/restock.

`CatalogItemDetailAdminProjection` folds `CatalogItemAdded`, `CatalogItemUpdated`, `CatalogItemPriceChanged`, `CatalogItemRestocked`, `CatalogStockDeducted`, and `CatalogItemRemoved` into one row per stream `CatalogItem:{itemId}`.

Those event types stay where they already are. `CatalogItem` stays in `Aggregates/CatalogItem/`.

GET `/views/catalog-item-detail-admin/{itemId}`.

An unknown id returns 404. A removed item is returned with `Removed` set. The response has the model fields: item id, name, description, price, brand id, type id, available stock, restock threshold, max stock, on reorder, and picture file name.

`PictureFileName` is the image file name from the item events, the same field as eShop `CatalogItem.PictureFileName`. Brand and type stay as ids. `OnReorder` starts false. Restock sets it false. Deduct leaves it. No event sets it true.

This slice has no storefront page. The admin read stays on Catalog.API.
