using HarborStreet.Projections.Hosting;
using HarborStreet.Projections.Projectors;
using HarborStreet.Projections.Seeding;
using Microsoft.Extensions.DependencyInjection;
using LawnDart.EventStore;

var dataDir = Path.Combine(Path.GetTempPath(), "harbor-street-projections", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dataDir);

var services = new ServiceCollection();
services.AddHarborStreetProjections(dataDir);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var sp = scope.ServiceProvider;

var store = sp.GetRequiredKeyedService<IEventStore>(ProjectionsComposition.ContextName);
var pipeline = sp.GetRequiredService<ProjectionPipeline>();

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var orderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var customerId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

await new DemoEventSeeder(store).SeedLighthouseKeeperStoryAsync(titleId, orderId, customerId);
await pipeline.RebuildAsync(store);

var catalog = pipeline.Store.Catalog[titleId];
var stock = pipeline.Store.Stock[titleId];
var order = pipeline.Store.Orders[orderId];

Console.WriteLine("Harbor Street projections (read models from events)");
Console.WriteLine("  Catalog:");
Console.WriteLine($"    {catalog.Name} — {catalog.Price:C} — scarce={catalog.IsScarce}");
Console.WriteLine("  Stock dashboard:");
Console.WriteLine($"    available={stock.Available} reserved={stock.Reserved}");
Console.WriteLine("  Order history:");
Console.WriteLine($"    order {order.OrderId.ToString()[..8]}… qty={order.Quantity} title={order.TitleId.ToString()[..8]}…");
Console.WriteLine("  Teaching point: views are derived; clear + RebuildAsync replays the same facts.");