using HarborStreet.Evolution.BuiltInVersioning;
using HarborStreet.Evolution.Evolution;
using HarborStreet.Evolution.Hosting;
using HarborStreet.Evolution.Projectors;
using HarborStreet.Evolution.Seeding;
using Microsoft.Extensions.DependencyInjection;
using LawnDart.EventStore;

var dataDir = Path.Combine(Path.GetTempPath(), "harbor-street-ch17", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dataDir);

var services = new ServiceCollection();
services.AddHarborStreetEvolution(dataDir);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var sp = scope.ServiceProvider;

var store = sp.GetRequiredKeyedService<IEventStore>(EvolutionComposition.ContextName);
var pipeline = sp.GetRequiredService<EvolutionPipeline>();

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var legacyOrder = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var modernOrder = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
var warehouseId = Guid.Parse("22222222-2222-2222-2222-222222222222");

await new EvolutionSeeder(store).SeedMixedHistoryAsync(titleId, legacyOrder, modernOrder, warehouseId);
await pipeline.RebuildAsync(store);

var catalogV2 = pipeline.Store.CatalogV2[titleId];
var legacyV2 = pipeline.Store.OrdersV2[legacyOrder];
var modernV2 = pipeline.Store.OrdersV2[modernOrder];

Console.WriteLine("Harbor Street - ch17 evolution (upcast + projection families)");
Console.WriteLine($"  Catalog v2: {catalogV2.Name} — {catalogV2.ScarcityLabel}");
Console.WriteLine($"  Legacy order upcast: warehouseKnown={legacyV2.WarehouseKnown} (Empty={OrderPlacedUpcaster.UnknownWarehouseId == legacyV2.WarehouseId})");
Console.WriteLine($"  Modern order: warehouse={modernV2.WarehouseId.ToString()[..8]}… display={modernV2.CustomerDisplayName}");
Console.WriteLine($"  Orders v1 count: {pipeline.Store.OrdersV1.Count}; Orders v2 count: {pipeline.Store.OrdersV2.Count}");
// Second approach: same event name, numbered versions, upcaster registered with the tooling.
var versionedLog = sp.GetRequiredKeyedService<IEventLog>(BuiltInVersioningComposition.ContextName);
var versionedStore = sp.GetRequiredKeyedService<IEventStore>(BuiltInVersioningComposition.ContextName);
var builtInStream = $"harbor-street:orders-versioned:{legacyOrder:N}";
await LegacyOrderHistory.WriteVersion1Async(versionedLog, builtInStream,
    new OrderPlacedV1(Guid.NewGuid(), DateTime.UtcNow, legacyOrder, Guid.NewGuid(), titleId, 1));
var readBack = (await versionedStore.ReadStreamAsync(builtInStream)).Single();
var current = (HarborStreet.Evolution.BuiltInVersioning.OrderPlaced)readBack.Event;
Console.WriteLine($"  Built-in versions: stored v{readBack.Metadata.SchemaVersion}, read as {current.GetType().Name}, warehouseKnown={current.WarehouseId != OrderPlacedV1ToCurrent.UnknownWarehouseId}");
Console.WriteLine("  Teaching point: do not rewrite history — upcast at read; rebuild projection families.");