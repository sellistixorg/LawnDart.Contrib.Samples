# Facts Before Features

The code for the book Facts Before Features, set in Harbor Street Books.

## Prerequisites

You need the .NET 10 SDK. Every sample keeps its state in memory. There is no database, no container, and no account.

`global.json` pins SDK `10.0.401` and rolls forward to the latest feature band. Run the commands below from `src/facts-before-features` so that pin applies.

## Get the code for your edition

```bash
git clone https://github.com/sellistixorg/LawnDart.Contrib.Samples
cd LawnDart.Contrib.Samples
```

This draft on `main` uses LawnDart `0.7.0-alpha.5`. An edition tag is not published yet.

When a tag is published, check it out:

```bash
git checkout <edition-tag>
```

Or download that tag's zip from GitHub.

## Build and test

```bash
cd src/facts-before-features
dotnet test HarborStreet.Samples.slnx
```

`dotnet run --project smoke-check/HarborStreet.Shell` checks that the LawnDart packages resolve.

## Chapters

Commands assume you are in `src/facts-before-features`.

| Chapter | Title | Folder | What it shows | Run | Test |
|---|---|---|---|---|---|
| 0 | Tooling demo | `book-support/snippet-demo` | One snippet file for the tooling demo. Not a project, and not in the solution. | | |
| 5 | Harbor Street, first board | `catalog-first-cut` | Title catalog: add, price, restock, mark scarce | `dotnet run --project catalog-first-cut/HarborStreet.Catalog` | `dotnet test catalog-first-cut/HarborStreet.Catalog.Tests` |
| 8 | Projections are features | `projections` | Catalog, stock dashboard, and order history rebuilt from events. Chapter 15 uses this folder for replay. | `dotnet run --project projections/HarborStreet.Projections` | `dotnet test projections/HarborStreet.Projections.Tests` |
| 10 | The last-copy wall | `checkout-aggregates` | Order and Title as separate aggregates. The race leaves an orphan order. | `dotnet run --project checkout-aggregates/HarborStreet.Checkout.Aggregates` | `dotnet test checkout-aggregates/HarborStreet.Checkout.Aggregates.Tests` |
| 11 | Dynamic consistency boundary | `checkout-dcb` | One decision appends Order Placed and Stock Reserved. Exactly one winner. Chapter 15 uses the race test here. | `dotnet run --project checkout-dcb/HarborStreet.Checkout.Dcb` | `dotnet test checkout-dcb/HarborStreet.Checkout.Dcb.Tests` |
| 12 | Automations and decisions | `payment-follow-on` | After payment: start fulfillment, or cancel the order and release stock | `dotnet run --project payment-follow-on/HarborStreet.Coordination` | `dotnet test payment-follow-on/HarborStreet.Coordination.Tests` |
| 14 | Generating and implementing a slice | `catalog-slice` | Catalog slice skeleton: `Domain/`, `Hosting/`, `SLICE.md`, and tests | `dotnet run --project catalog-slice/HarborStreet.Catalog.Slice` | `dotnet test catalog-slice/HarborStreet.Catalog.Slice.Tests` |
| 15 | Guardrails, tests, and roles | `projections`, `checkout-dcb` | Replay rebuild, and the last-copy test. No separate folder. | See chapters 8 and 11 | See chapters 8 and 11 |
| 16 | Iterating when the model is wrong | `multi-warehouse` | Stock per warehouse (model v2) | `dotnet run --project multi-warehouse/HarborStreet.Iteration` | `dotnet test multi-warehouse/HarborStreet.Iteration.Tests` |
| 17 | Versioning and projection families | `evolution` | Upcast an old Order Placed. Rebuild catalog and order views. The same folder is the versioning sample for chapter 18. | `dotnet run --project evolution/HarborStreet.Evolution` | `dotnet test evolution/HarborStreet.Evolution.Tests` |
| 19 | Harbor Street end to end | `end-to-end` | Last-copy decision, then payment follow-on | `dotnet run --project end-to-end/HarborStreet.EndToEnd` | `dotnet test end-to-end/HarborStreet.EndToEnd.Tests` |

Model notes for v1 and v2 are in `docs/models/facts-before-features/`.

## Editions

| Edition | Tag | LawnDart version |
|---|---|---|
| Draft on main | not yet published | 0.7.0-alpha.5 |
