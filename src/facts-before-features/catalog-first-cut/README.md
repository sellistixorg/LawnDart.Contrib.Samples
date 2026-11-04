# ch05 — Bookstore first cut (Catalog / Title)

## Objective

Minimal runnable Harbor Street **catalog**: `Title` aggregate with Add / Update Price / Restock / Mark Scarce — aligned with [COMMANDS_AND_EVENTS.md](../../../docs/models/bookstore-v1/COMMANDS_AND_EVENTS.md) §1. No cart, checkout, or DCB yet.

## Run

```powershell
cd src/facts-before-features
dotnet run --project catalog-first-cut/HarborStreet.Catalog/HarborStreet.Catalog.csproj
dotnet test catalog-first-cut/HarborStreet.Catalog.Tests/HarborStreet.Catalog.Tests.csproj
```

## GWT

| Given | When | Then |
|-------|------|------|
| Title not in catalog | Add Title to Catalog | Title Added to Catalog |
| Title Added to Catalog | Update Title Price | Title Price Updated |
| Title not in catalog | Update Title Price | Rejected — title not in catalog |
| Title Added to Catalog | Restock Title (1) | Title Restocked; available = 1 |
| Ordinary title in catalog | Mark Title Scarce | Title Marked Scarce |

## What AI must not invent

- Cart, Place Order, payment, or shipping types in this sample  
- Events not listed in model §1 for catalog  
- A second aggregate “Product” parallel to Title  
- Oversell / reservation logic (that is ch07)
