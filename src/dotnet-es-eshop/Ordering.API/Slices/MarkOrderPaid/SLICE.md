# Mark Order Paid

Status: Ready

Sets order status to Paid and signals Catalog to deduct stock.

`MarkOrderPaidCommand` appends `OrderPaid` on stream `Order:{orderId}`. The model writes that key as `order:{orderId}`. This repo uses the aggregate type name.

`Order` lives in `Aggregates/Order/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command and the handler stay in this slice. `OrderPaid` lives in `Shared/Events/` so Catalog can subscribe without referencing Ordering.API.

Only a StockConfirmed order moves to Paid. Any other status writes no event. A second Mark Order Paid on a Paid order writes no event. That matches eShop `Order.SetPaidStatus`.

An unknown order is rejected with `ORDER_NOT_FOUND`. An empty order id is rejected with `ORDER_ID_REQUIRED`. An empty command id is rejected with `ORDER_REQUEST_ID_REQUIRED`.

POST `/api/ordering/mark-order-paid`.

`OrderPaid` uses token `order-paid`. It carries the order id, the buyer id, and each line's product id and units. The buyer id is the one stored on the order. Applying it sets the status to Paid and the status description to `The payment was performed at a simulated "American Bank checking bank account ending on XX35071"`. The SQL outbox copies that write. The RabbitMQ transport publishes it. Catalog deducts stock on this signal in a later slice. When Payment Succeeded issues this command.
