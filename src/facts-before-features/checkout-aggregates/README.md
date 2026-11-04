# ch10 — Last-copy checkout with traditional aggregates

## Objective

Show why **Order + Title as separate aggregates + choreography** struggles with the concurrent last-copy GWT. Same business names as the Event Model; the pain is the multi-stream commit. Pair with `ch11-dcb-checkout` (Chapter 11) for the side-by-side win.

## Run

```powershell
cd src/facts-before-features
dotnet run --project checkout-aggregates/HarborStreet.Checkout.Aggregates/HarborStreet.Checkout.Aggregates.csproj
dotnet test checkout-aggregates/HarborStreet.Checkout.Aggregates.Tests/HarborStreet.Checkout.Aggregates.Tests.csproj
```

## GWT

Source: [`docs/models/bookstore-v1/LAST_COPY_GWT.md`](../../../docs/models/bookstore-v1/LAST_COPY_GWT.md)

| Given | When | Then (desired) | Then (this sample) |
|-------|------|----------------|--------------------|
| Title has 1 unit; customers A & B | Concurrent Place Order | Exactly one order + one reservation | **2× Order Placed**, **1× Stock Reserved**, loser `NeedsCompensation` |
| Title has 1 unit | Sequential Place Order then another | Second rejected unavailable | Same — works when not concurrent |
| Happy path one customer | Place Order | Order Placed + Stock Reserved; available 0 | Passes |

Choreography order (deliberate teaching shape):

1. Optimistic read of Title available (both racers may observe the last unit)  
2. **Place Order** on Order stream  
3. **Reserve Stock** on Title stream  

The race test models step 1 as a shared stale observation (`skipStockCheck`), then both run steps 2–3. Title stock is not oversold (optimistic concurrency / invariant on Title), but you get an **orphan Order Placed** that needs compensation — the multi-aggregate pain the chapter teaches.

## What AI must not invent

- A single aggregate that silently owns both order and stock “to make the test pass” (that hides the chapter lesson)  
- Distributed locks / DB transactions as the “solution” in this sample  
- Payment capture inside Place Order (v1 policy: reserve in checkout; pay later)  
- DCB / `AppendCondition` APIs here — that belongs in `ch11-dcb-checkout`  

## Layout

```text
HarborStreet.Checkout.Aggregates/
  Catalog/     Title aggregate + Stock Reserved
  Orders/      Order aggregate + Order Placed / Cancelled
  Checkout/    AggregateCheckoutChoreography
  Hosting/     DI wiring
```
