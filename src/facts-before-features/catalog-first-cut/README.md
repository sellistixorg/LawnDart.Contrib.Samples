# Catalog first cut

## Objective

A runnable Harbor Street catalog. The `Title` aggregate adds a title, updates the price, restocks, and marks a title scarce. Names match [commands and events](../../../docs/models/facts-before-features/bookstore-v1/COMMANDS_AND_EVENTS.md), section 1. This sample has no cart, checkout, or dynamic consistency boundary.

## Run

```bash
cd src/facts-before-features
dotnet run --project catalog-first-cut/HarborStreet.Catalog
dotnet test catalog-first-cut/HarborStreet.Catalog.Tests
```

## GWT

| Given | When | Then |
|---|---|---|
| Title not in catalog | Add Title to Catalog | Title Added to Catalog |
| Title Added to Catalog | Update Title Price | Title Price Updated |
| Title not in catalog | Update Title Price | Rejected: title not in catalog |
| Title Added to Catalog | Restock Title (1) | Title Restocked; available = 1 |
| Ordinary title in catalog | Mark Title Scarce | Title Marked Scarce |

## What AI must not invent

- Cart, Place Order, payment, or shipping types in this sample
- Events outside catalog section 1 of the model
- A second aggregate named Product beside Title
- Oversell or reservation logic
