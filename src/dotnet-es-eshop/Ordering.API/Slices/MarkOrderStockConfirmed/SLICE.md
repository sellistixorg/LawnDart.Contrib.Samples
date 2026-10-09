# Mark Order Stock Confirmed

Status: Ready

Sets order status to StockConfirmed.

`MarkOrderStockConfirmedCommand` appends `OrderStatusStockConfirmed` on stream `Order:{orderId}`. The model writes that key as `order:{orderId}`. This repo uses the aggregate type name.

`Order` lives in `Aggregates/Order/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command and the handler stay in this slice. `OrderStatusStockConfirmed` lives in `Shared/Events/` so Payment can subscribe without referencing Ordering.API.

Only an AwaitingValidation order moves to StockConfirmed. Any other status writes no event. That matches eShop `Order.SetStockConfirmedStatus`.

An unknown order is rejected with `ORDER_NOT_FOUND`. An empty order id is rejected with `ORDER_ID_REQUIRED`. An empty command id is rejected with `ORDER_REQUEST_ID_REQUIRED`.

POST `/api/ordering/mark-order-stock-confirmed`.

`OrderStatusStockConfirmed` uses token `order-status-stock-confirmed`. It carries the order id, the buyer id, the buyer name, and the order total. The buyer id is the one stored on the order. The order total is the sum of unit price times units. Applying it sets the status to StockConfirmed and the status description to "All the items were confirmed with available stock." The SQL outbox copies that write. The RabbitMQ transport publishes it. Payment charges on this signal in a later slice. The When Catalog Stock Confirmed slice issues this command.
