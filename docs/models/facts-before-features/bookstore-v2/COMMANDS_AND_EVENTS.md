# Harbor Street Books: v2 commands and events (names only)

**Delta from:** [bookstore-v1 commands and events](../bookstore-v1/COMMANDS_AND_EVENTS.md)

**Policy:** Unchanged. Reserve in a successful Place Order. Payment is a follow-on.

## Delta summary

| Kind | Name | Notes |
|---|---|---|
| Command | Open Warehouse | New |
| Event | Warehouse Opened | New |
| Command | Restock Title at Warehouse | Replaces the teaching use of global Restock Title |
| Event | Title Restocked at Warehouse | New |
| Command | Place Order | Now requires a warehouse (which location to take stock from) |
| Event | Stock Reserved at Warehouse | Replaces the teaching use of global Stock Reserved for checkout |
| Event | Stock Released at Warehouse | Compensation release at that location |

v1 global Restock Title and Stock Reserved stay in the historical vocabulary. New samples should prefer warehouse-scoped names.

## 1. Catalog (Title): catalog only

| Command | Events (success) | Failure |
|---|---|---|
| Add Title to Catalog | Title Added to Catalog | none listed |
| Update Title Price | Title Price Updated | Title not in catalog |
| Mark Title Scarce | Title Marked Scarce | Title not in catalog |
| Mark Title Ordinary | Title Marked Ordinary | Title not in catalog |

**Notes:** For the v2 teaching path, stock quantity does not live on Title.

## 2. Warehouse

| Command | Events (success) | Failure |
|---|---|---|
| Open Warehouse | Warehouse Opened | Warehouse already open |

## 3. Inventory at a warehouse

| Command | Events (success) | Failure |
|---|---|---|
| Restock Title at Warehouse | Title Restocked at Warehouse | Warehouse unknown; title not in catalog; invalid quantity |

## 4. Checkout and reserve (Place Order): warehouse-scoped

| Command | Events (success) | Failure |
|---|---|---|
| Place Order | Order Placed; Stock Reserved at Warehouse | Title unavailable at that warehouse; warehouse unknown; cart issues |

**Notes:** Concurrent Place Order against one available unit at the same warehouse: exactly one success. Another warehouse's stock is a different consistency boundary.

## 5. Payment and ship

Same names as v1 ([payment and fulfillment](../bookstore-v1/COMMANDS_AND_EVENTS.md)). Prefer **Stock Released at Warehouse** when releasing a warehouse reservation.
