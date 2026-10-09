# Record Payment Failed

Status: Ready

Marks the payment attempt failed and notifies Ordering.

`RecordPaymentFailedCommand` appends `PaymentFailed` on stream `PaymentAttempt:{attemptId}`. The model writes that key as `payment-attempt:{attemptId}`. This repo uses the aggregate type name.

`PaymentAttempt` lives in `Aggregates/PaymentAttempt/`. This slice adds `Handle` and `Apply` for this command. The command and the handler stay in this slice. `PaymentFailed` lives in `Shared/Events/` so Ordering can subscribe without referencing PaymentProcessor.

Given Payment Charge Requested, the command appends Payment Failed. The token is `payment-failed`. The event carries the attempt id, the order id, and the failure detail. Applying it sets the status to Failed and stores the failure detail. Amount, currency, and gateway reference stay as they were.

An unknown attempt, one with no Payment Charge Requested, is rejected with `PAYMENT_ATTEMPT_NOT_FOUND`. An empty command id is rejected with `PAYMENT_COMMAND_ID_REQUIRED`. An empty attempt id is rejected with `PAYMENT_ATTEMPT_ID_REQUIRED`. An empty order id is rejected with `PAYMENT_ORDER_ID_REQUIRED`. An order id that does not match the attempt is rejected with `PAYMENT_ORDER_ID_MISMATCH`.

An attempt whose status is already Succeeded or Failed writes no event. A repeat result leaves the status and the failure detail as they were.

A blank failure reason is stored as null. A reason with surrounding spaces is stored trimmed.

The model error `PAYMENT_DECLINED` is "Gateway declined the charge." This command records that outcome as Payment Failed. The failure detail is the command reason.

Translate Gateway Failed issues this command. PaymentProcessor has no HTTP route for it. Do not call the gateway from this handler. That later slice owns the gateway result.
