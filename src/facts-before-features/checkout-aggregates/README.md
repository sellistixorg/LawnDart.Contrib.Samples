# Last-copy checkout with aggregates

## Objective

Order and Title are separate aggregates. A choreography runs the concurrent last-copy scenario. The business names match the event model. The pain is the multi-stream commit. The same scenario in `checkout-dcb` has one winner.

## Run

```bash
cd src/facts-before-features
dotnet run --project checkout-aggregates/HarborStreet.Checkout.Aggregates
dotnet test checkout-aggregates/HarborStreet.Checkout.Aggregates.Tests
```

## GWT

Source: [last-copy scenario](../../../docs/models/facts-before-features/bookstore-v1/LAST_COPY_GWT.md)

| Given | When | Then (desired) | Then (this sample) |
|---|---|---|---|
| Title has 1 unit; customers A and B | Concurrent Place Order | Exactly one order and one reservation | 2 Order Placed, 1 Stock Reserved, loser `NeedsCompensation` |
| Title has 1 unit | Place Order, then another | Second rejected as unavailable | Works when the two orders are not concurrent |
| One customer | Place Order | Order Placed and Stock Reserved; available 0 | Passes |

Choreography order:

1. Optimistic read of Title available. Both racers may observe the last unit.
2. Place Order on the Order stream.
3. Reserve Stock on the Title stream.

The race test models step 1 as a shared stale observation (`skipStockCheck`), then both run steps 2 and 3. Title stock is not oversold. You still get an orphan Order Placed that needs compensation.

## What AI must not invent

- One aggregate that owns both order and stock so the test passes. That hides the multi-stream pain.
- Distributed locks or a database transaction as the fix in this sample
- Payment capture inside Place Order. This model reserves in checkout and pays later.
- A dynamic consistency boundary or `AppendCondition` here. That lives in `checkout-dcb`.

## Layout

```text
HarborStreet.Checkout.Aggregates/
  Catalog/     Title aggregate and Stock Reserved
  Orders/      Order aggregate, Order Placed, and Cancelled
  Checkout/    AggregateCheckoutChoreography
  Hosting/     DI wiring
```
