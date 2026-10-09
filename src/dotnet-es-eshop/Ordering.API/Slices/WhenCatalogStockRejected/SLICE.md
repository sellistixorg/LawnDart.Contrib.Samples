# When Catalog Stock Rejected

Status: Ready

Catalog Order Stock Rejected issues Cancel Order For Insufficient Stock.

`WhenCatalogStockRejectedReactor` subscribes to `OrderStockRejected` on the `ordering` queue. The routing key is `order-stock-rejected`. It issues the existing `CancelOrderForInsufficientStockCommand` for that order. The command receives only the product ids whose hasStock flag is false, in the order they appear.

The command id is an RFC 4122 version 5 id. The name is `order-stock-rejected:{messageId}`. `messageId` is the transport message id. The same message id is the same command. The SQL inbox records that id after the command succeeds, so a second delivery of the same message id does not run again.

An empty order id is logged and skipped. Cancel Order For Insufficient Stock would reject it with `ORDER_ID_REQUIRED`, and this delivery must not be retried. An unknown order is rejected with `ORDER_NOT_FOUND`. That failure is rethrown, so the inbox does not record the message.

A delivery with no message id throws. The host logs that failure and rethrows it. The inbox does not record the message.

When every product shows hasStock true, or the line list is missing, the reactor still issues the command with an empty product list. Cancel Order For Insufficient Stock then cancels an AwaitingValidation order. The status description is `The product items don't have stock: ().`

Only an AwaitingValidation order moves to Cancelled. Any other status writes no event. The command and `Order` stay in Cancel Order For Insufficient Stock and `Aggregates/Order/`. `OrderStockRejected` stays in `Shared/Events/` with token `order-stock-rejected`. This slice adds no event.

`AddEShopSlices` scans the Ordering assembly and registers the reactor. `AddEShopLawnDart` already registers the `ordering` queue and the SQL inbox. No new HTTP route is mapped. The existing assembly scan still maps Cancel Order For Insufficient Stock.

This is the eShop `OrderStockRejectedIntegrationEventHandler`. That handler sends the command that cancels the order for insufficient stock.
