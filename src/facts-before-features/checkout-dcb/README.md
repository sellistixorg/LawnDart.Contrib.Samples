# ch11 — Last-copy checkout with DCB

## Objective

Same last-copy GWT as `ch10-aggregates-checkout`, solved with a **Dynamic Consistency Boundary**: Place Order is one decision that appends **Order Placed** and **Stock Reserved** together under tag `title:{titleId}`. Concurrent attempts: **exactly one winner**; loser leaves no order (no compensation saga).

## Run

```powershell
cd src/facts-before-features
dotnet run --project checkout-dcb/HarborStreet.Checkout.Dcb/HarborStreet.Checkout.Dcb.csproj
dotnet test checkout-dcb/HarborStreet.Checkout.Dcb.Tests/HarborStreet.Checkout.Dcb.Tests.csproj
```

## GWT

Source: [`docs/models/bookstore-v1/LAST_COPY_GWT.md`](../../../docs/models/bookstore-v1/LAST_COPY_GWT.md)

| Given | When | Then (this sample) |
|-------|------|--------------------|
| Title has 1 unit; A & B both observed it | Concurrent Place Order | Exactly **one** Succeeded; **1×** Order Placed + **1×** Stock Reserved; loser `ConflictLost` or unavailable |
| Title has 1 unit | Sequential Place Order then another | Second `RejectedUnavailable` |
| One customer | Place Order | Order Placed + Stock Reserved; available 0 |

## vs aggregates (step 10)

| | Aggregates + choreography | DCB (this sample) |
|--|---------------------------|-------------------|
| Streams / tags | Order stream then Title stream | Decision on `title:{id}` |
| Concurrent last copy | 2× Order Placed, 1× Stock Reserved | 1× each |
| Loser | Orphan order → compensation | No persisted order |
| Consistency | Manual saga / cancel | `AppendCondition.FailIfMatches` |

## What AI must not invent

- Splitting Place Order into Order-then-Reserve across aggregates again (that’s Chapter 10)  
- Emitting order-only tags into `entity.Tags` in a way that shrinks the FailIfMatches scope off the title  
- Payment capture inside Place Order (v1 policy: reserve in checkout; pay later)  
- Distributed locks / DB transactions as the “real” fix  

## Layout

```text
HarborStreet.Checkout.Dcb/
  CheckoutDecision.cs     DcbEntity — Place Order decision
  Commands/ Events/
  Checkout/DcbCheckoutService.cs
  Hosting/
```
