# Translate Gateway Failed

Status: Ready

Translates an inbound payment gateway failure into Record Payment Failed.

`TranslateGatewayFailedSubscription` subscribes to `PaymentGatewayChargeFailed`. The routing key is `payment-gateway-charge-failed`. `TranslateGatewayFailedReactor` issues `RecordPaymentFailedCommand` for that attempt. The attempt id and the order id come from the gateway message. The failure reason is why the charge failed.

The command id is an RFC 4122 version 5 id. The name is `payment-gateway-charge-failed:{messageId}`. `messageId` is the transport message id. The same message id is the same command. A second delivery of that message id does not append another Payment Failed. The inbox key is `translate-gateway-failed:{messageId}`. The SQL inbox drops that delivery after the first one succeeds. An attempt whose status is already Succeeded or Failed writes no event when Record Payment Failed runs.

An empty attempt id is logged and skipped. Record Payment Failed would reject it with `PAYMENT_ATTEMPT_ID_REQUIRED`, and this delivery must not be retried.

An empty order id is logged and skipped. Record Payment Failed would reject it with `PAYMENT_ORDER_ID_REQUIRED`, and this delivery must not be retried.

An unknown attempt, one with no Payment Charge Requested, is logged and skipped. Record Payment Failed would reject it with `PAYMENT_ATTEMPT_NOT_FOUND`, and this delivery must not be retried.

An order id that does not match the attempt is logged and skipped. Record Payment Failed would reject it with `PAYMENT_ORDER_ID_MISMATCH`, and this delivery must not be retried.

An attempt whose status is already Succeeded or Failed is logged and skipped.

A delivery with no message id throws. The subscription logs that failure and rethrows it. The inbox does not record the message.

The callback is not a stored event. `AddEShopReactors` does not subscribe to it. `TranslateGatewayFailedModule` registers the subscription. `AddEShopSlices` finds that module. PaymentProcessor does not map Record Payment Failed on HTTP. That command stays in Record Payment Failed.

The simulated gateway publishes this message when `PaymentOptions.PaymentSucceeded` is false. The message id is `payment-gateway-charge-failed:{attemptId}`.
