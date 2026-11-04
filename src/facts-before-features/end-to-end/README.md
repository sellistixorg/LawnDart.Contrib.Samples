# Harbor Street end to end

## Objective

The bookstore v1 core story in one host:

1. Last-copy Place Order on tag `title:{id}`. Exactly one winner.
2. Payment follow-on: capture starts fulfillment, or failure cancels the order and releases stock.

Multi-warehouse stock (v2) stays in `multi-warehouse`.

## Run

```bash
cd src/facts-before-features
dotnet run --project end-to-end/HarborStreet.EndToEnd
dotnet test end-to-end/HarborStreet.EndToEnd.Tests
```

## GWT

| Given | When | Then |
|---|---|---|
| Scarce title, 1 unit; A and B race Place Order | Concurrent place | Exactly one `Succeeded`; 1 `StockReserved`; one distinct order id |
| Winner awaiting payment | Capture succeeds | `PaymentCaptured` and `FulfillmentStarted`; stock still reserved |
| Winner awaiting payment | Capture declines | `PaymentFailed`, `OrderCancelled`, and `StockReleased`; available back to 1 |

Sources: [last-copy scenario](../../../docs/models/facts-before-features/bookstore-v1/LAST_COPY_GWT.md) and the payment names in [commands and events](../../../docs/models/facts-before-features/bookstore-v1/COMMANDS_AND_EVENTS.md).

## Layout

```text
HarborStreet.EndToEnd/
  Domain/InventoryDecision.cs   place and release
  Domain/Order.cs               payment, fulfill, cancel
  Bookstore/HarborStreetBookstore.cs
  Payments/  Reactions/  Hosting/
```

After a win, the facade records `OrderPlaced` on the order aggregate stream so payment commands have a home. The scarce-stock proof is the `StockReserved` count and the inventory state, not a raw global `OrderPlaced` count.

## What AI must not invent

- Payment capture inside Place Order
- Two winners on the last copy
- One pooled stock count, or warehouse ids. That is `multi-warehouse`.
- Rewriting stored events instead of release and cancel facts
- Skipping the race test or the payment-fail test

## Other samples

| Concern | Folder |
|---|---|
| Last copy, one decision | `checkout-dcb` |
| Payment reactions | `payment-follow-on` |
| Projections and versioning | `projections`, `evolution` |
