# ch19 — End-to-end Harbor Street (core flows)

## Objective

Compose the **bookstore-v1** core story in one host:

1. **Last-copy Place Order** via DCB (`title:{id}`) — exactly one winner  
2. **Payment follow-on** — capture → fulfillment, or fail → cancel + **Stock Released**

Not a reimplementation of every chapter sample. Multi-warehouse (v2) stays in `ch16-iteration`.

## Run

```powershell
cd src/facts-before-features
dotnet run --project end-to-end/HarborStreet.EndToEnd/HarborStreet.EndToEnd.csproj
dotnet test end-to-end/HarborStreet.EndToEnd.Tests/HarborStreet.EndToEnd.Tests.csproj
```

## GWT

| Given | When | Then |
|-------|------|------|
| Scarce title, 1 unit; A & B race Place Order | Concurrent place | Exactly one `Succeeded`; **1×** `StockReserved`; one distinct order id |
| Winner awaiting payment | Capture succeeds | `PaymentCaptured` + `FulfillmentStarted`; stock still reserved |
| Winner awaiting payment | Capture declines | `PaymentFailed` + `OrderCancelled` + `StockReleased`; available back to 1 |

Sources: [`LAST_COPY_GWT.md`](../../../docs/models/bookstore-v1/LAST_COPY_GWT.md), payment policy in `COMMANDS_AND_EVENTS.md`.

## Layout

```text
HarborStreet.EndToEnd/
  Domain/InventoryDecision.cs   DCB place + release
  Domain/Order.cs               payment / fulfill / cancel
  Bookstore/HarborStreetBookstore.cs
  Payments/  Reactions/  Hosting/
```

After a DCB win, the facade **materializes** `OrderPlaced` on the order aggregate stream so payment commands have a home. Scarce-stock proof remains **`StockReserved` count** (and inventory state), not a raw global `OrderPlaced` count.

## What AI must not invent

- Payment capture inside Place Order  
- Dual winners on last copy  
- Global pooled stock / warehouse ids (that's v2 / Chapter 16)  
- Rewriting stored events instead of release / cancel facts  
- Skipping tests for race or payment-fail paths  

## Prior chapters

| Concern | Sample |
|---------|--------|
| DCB last-copy | `ch11-dcb-checkout` |
| Payment reactions | `ch12-coordination` |
| Projections / evolution | `ch08-projections`, `ch17-evolution` |
