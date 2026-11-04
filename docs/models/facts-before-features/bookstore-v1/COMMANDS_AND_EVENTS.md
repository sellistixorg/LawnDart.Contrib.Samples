# Harbor Street Books: v1 commands and events (names only)

**Policy:** Reserve stock in the successful Place Order decision. Capture Payment is a follow-on. See [LAST_COPY_GWT.md](./LAST_COPY_GWT.md).

These are names for the model and the samples. CLR types come later.

Naming style: spoken business phrases. Samples may map these to PascalCase types later (`PlaceOrder` to `PlaceOrderCommand`, and so on).

## 1. Catalog (Title)

| Command | Events (success) | Failure or rejection (examples) |
|---|---|---|
| Add Title to Catalog | Title Added to Catalog | none listed |
| Update Title Price | Title Price Updated | Title not in catalog |
| Restock Title | Title Restocked | Title not in catalog; invalid quantity |
| Mark Title Scarce | Title Marked Scarce | Title not in catalog |
| Mark Title Ordinary | Title Marked Ordinary | Title not in catalog |

**Notes**

- **Title** is the catalog identity. For v1, stock quantity lives with the title. Available and reserved come from events.
- **Mark Title Scarce** flags titles where a last-unit race matters (for example a signed edition).

## 2. Cart

| Command | Events (success) | Failure or rejection (examples) |
|---|---|---|
| Open Cart | Cart Opened | none listed |
| Add Title to Cart | Title Added to Cart | Title not in catalog; quantity invalid |
| Remove Title from Cart | Title Removed from Cart | Title not in cart |
| Abandon Cart | Cart Abandoned | Cart already checked out or abandoned |

**Notes**

- Cart is pre-commitment. Checkout is **Place Order**, not a cart event.

## 3. Checkout and reserve (Place Order): last-copy slice

| Command | Events (success) | Failure or rejection (examples) |
|---|---|---|
| Place Order | Order Placed; Stock Reserved | Title unavailable (including last copy taken); cart empty; cart already checked out |

**Notes**

- On success, both facts are part of the same decision: the order exists, and scarce stock is reserved for it.
- Concurrent Place Order against one available unit: exactly one success. The loser gets **Title unavailable**. See [LAST_COPY_GWT.md](./LAST_COPY_GWT.md).
- Aggregates plus choreography, or one dynamic consistency boundary, is a teaching choice. These names stay the same.

## 4. Payment (follow-on)

| Command | Events (success) | Failure or rejection (examples) |
|---|---|---|
| Capture Payment | Payment Captured | Payment declined; order not awaiting payment |
| Record Payment Failure | Payment Failed | records an outcome from the gateway |

**Related (compensation when payment fails)**

| Command | Events (success) | Failure or rejection (examples) |
|---|---|---|
| Cancel Order for Payment Failure | Order Cancelled; Stock Released | Order not cancellable in the current state |

**Notes**

- This is not part of the last-copy scenario's When and Then. It is the reaction after a payment fact.
- **Stock Released** returns reserved quantity to available.

## 5. Fulfillment and ship

| Command | Events (success) | Failure or rejection (examples) |
|---|---|---|
| Start Fulfillment | Fulfillment Started | Order not paid, or not reserved |
| Mark Order Shipped | Order Shipped | Fulfillment not started |
| Cancel Order before Ship | Order Cancelled; Stock Released | Already shipped |

**Notes**

- Shipping stays after checkout.
- Keep the list thin in v1: enough names for the end-to-end board.

## Quick index

### Commands (v1)

- Add Title to Catalog
- Update Title Price
- Restock Title
- Mark Title Scarce
- Mark Title Ordinary
- Open Cart
- Add Title to Cart
- Remove Title from Cart
- Abandon Cart
- Place Order
- Capture Payment
- Record Payment Failure
- Cancel Order for Payment Failure
- Start Fulfillment
- Mark Order Shipped
- Cancel Order before Ship

### Events (v1)

- Title Added to Catalog
- Title Price Updated
- Title Restocked
- Title Marked Scarce
- Title Marked Ordinary
- Cart Opened
- Title Added to Cart
- Title Removed from Cart
- Cart Abandoned
- Order Placed
- Stock Reserved
- Payment Captured
- Payment Failed
- Order Cancelled
- Stock Released
- Fulfillment Started
- Order Shipped

## Outside the v1 name list

- Returns and refunds after ship
- Multi-warehouse allocation
- Gift cards, subscriptions, tax
- Customer profile commands (add one only when a slice needs it)

Those wait for model v2, or for a later sample, unless the board needs the name sooner.
