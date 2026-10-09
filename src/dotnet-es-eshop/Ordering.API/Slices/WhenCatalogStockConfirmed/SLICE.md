# When Catalog Stock Confirmed

Status: Ready

Catalog Order Stock Confirmed issues Mark Order Stock Confirmed.

`WhenCatalogStockConfirmedReactor` subscribes to `OrderStockConfirmed` on the `ordering` queue. The routing key is `order-stock-confirmed`. It issues the existing `MarkOrderStockConfirmedCommand` for that order.

The command id is an RFC 4122 version 5 id. The name is `order-stock-confirmed:{messageId}`. `messageId` is the transport message id. The same message id is the same command. The SQL inbox records that id after the command succeeds, so a second delivery of the same message id does not run again.

An empty order id is logged and skipped. Mark Order Stock Confirmed would reject it with `ORDER_ID_REQUIRED`, and this delivery must not be retried. An unknown order is rejected with `ORDER_NOT_FOUND`. That failure is rethrown, so the inbox does not record the message.

A delivery with no message id throws. The host logs that failure and rethrows it. The inbox does not record the message.

Only an AwaitingValidation order moves to StockConfirmed. Any other status writes no event. The command and `Order` stay in Mark Order Stock Confirmed and `Aggregates/Order/`. `OrderStockConfirmed` stays in `Shared/Events/` with token `order-stock-confirmed`.

`AddEShopSlices` scans the Ordering assembly and registers the reactor. `AddEShopLawnDart` already registers the `ordering` queue and the SQL inbox. No new HTTP route is mapped. The existing assembly scan still maps Mark Order Stock Confirmed.

This is the eShop `OrderStockConfirmedIntegrationEventHandler`. That handler sends the command that sets the order to StockConfirmed.
