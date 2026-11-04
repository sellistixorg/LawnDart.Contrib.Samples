# Slice contract — Catalog / Title

**Chapter sample:** ch14 (AI skeleton shape)  
**Event Model slice:** Catalog  
**Source names:** [COMMANDS_AND_EVENTS.md](../../../docs/models/bookstore-v1/COMMANDS_AND_EVENTS.md) §1  

This file is the **guardrail context** for humans and AI. Implementation must not invent commands or events absent from this contract.

## One command focus (primary)

| Command | Success events | Rejection |
|---------|----------------|-----------|
| Add Title to Catalog | Title Added to Catalog | — (fails if already exists) |

## Supporting commands (same aggregate)

| Command | Success events | Rejection |
|---------|----------------|-----------|
| Update Title Price | Title Price Updated | Title not in catalog |
| Restock Title | Title Restocked | Title not in catalog; quantity ≤ 0 |
| Mark Title Scarce | Title Marked Scarce | Title not in catalog; already scarce |

## Aggregate / stream

- Aggregate: **Title**
- Stream id (no tenant): `Title:{titleId}`
- State fields: TitleId, Name, Price, AvailableUnits, IsScarce

## Primary GWT

**Given** a title that is not yet in the catalog  
**When** Add Title to Catalog (*The Lighthouse Keeper*, scarce, price 52.00)  
**Then** Title Added to Catalog with that name, price, and scarce flag  

## Folder skeleton (what AI should generate)

```text
ch14-catalog-slice/
  SLICE.md                          ← this contract
  README.md
  HarborStreet.Catalog.Slice/
    Domain/
      Commands/
      Events/
      Title.cs
      TitleState.cs
      CatalogEventTypes.cs
    Hosting/
      CatalogComposition.cs         ← DI / InMemory wiring only
    Program.cs                      ← thin demo host
  HarborStreet.Catalog.Slice.Tests/
    AddTitleToCatalogTests.cs
```

## Forbidden

- Cart, Place Order, payment, shipping types  
- New event names not listed above  
- Business logic inside `Hosting/` or `Program.cs` beyond calling the repository  
