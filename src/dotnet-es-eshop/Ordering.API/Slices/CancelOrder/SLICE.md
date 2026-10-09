# Cancel Order

Status: Ready

Cancels an order that is not yet Paid or Shipped.

`CancelOrderCommand` appends `OrderCancelled` on stream `Order:{orderId}`. The model writes that key as `order:{orderId}`. This repo uses the aggregate type name.

`Order` lives in `Aggregates/Order/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command, the handler, and `OrderCancelled` stay in this slice.

A Submitted, AwaitingValidation, or StockConfirmed order moves to Cancelled. A Paid or Shipped order is rejected with `CANNOT_CANCEL_ORDER`. An order that is already Cancelled writes no event. That matches eShop `Order.SetCancelledStatus` for Paid and Shipped, and it leaves an existing cancellation in place.

The status description becomes `The order was cancelled.` The event stores the cancel reason. A blank reason is stored as no reason. Buyer, lines, address, and card fields stay as they were.

An unknown order is rejected with `ORDER_NOT_FOUND`. An empty order id is rejected with `ORDER_ID_REQUIRED`. An empty command id is rejected with `ORDER_REQUEST_ID_REQUIRED`.

POST `/api/ordering/cancel-order`.

`OrderCancelled` uses token `order-cancelled`. It carries the order id, the status Cancelled, and the cancel reason. Applying it sets the status to Cancelled. Order Cancelled For Stock is a different event.
