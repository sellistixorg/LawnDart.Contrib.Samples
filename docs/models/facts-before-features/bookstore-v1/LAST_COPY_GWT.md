# Last-copy checkout: Given, When, Then

**Domain:** Harbor Street Books
**Slice:** Checkout for a scarce title. Reserve stock as part of placing the order.
**Policy (v1):** The successful checkout decision reserves stock. Payment capture is a follow-on. It is not part of this scenario.

## Scenario

Harbor Street has a scarce title: a signed copy of *The Lighthouse Keeper*, one unit left. Two customers try to check out that last unit at the same time. The rule is: never oversell. Exactly one customer gets the reservation. The other is told the copy is gone.

## Concurrent last-copy checkout

### Given

- Title **The Lighthouse Keeper** (scarce) has exactly **1** available unit
- Customer **A** has a cart containing that title (quantity 1)
- Customer **B** has a cart containing that title (quantity 1)
- Neither customer has an order or a reservation for that title yet

### When

- Customer **A** and Customer **B** both submit **Place Order** for that cart at the same time

### Then

- Exactly **one** checkout succeeds
- The successful checkout results in an **order placed** for that customer and a **reservation** of the single available unit for that order
- Available stock for the title is **0**
- The other checkout fails cleanly (no order, no reservation) with a business outcome such as: the signed copy is no longer available
- The title is not oversold (never 2 reservations against 1 unit)

### Later slices

- Payment capture runs afterward for the successful order only
- If payment later fails, a follow-on process may release the reservation and cancel the order

## Why this scenario exists

This is the scenario the book teaches. The same Given, When, Then shows:

1. Why a naive write of stock under the race oversells
2. What separate aggregates plus choreography still struggle with
3. How one checkout decision can enforce exactly one winner without a distributed transaction

A reader can follow this page without a framework.
