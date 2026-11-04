# ch17 — Versioning events and projections

## Objective

Show **additive evolution** for Harbor Street:

1. Historical `OrderPlaced` stays on disk.  
2. New writes use `OrderPlacedV2` (`HarborStreet.OrderPlaced.V2`) with warehouse + display name.  
3. **Read-time upcast** maps v1 → canonical v2 (unknown warehouse = `Guid.Empty`).  
4. **Projection families** Catalog v1/v2 and Order history v1/v2 rebuild from the same stream.
5. **Second way to version** (`BuiltInVersioning/`): one event name `HarborStreet.OrderPlaced` with numbered versions (`OrderPlacedV1` version 1, `OrderPlaced` version 2 current) and an upcaster registered with `WithUpcasters` in a separate bounded context (`orders-versioned`). A version 1 row reads back as the current shape with the warehouse unknown.

No silent rewrite of history.

## Run

```powershell
cd src/facts-before-features
dotnet run --project evolution/HarborStreet.Evolution/HarborStreet.Evolution.csproj
dotnet test evolution/HarborStreet.Evolution.Tests/HarborStreet.Evolution.Tests.csproj
```

## GWT

| Given | When | Then |
|-------|------|------|
| Stream has OrderPlaced (v1) + OrderPlacedV2 | Rebuild order history v2 | Both orders present; legacy `WarehouseKnown=false`; modern has warehouse |
| Title Added (scarce) | Rebuild catalog v1 and v2 | v1 has name/price; v2 adds ScarcityLabel |
| Views cleared | Rebuild again | Same upcasted rows (replay safety net) |
| Version 1 Order Placed stored by an older build | Today's build reads the stream | Current shape, warehouse unknown; stored record still says version 1 |
| Today's build | Append version 1 shape | Refused; only the current version is written |

## What AI must not invent

- Mutating or deleting old `OrderPlaced` bytes “to migrate”  
- One projection that breaks when warehouse is missing  
- Renaming the v1 event type name out from under stored events  
- Requiring warehouse on upcasted rows as if it were always known  

## Layout

```text
Events/           OrderPlaced + OrderPlacedV2 + TitleAddedToCatalog
Evolution/        OrderPlacedUpcaster
BuiltInVersioning/ One event name, numbered versions, registered upcaster
Projectors/       Catalog v1/v2, Order history v1/v2, RebuildAsync
Seeding/          Mixed history for *The Lighthouse Keeper*
```

Chapter 14 (next) covers deliberate breaking changes and heavier migrate paths; this sample stays on the additive/upcast happy path.
