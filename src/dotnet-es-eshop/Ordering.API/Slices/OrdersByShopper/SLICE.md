# Orders By Shopper

Status: Ready

Shopper order list / history (GET api/orders).

`OrdersByShopperProjection` folds `OrderPlaced`, `OrderAwaitingValidation`, `OrderStatusStockConfirmed`, `OrderPaid`, `OrderShipped`, `OrderCancelled`, and `OrderCancelledForStock` into one list per shopper.

Those event types stay where they already are. `Order` stays in `Aggregates/Order/`.

GET `/views/orders-by-shopper?buyerId=`.

A missing or empty buyer id returns 400. An unknown buyer returns an empty list. Rows are that shopper's orders, newest first. A later place with the same date sorts ahead of an earlier place. Each row has the order id, the date it was placed, the status, and the total. The total is the sum of unit price times units from Order Placed. A later status event updates the matching row and leaves the total as it was. A status event for an unknown order leaves the list unchanged. A buyer id on a status event that does not match the row leaves that row unchanged.

The orders page calls this route through the `ordering` HTTP client. Buyer id is the `accountId` query on the page. A signed-out shopper sees an empty list and a sign-in link. A signed-in shopper with no account id sees that the account is missing. A shopper with no orders sees the empty orders state.

A refused connection, bad JSON, a timeout, or an unsupported address shows that ordering is temporarily unavailable. Cancellation of the component token still throws. The client keeps no order rows.
