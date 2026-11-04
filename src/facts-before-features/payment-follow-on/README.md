# Payment follow-on

## Objective

Reactions after Place Order has already reserved stock (`checkout-aggregates` or `checkout-dcb`). Capture Payment is not inside the checkout decision.

| Gateway outcome | Reaction |
|---|---|
| Payment Captured | Start Fulfillment |
| Payment Failed | Cancel Order for Payment Failure, and Stock Released |

## Run

```bash
cd src/facts-before-features
dotnet run --project payment-follow-on/HarborStreet.Coordination
dotnet test payment-follow-on/HarborStreet.Coordination.Tests
```

## GWT

| Given | When | Then |
|---|---|---|
| Order placed; stock reserved | Capture Payment succeeds | Payment Captured; Fulfillment Started; stock still reserved |
| Order placed; stock reserved | Capture Payment declines | Payment Failed; Order Cancelled; Stock Released; available back to 1 |

## Compared with Place Order

| | Place Order | This sample |
|---|---|---|
| Boundary | Checkout decision (aggregate choreography or one dynamic consistency boundary) | Reactions after payment facts |
| Stock | Reserve on success | Release on payment failure |
| Payment | Out of scope | Fake gateway, then domain commands, then reactions |

## What AI must not invent

- Putting Capture Payment inside Place Order
- Releasing stock without Order Cancelled, or inventing `InventoryAdjusted`
- Starting fulfillment when payment failed
- A distributed two-phase commit with the payment provider

## Layout

```text
Domain/           Title and Order aggregates
Payments/         IPaymentGateway and FakePaymentGateway
Reactions/        PaymentFollowOnReactions
Coordination/     PaymentCaptureService (record the fact, then react)
Seeding/          A reserved order after checkout
```
