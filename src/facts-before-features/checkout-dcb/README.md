# Last-copy checkout with a dynamic consistency boundary

## Objective

The same last-copy scenario as `checkout-aggregates`, solved as one decision. Place Order appends Order Placed and Stock Reserved together under the tag `title:{titleId}`. Concurrent attempts: exactly one winner. The loser leaves no order.

## Run

```bash
cd src/facts-before-features
dotnet run --project checkout-dcb/HarborStreet.Checkout.Dcb
dotnet test checkout-dcb/HarborStreet.Checkout.Dcb.Tests
```

## GWT

Source: [last-copy scenario](../../../docs/models/facts-before-features/bookstore-v1/LAST_COPY_GWT.md)

| Given | When | Then (this sample) |
|---|---|---|
| Title has 1 unit; A and B both observed it | Concurrent Place Order | Exactly one Succeeded; 1 Order Placed and 1 Stock Reserved; loser `ConflictLost` or unavailable |
| Title has 1 unit | Place Order, then another | Second `RejectedUnavailable` |
| One customer | Place Order | Order Placed and Stock Reserved; available 0 |

## Compared with aggregates

| | Aggregates and choreography | This sample |
|---|---|---|
| Streams and tags | Order stream, then Title stream | Decision on `title:{id}` |
| Concurrent last copy | 2 Order Placed, 1 Stock Reserved | 1 of each |
| Loser | Orphan order, then compensation | No persisted order |
| Consistency | A later cancel | `AppendCondition.FailIfMatches` |

## What AI must not invent

- Splitting Place Order into Order-then-Reserve across aggregates again. That is `checkout-aggregates`.
- Emitting order-only tags into `entity.Tags` in a way that shrinks the FailIfMatches scope off the title
- Payment capture inside Place Order. This model reserves in checkout and pays later.
- Distributed locks or a database transaction as the fix

## Layout

```text
HarborStreet.Checkout.Dcb/
  CheckoutDecision.cs     DcbEntity: Place Order decision
  Commands/ Events/
  Checkout/DcbCheckoutService.cs
  Hosting/
```
