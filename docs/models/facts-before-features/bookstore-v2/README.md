# Harbor Street Books model v2 (multi-warehouse)

Extends [bookstore-v1](../bookstore-v1/README.md). It does not replace it.

v1 keeps available and reserved quantity on **Title**. Harbor Street now stocks the same scarce title in more than one place (the Harbor floor and the Downtown annex). Oversell rules apply per warehouse. That location's last copy still must not be oversold.

Catalog identity (**Title**) stays. Inventory becomes **Title at Warehouse**.

## What changed from v1

| Area | v1 | v2 |
|---|---|---|
| Restock | Restock Title (one pool) | Restock Title at Warehouse |
| Reserve | Stock Reserved (on the title) | Stock Reserved at Warehouse |
| Place Order | Title id (and cart) | Title id and warehouse id |
| Title aggregate | Catalog and stock quantity | Catalog only (price, scarce flag) |
| New | | Open Warehouse, and a warehouse name |

Payment and ship policies from v1 stay: reserve in Place Order, then payment follow-on.

## Artifacts

| File | Role |
|---|---|
| [COMMANDS_AND_EVENTS.md](./COMMANDS_AND_EVENTS.md) | v2 names and the delta from v1 |
| [MULTI_WAREHOUSE_GWT.md](./MULTI_WAREHOUSE_GWT.md) | Last copy at a warehouse |
| [EVENT_MODEL.md](./EVENT_MODEL.md) | Board guide for the delta |
| Sample | [`multi-warehouse`](../../../../src/facts-before-features/multi-warehouse/) |

v1 docs stay the names for the single-warehouse story. Leave v1 as written.
