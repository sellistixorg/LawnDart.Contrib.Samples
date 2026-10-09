# Translate Gateway Succeeded

Status: Ready

A payment gateway success callback issues Record Payment Succeeded.

`TranslateGatewaySucceededHost` subscribes to `PaymentGatewayChargeSucceeded`. The routing key is `payment-gateway-charge-succeeded`. The callback carries the attempt id, the order id, the amount, the currency, and a gateway reference when the gateway sent one. The callback has no event id and no timestamp. `AddEShopSlices` finds `TranslateGatewaySucceededModule`, and that module registers this host.

The host issues `RecordPaymentSucceededCommand`. The attempt id and the order id come from the callback. The gateway reference and the charged amount come from the callback. Those are the fields the command records. The currency stays on the attempt.

The command id is an RFC 4122 version 5 id. The name is `payment-gateway-charge-succeeded:{messageId}`. `messageId` is the transport message id. The gateway message id is `payment-gateway-charge-succeeded:{attemptId}`. The same message id is the same command. A second delivery of that message id does not append another Payment Succeeded. The inbox drops that delivery. An attempt whose status is already Succeeded or Failed writes no event.

A delivery with no message id throws. The host logs that failure and rethrows it. The inbox does not record the message.

Record Payment Succeeded rejections are logged and not retried. An attempt with no Payment Charge Requested is `PAYMENT_ATTEMPT_NOT_FOUND`. An order id that does not match the attempt is `PAYMENT_ORDER_ID_MISMATCH`.

PaymentProcessor does not map this command on HTTP. Record Payment Succeeded stays in that slice.

Upstream the gateway is a simulated stub. It reads `PaymentOptions.PaymentSucceeded`. The default is true. A true flag publishes this callback.
