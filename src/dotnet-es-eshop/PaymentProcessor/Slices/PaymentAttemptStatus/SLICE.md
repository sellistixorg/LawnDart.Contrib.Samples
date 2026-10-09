# Payment Attempt Status

Status: Ready

Read model of payment attempt lifecycle for ops/diagnostics.

`PaymentAttemptStatusProjection` folds `PaymentChargeRequested`, `PaymentSucceeded`, and `PaymentFailed` into one row per stream `PaymentAttempt:{attemptId}`. The model writes that key as `payment-attempt:{attemptId}`. This repo uses the aggregate type name.

Those event types stay where they already are. `PaymentAttempt` stays in `Aggregates/PaymentAttempt/`.

GET `/views/payment-attempt-status/{attemptId}`.

An unknown id returns 404. The response fields are attempt id, order id, amount, status, gateway reference, and failure detail. Status is Requested, Succeeded, or Failed.

Payment Charge Requested sets the attempt id, the order id, the amount, and status Requested. Gateway reference and failure detail stay empty. Payment Succeeded sets status Succeeded. A gateway reference on that event is stored. A charged amount on that event replaces the requested amount. The order id stays as it was. Payment Failed sets status Failed and stores the failure detail. Amount and gateway reference stay as they were.

This slice has no storefront page. The read stays on PaymentProcessor.
