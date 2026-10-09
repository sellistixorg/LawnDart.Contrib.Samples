# When Payment Failed

Status: Ready

Payment failed cancels the order.

`WhenPaymentFailedReactor` subscribes to `PaymentFailed` on the `ordering` queue. The routing key is `payment-failed`. It issues the existing `CancelOrderCommand` for that order. The cancel reason is the payment failure detail.

The command id is an RFC 4122 version 5 id. The name is `payment-failed:{messageId}`. `messageId` is the transport message id. The same message id is the same command. The SQL inbox records that id after the handoff finishes, so a second delivery of the same message id does not run again.

An empty order id is logged and skipped. Cancel Order would reject it with `ORDER_ID_REQUIRED`, and this delivery must not be retried.

An unknown order is logged and skipped. Cancel Order would reject it with `ORDER_NOT_FOUND`, and this delivery must not be retried.

A Paid or Shipped order is logged and skipped. Cancel Order would reject it with `CANNOT_CANCEL_ORDER`, and this delivery must not be retried.

A delivery with no message id throws. The host logs that failure and rethrows it. The inbox does not record the message.

A store failure while loading the order is rethrown. The inbox does not record the message, so the transport retries.

A Submitted, AwaitingValidation, StockConfirmed, or already Cancelled order still issues Cancel Order. Cancel Order moves Submitted, AwaitingValidation, and StockConfirmed to Cancelled. An order that is already Cancelled writes no event. The status description becomes `The order was cancelled.` A blank failure detail is stored as no reason.

`CancelOrderCommand` and `OrderCancelled` stay in Cancel Order. `PaymentFailed` stays in `Shared/Events/` with token `payment-failed`. This slice adds no event.

`AddEShopSlices` scans the Ordering assembly and registers the reactor. `AddEShopLawnDart` already registers the `ordering` queue and the SQL inbox. No new HTTP route is mapped. The existing assembly scan still maps Cancel Order.

This is the eShop `OrderPaymentFailedIntegrationEventHandler`. That handler sends Cancel Order for the order.
