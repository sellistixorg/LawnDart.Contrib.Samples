# Mark Order Awaiting Validation

Status: Ready

Sets order status to AwaitingValidation and notifies Catalog.

`MarkOrderAwaitingValidationCommand` appends `OrderAwaitingValidation` on stream `Order:{orderId}`. The model writes that key as `order:{orderId}`. This repo uses the aggregate type name.

`Order` lives in `Aggregates/Order/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command and the handler stay in this slice. `OrderAwaitingValidation` lives in `Shared/Events/` so Catalog can subscribe without referencing Ordering.API.

Only a Submitted order moves to AwaitingValidation. Any other status writes no event. That matches eShop `Order.SetAwaitingValidationStatus`.

An unknown order is rejected with `ORDER_NOT_FOUND`. An empty order id is rejected with `ORDER_ID_REQUIRED`. An empty command id is rejected with `ORDER_REQUEST_ID_REQUIRED`.

POST `/api/ordering/mark-order-awaiting-validation`.

`OrderAwaitingValidation` uses token `order-awaiting-validation`. It carries the order id, the buyer id, and each line's product id and units. The buyer id is the one stored on the order. Applying it sets the status to AwaitingValidation. The SQL outbox copies that write. The RabbitMQ transport publishes it. The When Order Awaiting Validation Validate Stock slice runs after this event. Confirm Grace Period issues this command.
