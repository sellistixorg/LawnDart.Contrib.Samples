# Cancel Order For Insufficient Stock

Status: Ready

Cancels order because stock validation failed.

`CancelOrderForInsufficientStockCommand` appends `OrderCancelledForStock` on stream `Order:{orderId}`. The model writes that key as `order:{orderId}`. This repo uses the aggregate type name.

`Order` lives in `Aggregates/Order/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command, the handler, and `OrderCancelledForStock` stay in this slice.

Only an AwaitingValidation order moves to Cancelled. Any other status writes no event. That matches eShop `Order.SetCancelledStatusWhenStockIsRejected`.

The status description lists the products that had no stock: `The product items don't have stock: (names).` Each command product id uses the order line name when the order has that product. A product id with no line is listed as the id. A missing or empty rejected-product list still cancels an AwaitingValidation order. The description is then `The product items don't have stock: ().`

An unknown order is rejected with `ORDER_NOT_FOUND`. An empty order id is rejected with `ORDER_ID_REQUIRED`. An empty command id is rejected with `ORDER_REQUEST_ID_REQUIRED`.

POST `/api/ordering/cancel-order-for-insufficient-stock`.

`OrderCancelledForStock` uses token `order-cancelled-for-stock`. It carries the order id, the rejected product ids, and the status Cancelled. Applying it sets the status to Cancelled. The SQL outbox copies that write. This slice does not subscribe to Order Stock Rejected. The When Catalog Stock Rejected slice issues this command.
