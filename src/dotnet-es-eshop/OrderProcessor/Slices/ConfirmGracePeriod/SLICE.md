# Confirm Grace Period

Status: Ready

Grace-period timer after Order Started, then issues Mark Order Awaiting Validation. The timer runs in the OrderProcessor worker. Ordering API issues Mark Order Awaiting Validation.

`TrackSubmittedOrderReactor` subscribes to `OrderStarted` on the `order-processor` queue. The routing key is `order-started`. It writes the order id and the event time before it returns. The inbox acknowledges Order Started only after that row is saved.

The rows live in `dbo.ProjectionViews` under the view type `order-processor-pending-grace`. Ordering projections use other type names in that same table. The AppHost is unchanged. OrderProcessor already uses `orderingdb` and the SQL view store.

`GracePeriodManagerService` wakes every CheckUpdateTime. The default is 30 seconds. Each tick reads the due rows. An order is due when its age is at least GracePeriodTime. The default is 1 minute. Both values come from `BackgroundTaskOptions`. A non-positive value uses that default. The clock is `TimeProvider`.

A due order is published as `GracePeriodConfirmed`. The token is `grace-period-confirmed`. The message id is a version 5 id derived from the order id. The row is deleted after a successful publish. A failed publish leaves the row for the next check.

The list keeps the first Order Started time. A redelivery does not move the clock. The pending list does not read order status. An order that has left Submitted is still confirmed. Mark Order Awaiting Validation writes no event in that case.

OrderProcessor does not poll the shared outbox. `AddEShopLawnDart` is called with the outbox processor off. Ordering.API publishes `orderingdb`.

This worker adds no HTTP route and no page.
