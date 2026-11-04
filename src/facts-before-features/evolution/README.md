# Versioning events and projections

## Objective

Additive change for Harbor Street:

1. Historical `OrderPlaced` stays on disk.
2. New writes use `OrderPlacedV2` (`HarborStreet.OrderPlaced.V2`) with a warehouse and a display name.
3. A read-time upcast maps v1 to canonical v2. Unknown warehouse is `Guid.Empty`.
4. Projection families (catalog v1 and v2, order history v1 and v2) rebuild from the same stream.
5. A second way to version lives in `BuiltInVersioning/`: one event name `HarborStreet.OrderPlaced`, numbered versions (`OrderPlacedV1` version 1, `OrderPlaced` version 2 current), and an upcaster registered with `WithUpcasters` in a separate bounded context (`orders-versioned`). A version 1 row reads back as the current shape with the warehouse unknown.

Old bytes stay as written.

## Run

```bash
cd src/facts-before-features
dotnet run --project evolution/HarborStreet.Evolution
dotnet test evolution/HarborStreet.Evolution.Tests
```

## GWT

| Given | When | Then |
|---|---|---|
| Stream has OrderPlaced (v1) and OrderPlacedV2 | Rebuild order history v2 | Both orders present; legacy `WarehouseKnown=false`; modern has a warehouse |
| Title Added (scarce) | Rebuild catalog v1 and v2 | v1 has name and price; v2 adds ScarcityLabel |
| Views cleared | Rebuild again | Same upcasted rows |
| Version 1 Order Placed stored by an older build | Today's build reads the stream | Current shape, warehouse unknown; the stored record still says version 1 |
| Today's build | Append the version 1 shape | Refused; only the current version is written |

## What AI must not invent

- Mutating or deleting old `OrderPlaced` bytes that are already stored
- One projection that breaks when warehouse is missing
- Renaming the v1 event type name out from under stored events
- Requiring a warehouse on upcasted rows as if it were always known

## Layout

```text
Events/            OrderPlaced, OrderPlacedV2, TitleAddedToCatalog
Evolution/         OrderPlacedUpcaster
BuiltInVersioning/ One event name, numbered versions, registered upcaster
Projectors/        Catalog v1 and v2, order history v1 and v2, RebuildAsync
Seeding/           Mixed history for The Lighthouse Keeper
```

This sample stays on the additive upcast path. Old events stay as written.
