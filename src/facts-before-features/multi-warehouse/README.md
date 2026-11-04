# Multi-warehouse stock

## Objective

Model v2: stock is per warehouse, and Title is catalog-only. The Harbor Street words stay. The last-copy race is local to a warehouse (tags `warehouse:{id}` and `title:{id}`).

## Model first

Read [bookstore v2](../../../docs/models/facts-before-features/bookstore-v2/) before changing code, especially [the warehouse scenario](../../../docs/models/facts-before-features/bookstore-v2/MULTI_WAREHOUSE_GWT.md).

## Run

```bash
cd src/facts-before-features
dotnet run --project multi-warehouse/HarborStreet.Iteration
dotnet test multi-warehouse/HarborStreet.Iteration.Tests
```

## GWT

| Given | When | Then |
|---|---|---|
| Harbor has 1 unit; Downtown has 0 | Place Order at Downtown | Rejected as unavailable; Harbor still has 1 |
| Harbor has 1 unit | Concurrent Place Order at Harbor | Exactly one Order Placed and one Stock Reserved at Warehouse |

## What AI must not invent

- A global Restock Title that ignores warehouses in this sample
- Summing all warehouses into one pool. v2 does not.
- Putting stock fields back on Title for convenience
- Renaming Stock Reserved to `InventoryAdjusted`

## Layout

```text
Domain/Title.cs                         catalog only
Domain/Warehouse.cs                     Open Warehouse
Domain/WarehouseInventoryDecision.cs    restock and Place Order
Services/MultiWarehouseCheckout.cs
```
