# When Order Paid Deduct Stock

Status: Ready

Ordering Order Paid issues Deduct Catalog Stock.

`WhenOrderPaidDeductStockReactor` subscribes to `OrderPaid`. The routing key is `order-paid`. It issues `DeductCatalogStockCommand` for that order. The order id is the event order id. The stock lines are the event lines: product id and units.

The command id is an RFC 4122 version 5 id. The name is `order-paid:{messageId}`. `messageId` is the transport message id. The same message id is the same command. The reactor dispatches that command and returns no commands. Returning the command would deduct again when the host dispatches it.

A `DomainException` is logged and the reactor returns no commands. The host records the inbox key, so that delivery is not retried. Available stock that is already 0 is rejected with `CATALOG_ITEM_OUT_OF_STOCK`. That check runs before a quantity of zero or less is rejected. A quantity of zero or less is logged the same way. Any other failure is thrown. The host rethrows it. The inbox does not record the message.

The SQL inbox drops a second delivery of the same message id. This slice does not record the order id. A different message id for the same order deducts again.

An unknown product is skipped by Deduct Catalog Stock. A removed product is skipped by Deduct Catalog Stock. Other lines on that command still deduct. For a product that exists, the command deducts the smaller of the requested units and the available stock. Available stock does not go below zero.

A delivery with no message id throws. The host logs that failure and rethrows it. The inbox does not record the message.

`AddEShopSlices` scans the Catalog assembly and registers the reactor. `AddEShopLawnDart` already registers the `catalog` queue (`eshop.catalog`) and the SQL inbox. Deduct Catalog Stock stays in that slice, including `POST /api/catalog/deduct-catalog-stock`. `OrderPaid` stays in `Shared/Events/` with token `order-paid`.

This is the eShop handler on `OrderStatusChangedToPaid` that calls `RemoveStock`.
