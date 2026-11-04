# Chapter samples

Incremental, chapter-aligned codebases for Harbor Street Books.

Runtime is **LawnDart 0.7.0-alpha.3** from nuget.org. No sibling clone and no private feed.

## Build shell

| File | Role |
|------|------|
| `HarborStreet.Samples.slnx` | Solution |
| `Directory.Build.props` | `net10.0` |
| `Directory.Packages.props` | Central package versions (LawnDart + test packages) |
| `LawnDart.refs.props` | Import from chapter `.csproj` files |
| `NuGet.Config` | nuget.org only |
| `_shell/HarborStreet.Shell/` | Smoke project — must build |

```powershell
cd src/facts-before-features
dotnet restore HarborStreet.Samples.slnx
dotnet build HarborStreet.Samples.slnx
dotnet test HarborStreet.Samples.slnx
dotnet run --project smoke-check/HarborStreet.Shell/HarborStreet.Shell.csproj
```

Each chapter host imports:

```xml
<Import Project="..\..\LawnDart.refs.props" />
```

(Adjust `..\..\` if nested differently.) Hosts call `AddLawnDart` + `UseInMemory` + `WithEventTypes`. Tests use `BddTestContext.CreateInMemory`.

More runtime detail: [LawnDart](https://github.com/sellistixorg/LawnDart).

---

## Sample index

| Folder | Chapter | Status |
|--------|---------|--------|
| `_shell/` | — | smoke build |
| `_book-support/` | Tooling (Quarto includes) | stub |
| `ch05-bookstore-first-cut/` | 5 | Title catalog aggregate + tests |
| `ch08-projections/` | 8 | Catalog, stock dashboard, order history from events |
| `ch10-aggregates-checkout/` | 9 (setup) / 10 | Last-copy race via Order+Title choreography (orphan order) |
| `ch11-dcb-checkout/` | 11 | Last-copy DCB decision; exactly one winner |
| `ch12-coordination/` | 12 | Payment follow-on: fulfill or cancel+release |
| `ch14-catalog-slice/` | 14 | Domain/Hosting skeleton + SLICE.md + tests |
| `ch16-iteration/` | 16 | Multi-warehouse stock (bookstore-v2) |
| `ch17-evolution/` | 17–18 | OrderPlaced upcast + catalog/order projection families |
| `ch19-end-to-end/` | 19 | DCB last-copy + payment follow-on facade |

Each chapter sample README: **Objective**, **Run**, **GWT**, **What AI must not invent**.

See [`docs/IMPLEMENTATION_PLAN.md`](../../docs/IMPLEMENTATION_PLAN.md) and [`docs/BOOK_SPINE.md`](../../docs/BOOK_SPINE.md).
