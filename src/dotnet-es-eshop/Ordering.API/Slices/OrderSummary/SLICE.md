# Order Summary

Status: Ready

Shopper read model for a single order detail as status progresses.

`OrderSummaryProjection` folds `OrderPlaced`, `OrderAwaitingValidation`, `OrderStatusStockConfirmed`, `OrderPaid`, `OrderShipped`, `OrderCancelled`, and `OrderCancelledForStock` into one row per stream `Order:{orderId}`. The model writes that key as `order:{orderId}`. This repo uses the aggregate type name.

Those event types stay where they already are. `Order` stays in `Aggregates/Order/`.

GET `/views/order-summary/{orderNumber}?buyerId=`.

`buyerId` is the signed-in shopper. It must match the order buyer id. An unknown order returns 404. An order for a different buyer returns 404. An empty order id returns 404. A missing buyer id returns 404.

Order Placed sets the order id, the buyer id, the status Submitted, the placed time, the lines, and the ship-to address. The total is the sum of unit price times units. Status text stays empty.

Order Awaiting Validation sets the status to AwaitingValidation. Lines, total, ship-to, and status text stay as they were.

Order Status Stock Confirmed sets the status to StockConfirmed. Status text becomes "All the items were confirmed with available stock."

Order Paid sets the status to Paid. Status text becomes "The payment was performed at a simulated "American Bank checking bank account ending on XX35071"".

Order Shipped sets the status to Shipped. Status text becomes "The order was shipped."

Order Cancelled sets the status to Cancelled. Status text becomes "The order was cancelled."

Order Cancelled For Stock sets the status to Cancelled. Status text lists the products that had no stock: `The product items don't have stock: (names).` A product id with no line is listed as the id.

The response fields are order id, buyer id, current status, placed at, lines, ship-to address, status text, and order total.
