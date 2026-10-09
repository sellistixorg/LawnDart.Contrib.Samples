# Ship Order

Status: Ready

Marks a paid order as shipped.

`ShipOrderCommand` appends `OrderShipped` on stream `Order:{orderId}`. The model writes that key as `order:{orderId}`. This repo uses the aggregate type name.

`Order` lives in `Aggregates/Order/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command, the handler, and `OrderShipped` stay in this slice.

Only a Paid order moves to Shipped. Any other status is rejected with `CANNOT_SHIP_ORDER`. A second Ship Order on a Shipped order is rejected the same way. That matches eShop `Order.SetShippedStatus`.

The status description becomes `The order was shipped.` Buyer, lines, address, and card fields stay as they were.

An unknown order is rejected with `ORDER_NOT_FOUND`. An empty order id is rejected with `ORDER_ID_REQUIRED`. An empty command id is rejected with `ORDER_REQUEST_ID_REQUIRED`.

POST `/api/ordering/ship-order`.

`OrderShipped` uses token `order-shipped`. It carries the order id, the status Shipped, and the description. Applying it sets the status to Shipped.
