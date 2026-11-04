# Harbor Street Books event model v2 (delta)

**Full v1 board:** [bookstore-v1/EVENT_MODEL.md](../bookstore-v1/EVENT_MODEL.md)
**Names:** [COMMANDS_AND_EVENTS.md](./COMMANDS_AND_EVENTS.md)
**Scenario:** [MULTI_WAREHOUSE_GWT.md](./MULTI_WAREHOUSE_GWT.md)

## What to add on the board

Keep Catalog, Cart, Place Order, Payment, and Ship. Add or annotate:

| Slice | v2 emphasis |
|---|---|
| Warehouse (new thin slice) | Open Warehouse |
| Catalog | Title identity only. No global restock widget for teaching. |
| Inventory (or an extension of Catalog) | Restock Title at Warehouse |
| Place Order | The command includes a warehouse. Success events are Order Placed and Stock Reserved at Warehouse. The scenario is last copy at a warehouse. |

Payment and Ship stay as in v1.

## Where consistency sits

| Concern | Lean |
|---|---|
| Title (catalog) | Aggregate: identity |
| Stock at a warehouse | Aggregate, or a dynamic consistency boundary over tags `title:{id}` and `warehouse:{id}` |
| Place Order last copy | The same warehouse tag set as the reserve decision |

The [`multi-warehouse`](../../../../src/facts-before-features/multi-warehouse/) sample uses one decision on both tags. Concurrent last-copy races at Harbor match the single-pool decision in [`checkout-dcb`](../../../../src/facts-before-features/checkout-dcb/), scoped to one warehouse.
