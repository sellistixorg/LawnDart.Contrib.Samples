# ch14 — Catalog vertical slice (AI skeleton shape)

## Objective

Show the **folder and hosting skeleton** you want AI to generate for one Event Model slice (Catalog / Title): `SLICE.md` contract → `Domain/` + `Hosting/` + thin `Program.cs` + GWT tests. Behavior matches Chapter 5’s catalog; structure is what Chapter 14 teaches.

## Run

```powershell
cd src/facts-before-features
dotnet run --project catalog-slice/HarborStreet.Catalog.Slice/HarborStreet.Catalog.Slice.csproj
dotnet test catalog-slice/HarborStreet.Catalog.Slice.Tests/HarborStreet.Catalog.Slice.Tests.csproj
```

## GWT

| Given | When | Then |
|-------|------|------|
| Title not in catalog | Add Title to Catalog | Title Added to Catalog |
| Title Added to Catalog | Restock Title (1) | Title Restocked; available = 1 |
| Title not in catalog | Update Title Price | Rejected |

Primary contract: [SLICE.md](./SLICE.md). Regenerating prompt: [AI_SKELETON_PROMPT.md](./AI_SKELETON_PROMPT.md).

## What AI must not invent

- Events or commands outside `SLICE.md`  
- Domain rules inside `Hosting/` or `Program.cs`  
- Cart / Place Order / payment / ship in this sample  
- A second parallel “Product” aggregate  

## vs Chapter 5

| | Ch 5 (`ch05-bookstore-first-cut`) | Ch 14 (`ch14-catalog-slice`) |
|--|------|------|
| Purpose | First runnable catalog | Skeleton layout for AI |
| Layout | Flat project | `Domain/` + `Hosting/` + `SLICE.md` |
| Event type list | Inline in tests/Program | `CatalogEventTypes.All` |
