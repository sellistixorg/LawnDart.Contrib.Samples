# When Payment Succeeded

Status: Ready

Payment Succeeded issues Mark Order Paid.

`WhenPaymentSucceededReactor` subscribes to `PaymentSucceeded` on the `ordering` queue. The routing key is `payment-succeeded`. It issues the existing `MarkOrderPaidCommand` for that order. PaymentProcessor appends `PaymentSucceeded`.

The command id is an RFC 4122 version 5 id. The name is `payment-succeeded:{messageId}`. `messageId` is the transport message id. The same message id is the same command. The SQL inbox records that id after the command succeeds, so a second delivery of the same message id does not run again.

An empty order id is logged and skipped. Mark Order Paid would reject it with `ORDER_ID_REQUIRED`, and this delivery must not be retried.

An unknown order is logged and skipped. Mark Order Paid would reject it with `ORDER_NOT_FOUND`, and this delivery must not be retried.

A delivery with no message id throws. The host logs that failure and rethrows it. The inbox does not record the message. A store failure while loading the order is rethrown the same way, so the transport retries.

Only a StockConfirmed order moves to Paid. Any other status writes no event. A late success, or a repeat while the order is already Paid, writes no event. The command and `Order` stay in Mark Order Paid and `Aggregates/Order/`. `PaymentSucceeded` stays in `Shared/Events/` with token `payment-succeeded`. This slice adds no event.

`AddEShopSlices` scans the Ordering assembly and registers the reactor. `AddEShopLawnDart` already registers the `ordering` queue and the SQL inbox. No new HTTP route is mapped. The existing assembly scan still maps Mark Order Paid.

This is the eShop `OrderPaymentSucceededIntegrationEventHandler`. That handler sends the command that sets the order to Paid.
