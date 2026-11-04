# Slice contract: Catalog / Title

**Event model slice:** Catalog
**Source names:** [commands and events](../../../docs/models/facts-before-features/bookstore-v1/COMMANDS_AND_EVENTS.md), section 1

This file is the guardrail for humans and for AI. Implementation must stick to the commands and events in this contract.

## One command focus (primary)

| Command | Success events | Rejection |
|---|---|---|
| Add Title to Catalog | Title Added to Catalog | Fails if the title already exists |

## Supporting commands (same aggregate)

| Command | Success events | Rejection |
|---|---|---|
| Update Title Price | Title Price Updated | Title not in catalog |
| Restock Title | Title Restocked | Title not in catalog; quantity <= 0 |
| Mark Title Scarce | Title Marked Scarce | Title not in catalog; already scarce |

## Aggregate and stream

- Aggregate: **Title**
- Stream id (no tenant): `Title:{titleId}`
- State fields: TitleId, Name, Price, AvailableUnits, IsScarce

## Primary scenario

**Given** a title that is not yet in the catalog
**When** Add Title to Catalog (*The Lighthouse Keeper*, scarce, price 52.00)
**Then** Title Added to Catalog with that name, price, and scarce flag

## Folder skeleton

```text
catalog-slice/
  SLICE.md                          this contract
  README.md
  HarborStreet.Catalog.Slice/
    Domain/
      Commands/
      Events/
      Title.cs
      TitleState.cs
      CatalogEventTypes.cs
    Hosting/
      CatalogComposition.cs         DI and in-memory wiring only
    Program.cs                      thin demo host
  HarborStreet.Catalog.Slice.Tests/
    AddTitleToCatalogTests.cs
```

## Out of this contract

- Cart, Place Order, payment, and shipping types
- New event names not listed above
- Business logic inside `Hosting/` or `Program.cs` beyond calling the repository
