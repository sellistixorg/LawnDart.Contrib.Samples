# Confirm Grace Period

Status: Ready

When the grace period elapses, Ordering issues Mark Order Awaiting Validation.

`ConfirmGracePeriodReactor` subscribes to `GracePeriodConfirmed` on the `ordering` queue. The routing key is `grace-period-confirmed`. It issues the existing `MarkOrderAwaitingValidationCommand` for that order id.

The command and the order aggregate stay in Mark Order Awaiting Validation and `Aggregates/Order/`. This slice adds the reactor only.

Only a Submitted order moves to AwaitingValidation. Any other status writes no event. A second confirmation for an order that has left Submitted writes no second event.

An empty order id is logged and skipped. The command would reject it with `ORDER_ID_REQUIRED`, and this delivery must not be retried. An unknown order is rejected with `ORDER_NOT_FOUND`.

`AddEShopSlices` scans the Ordering assembly and registers the reactor. `AddEShopLawnDart` already registers the `ordering` queue, the SQL inbox, and the outbox. No new HTTP route is mapped. The existing assembly scan still maps Mark Order Awaiting Validation.
