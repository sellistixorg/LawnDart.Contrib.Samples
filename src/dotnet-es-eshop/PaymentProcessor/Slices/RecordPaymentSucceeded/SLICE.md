# Record Payment Succeeded

Status: Ready

Records gateway success on a payment attempt and emits Payment Succeeded so Ordering can mark the order paid.

`RecordPaymentSucceededCommand` appends `PaymentSucceeded` on stream `PaymentAttempt:{attemptId}`. The model writes that key as `payment-attempt:{attemptId}`. This repo uses the aggregate type name.

`PaymentAttempt` lives in `Aggregates/PaymentAttempt/`. This slice adds `Handle` and `Apply` for this command. The command and the handler stay in this slice. `PaymentSucceeded` lives in `Shared/Events`.

`PaymentSucceeded` uses token `payment-succeeded`. It carries the attempt id, the order id, the gateway reference, and the charged amount. Ordering automation When Payment Succeeded subscribes to that token. The SQL outbox publishes it.

An empty command id is rejected with `PAYMENT_COMMAND_ID_REQUIRED`. An empty attempt id is rejected with `PAYMENT_ATTEMPT_ID_REQUIRED`. An empty order id is rejected with `PAYMENT_ORDER_ID_REQUIRED`. An attempt with no Payment Charge Requested is rejected with `PAYMENT_ATTEMPT_NOT_FOUND`. An order id that does not match the attempt is rejected with `PAYMENT_ORDER_ID_MISMATCH`.

An attempt whose status is already Succeeded or Failed writes no event. A repeat result leaves the status, the order id, the amount, and the gateway reference as they were.

Applying Payment Succeeded sets the status to `Succeeded`. The attempt order id stays as it was. A gateway reference on the command is stored after trim. A blank reference is stored as null and leaves the attempt reference unchanged. A charged amount on the command replaces the requested amount. A missing amount leaves the requested amount.

PaymentProcessor does not map this command on HTTP. Translate Gateway Succeeded issues it later. This slice does not read the gateway result.
