# Catalog slice

## Objective

The folder and hosting skeleton for one event-model slice (Catalog / Title): `SLICE.md`, then `Domain/`, `Hosting/`, a thin `Program.cs`, and Given-When-Then tests. Behavior matches `catalog-first-cut`. The layout is the skeleton.

## Run

```bash
cd src/facts-before-features
dotnet run --project catalog-slice/HarborStreet.Catalog.Slice
dotnet test catalog-slice/HarborStreet.Catalog.Slice.Tests
```

## GWT

| Given | When | Then |
|---|---|---|
| Title not in catalog | Add Title to Catalog | Title Added to Catalog |
| Title Added to Catalog | Restock Title (1) | Title Restocked; available = 1 |
| Title not in catalog | Update Title Price | Rejected |

Primary contract: [SLICE.md](./SLICE.md). Regenerating prompt: [AI_SKELETON_PROMPT.md](./AI_SKELETON_PROMPT.md).

## What AI must not invent

- Events or commands outside `SLICE.md`
- Domain rules inside `Hosting/` or `Program.cs`
- Cart, Place Order, payment, or shipping in this sample
- A second aggregate named Product

## Compared with the first catalog cut

| | `catalog-first-cut` | This folder |
|---|---|---|
| Purpose | First runnable catalog | Skeleton layout |
| Layout | Flat project | `Domain/`, `Hosting/`, and `SLICE.md` |
| Event type list | Inline in tests and Program | `CatalogEventTypes.All` |
