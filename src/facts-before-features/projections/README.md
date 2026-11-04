# ch08 — Projections and read models

## Objective

Show Harbor Street **read models derived from events**: product catalog, stock dashboard (available / reserved), and order history. Views are not the system of record—clear and `RebuildAsync` replay the same facts.

## Run

```powershell
cd src/facts-before-features
dotnet run --project projections/HarborStreet.Projections/HarborStreet.Projections.csproj
dotnet test projections/HarborStreet.Projections.Tests/HarborStreet.Projections.Tests.csproj
```

## GWT (projection)

| Given | When | Then |
|-------|------|------|
| Title Added (@ 52.00), Restocked(1), Order Placed, Stock Reserved | Rebuild projections | Catalog shows scarce title @ 52.00; stock available=0 reserved=1; order history has the order |
| Views cleared | Rebuild again | Same catalog / stock / order rows |

## What AI must not invent

- Using the catalog UI / in-memory store as inventory source of truth  
- New event types not in the Harbor Street name list  
- Skipping rebuild/replay when “fixing” a wrong view  
- Payment or shipping projections in this sample (later chapters)  

## Layout

```text
Events/           Title + Order + Stock facts
Views/            CatalogTitleView, StockDashboardRow, OrderHistoryItem
Projectors/       Apply(IEvent) + ProjectionPipeline.RebuildAsync
Seeding/          Demo event history for *The Lighthouse Keeper*
```

Event names match `docs/models/bookstore-v1/COMMANDS_AND_EVENTS.md`. Decision logic for Place Order lives in the Chapter 10–11 samples; this chapter consumes the facts.
