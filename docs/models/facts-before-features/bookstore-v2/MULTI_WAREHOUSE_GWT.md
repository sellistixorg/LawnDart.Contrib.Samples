# Last copy at a warehouse: Given, When, Then

**Domain:** Harbor Street Books, model v2
**Slice:** Place Order against stock at a specific warehouse
**Policy:** Same as v1. Reserve in Place Order. Payment is a follow-on.

## Scenario

*The Lighthouse Keeper* (scarce) has 1 unit at the Harbor warehouse and 0 at Downtown. Two customers try to Place Order for Harbor's last unit at the same time. Exactly one gets the reservation at Harbor. An order that asks for Downtown fails cleanly, because that warehouse has no stock, even while Harbor still has a copy.

## Concurrent last copy at one warehouse

### Given

- Title **The Lighthouse Keeper** (scarce) is in the catalog
- Warehouse **Harbor** is open and has exactly **1** available unit of that title
- Warehouse **Downtown** is open and has **0** available units of that title
- Customers **A** and **B** both Place Order for that title at **Harbor** (quantity 1), at the same time

### When

- A and B submit Place Order at Harbor at the same time

### Then

- Exactly **one** checkout succeeds with **Order Placed** and **Stock Reserved at Warehouse** (Harbor)
- Harbor available is **0**
- The other checkout fails cleanly (title unavailable at that warehouse)
- Downtown stock is unchanged

### Separate check

- Place Order for the same title at **Downtown** fails with title unavailable at that warehouse while Harbor still held the only unit (before the race). After Harbor is depleted, both warehouses show unavailable.

## Why this scenario exists

Same bookstore words, tighter inventory. Stock is per warehouse. A second global Restock Title that ignores warehouses does not belong here. v2 does not sum warehouses into one pool.
