# When Order Status Stock Confirmed

Status: Ready

Ordering Order Status Stock Confirmed issues Charge Order Payment.

`WhenOrderStatusStockConfirmedReactor` subscribes to `OrderStatusStockConfirmed`. The routing key is `order-status-stock-confirmed`. It issues `ChargeOrderPaymentCommand` for that order. The order id is the event order id. The amount is the order total. The buyer id is the event buyer id. The currency is null. This event has no currency code.

The command id is an RFC 4122 version 5 id. The name is `order-status-stock-confirmed:{messageId}`. The attempt id is an RFC 4122 version 5 id. The name is `payment-attempt:{messageId}`. `messageId` is the transport message id. The same message id is the same command and the same attempt. A second delivery of that message id does not append another Payment Charge Requested. The attempt already has events, so the command writes nothing. The SQL inbox drops that delivery after the first one succeeds.

An empty order id is logged and skipped. Charge Order Payment would reject it with `PAYMENT_ORDER_ID_REQUIRED`, and this delivery must not be retried.

An order total that is not greater than zero is logged and skipped. Charge Order Payment would reject it with `PAYMENT_AMOUNT_INVALID`, and this delivery must not be retried.

A delivery with no message id throws. The host logs that failure and rethrows it. The inbox does not record the message.

`AddEShopSlices` scans the PaymentProcessor assembly and registers the reactor. PaymentProcessor does not map this command on HTTP. Charge Order Payment stays in that slice. `OrderStatusStockConfirmed` stays in `Shared/Events/` with token `order-status-stock-confirmed`.

This is the eShop `OrderStatusChangedToStockConfirmedIntegrationEventHandler`. That handler starts the charge for the stock-confirmed order.
