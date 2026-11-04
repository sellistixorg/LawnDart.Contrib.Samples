using HarborStreet.Iteration.Domain;
using HarborStreet.Iteration.Events;
using HarborStreet.Iteration.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LawnDart.Dcb;
using LawnDart.EventStore;

var dataDir = Path.Combine(Path.GetTempPath(), "harbor-street-warehouses", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dataDir);

var services = new ServiceCollection();
services.AddHarborStreetIteration(dataDir);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var sp = scope.ServiceProvider;

var checkout = IterationComposition.CreateCheckout(sp);
var dcb = sp.GetRequiredKeyedService<IDcbRepository>(IterationComposition.ContextName);
var store = sp.GetRequiredKeyedService<IEventStore>(IterationComposition.ContextName);

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var harborId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var downtownId = Guid.Parse("33333333-3333-3333-3333-333333333333");

await checkout.EnsureTitleAsync(titleId, "The Lighthouse Keeper", 52.00m, scarce: true);
await checkout.OpenWarehouseAsync(harborId, "Harbor");
await checkout.OpenWarehouseAsync(downtownId, "Downtown");
await checkout.RestockAsync(harborId, titleId, 1);
// Downtown deliberately has zero units.

var downtownOrder = await checkout.PlaceOrderAsync(
    Guid.NewGuid(), Guid.NewGuid(), downtownId, titleId);

var tags = WarehouseInventoryDecision.BoundaryTags(harborId, titleId);
var a = await dcb.GetOrCreateEntityAsync<WarehouseInventoryDecision>(tags);
var b = await dcb.GetOrCreateEntityAsync<WarehouseInventoryDecision>(tags);

var race = await Task.WhenAll(
    checkout.PlaceOrderAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.NewGuid(), harborId, titleId, preloaded: a),
    checkout.PlaceOrderAsync(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Guid.NewGuid(), harborId, titleId, preloaded: b));

var all = (await store.ReadByQueryAsync(Query.All())).Events;
var harborAfter = await dcb.GetOrCreateEntityAsync<WarehouseInventoryDecision>(tags);

Console.WriteLine("Harbor Street multi-warehouse (model v2)");
Console.WriteLine($"  Downtown Place Order: {downtownOrder.Status}");
Console.WriteLine($"  Harbor race: {race[0].Status} / {race[1].Status}");
Console.WriteLine($"  OrderPlaced: {all.Count(e => e.Event is OrderPlaced)}");
Console.WriteLine($"  StockReservedAtWarehouse: {all.Count(e => e.Event is StockReservedAtWarehouse)}");
Console.WriteLine($"  Harbor available/reserved: {harborAfter.State.AvailableUnits}/{harborAfter.State.ReservedUnits}");
Console.WriteLine("  Teaching point: stock is per warehouse; last-copy race is local to Harbor.");