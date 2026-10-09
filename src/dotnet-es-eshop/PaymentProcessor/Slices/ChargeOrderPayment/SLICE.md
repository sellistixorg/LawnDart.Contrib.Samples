# Charge Order Payment

Status: Ready

Creates a payment attempt and sends a charge request to the gateway.

`ChargeOrderPaymentCommand` appends `PaymentChargeRequested` on stream `PaymentAttempt:{attemptId}`. The model writes that key as `payment-attempt:{attemptId}`. This repo uses the aggregate type name.

`PaymentAttempt` lives in `Aggregates/PaymentAttempt/`. This slice adds `Handle` and `Apply` for this command. The command, the handler, and `PaymentChargeRequested` stay in this slice.

An empty command id is rejected with `PAYMENT_COMMAND_ID_REQUIRED`. An empty attempt id is rejected with `PAYMENT_ATTEMPT_ID_REQUIRED`. An empty order id is rejected with `PAYMENT_ORDER_ID_REQUIRED`. An amount that is not greater than zero is rejected with `PAYMENT_AMOUNT_INVALID`.

A payment attempt that already has events writes no event. A second Charge Order Payment for the same attempt is that case.

`PaymentChargeRequested` uses token `payment-charge-requested`. It carries the attempt id, the order id, the amount, the status `Requested`, and the currency code. Applying it sets those fields. Gateway reference and failure reason stay empty.

The handler calls `IPaymentGateway.ChargeAsync` after the event is stored. It does not return the gateway result. The simulated gateway reads `PaymentOptions.PaymentSucceeded`. The default is true. A true flag makes the gateway return `PaymentGatewaySucceeded`. A false flag makes the gateway return `PaymentGatewayFailed` with reason "Payment was declined." This slice does not append Payment Succeeded or Payment Failed. Those are later slices.

The handler calls the gateway while the attempt status is still `Requested`. The first call appends Payment Charge Requested, then charges. A retry after the gateway throws finds that event and charges again. It does not append a second event. The recorded amount and currency are the ones on the attempt. Once a later slice sets the status to `Succeeded` or `Failed`, this handler stops calling the gateway.

A true flag publishes `PaymentGatewayChargeSucceeded`. A false flag publishes `PaymentGatewayChargeFailed`. Those messages are the gateway callback, not domain events. Their tokens are `payment-gateway-charge-succeeded` and `payment-gateway-charge-failed`. Each message carries the attempt id, the order id, the amount, the currency, and a gateway reference when the gateway sent one. The failure message also carries the reason. The stub sends no gateway reference. The message id is the token plus the attempt id, so a second callback for the same attempt and outcome is the same message. Translate Gateway Succeeded and Translate Gateway Failed subscribe to those messages.

PaymentProcessor does not map this command on HTTP. A later reactor issues it.
