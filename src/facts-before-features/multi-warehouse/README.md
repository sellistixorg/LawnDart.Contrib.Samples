# ch16 — Iterating the domain (multi-warehouse)

## Objective

Implement **bookstore-v2**: stock is per **warehouse**, Title is catalog-only. Same Harbor Street words; tighter inventory reality. Last-copy race is local to a warehouse (DCB on `warehouse:{id}` + `title:{id}`).

## Model first

Read [`docs/models/bookstore-v2/`](../../../docs/models/bookstore-v2/) before changing code—especially [MULTI_WAREHOUSE_GWT.md](../../../docs/models/bookstore-v2/MULTI_WAREHOUSE_GWT.md).

## Run

```powershell
cd src/facts-before-features
dotnet run --project multi-warehouse/HarborStreet.Iteration/HarborStreet.Iteration.csproj
dotnet test multi-warehouse/HarborStreet.Iteration.Tests/HarborStreet.Iteration.Tests.csproj
```

## GWT

| Given | When | Then |
|-------|------|------|
| Harbor has 1 unit; Downtown has 0 | Place Order at Downtown | Rejected unavailable; Harbor still 1 |
| Harbor has 1 unit | Concurrent Place Order at Harbor | Exactly one Order Placed + Stock Reserved at Warehouse |

## What AI must not invent

- Global `Restock Title` that ignores warehouses in this sample  
- Summing all warehouses into one pool unless the model says so (v2 does not)  
- Putting stock fields back on Title “for convenience”  
- Silent rename of Stock Reserved → inventing `InventoryAdjusted`  

## Layout

```text
Domain/Title.cs              catalog only
Domain/Warehouse.cs          Open Warehouse
Domain/WarehouseInventoryDecision.cs   DCB restock + Place Order
Services/MultiWarehouseCheckout.cs
```
