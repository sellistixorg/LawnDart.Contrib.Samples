# Projections and read models

## Objective

Read models built from events: a product catalog, a stock dashboard (available and reserved), and order history. Views are not the record. Clear them and `RebuildAsync` replays the same facts.

## Run

```bash
cd src/facts-before-features
dotnet run --project projections/HarborStreet.Projections
dotnet test projections/HarborStreet.Projections.Tests
```

## GWT

| Given | When | Then |
|---|---|---|
| Title Added (at 52.00), Restocked(1), Order Placed, Stock Reserved | Rebuild projections | Catalog shows the scarce title at 52.00; stock available=0 reserved=1; order history has the order |
| Views cleared | Rebuild again | Same catalog, stock, and order rows |

## What AI must not invent

- Treating the catalog view as the inventory record
- Event types outside the Harbor Street name list
- Skipping rebuild when a view looks wrong
- Payment or shipping projections in this sample

## Layout

```text
Events/           Title, order, and stock facts
Views/            CatalogTitleView, StockDashboardRow, OrderHistoryItem
Projectors/       Apply(IEvent) and ProjectionPipeline.RebuildAsync
Seeding/          Demo event history for The Lighthouse Keeper
```

Event names match [commands and events](../../../docs/models/facts-before-features/bookstore-v1/COMMANDS_AND_EVENTS.md). Place Order decisions live in `checkout-aggregates` and `checkout-dcb`. This sample consumes the facts.
