# ch12 — Automations and coordination

## Objective

Show **payment follow-on reactions** after Place Order already reserved stock (Chapters 10–11). Capture Payment is not inside the checkout decision.

| Gateway outcome | Reaction |
|-----------------|----------|
| Payment Captured | **Start Fulfillment** |
| Payment Failed | **Cancel Order for Payment Failure** + **Stock Released** |

## Run

```powershell
cd src/facts-before-features
dotnet run --project payment-follow-on/HarborStreet.Coordination/HarborStreet.Coordination.csproj
dotnet test payment-follow-on/HarborStreet.Coordination.Tests/HarborStreet.Coordination.Tests.csproj
```

## GWT

| Given | When | Then |
|-------|------|------|
| Order placed; stock reserved | Capture Payment succeeds | Payment Captured; Fulfillment Started; stock still reserved |
| Order placed; stock reserved | Capture Payment declines | Payment Failed; Order Cancelled; Stock Released; available back to 1 |

## vs Place Order (Ch 7)

| | Place Order | This sample |
|--|-------------|-------------|
| Boundary | Checkout decision (agg choreography or DCB) | Reactions after payment facts |
| Stock | Reserve on success | Release on payment failure |
| Payment | Out of scope | Fake gateway → domain commands → reactions |

## What AI must not invent

- Putting Capture Payment inside Place Order / DCB checkout  
- Releasing stock without Order Cancelled (or inventing `InventoryAdjusted`)  
- Starting fulfillment when payment failed  
- Distributed 2PC with the payment provider  

## Layout

```text
Domain/           Title + Order aggregates
Payments/         IPaymentGateway + FakePaymentGateway
Reactions/        PaymentFollowOnReactions
Coordination/     PaymentCaptureService (record fact → react)
Seeding/          Post-checkout reserved order
```
