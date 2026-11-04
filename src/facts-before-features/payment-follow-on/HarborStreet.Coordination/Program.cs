using HarborStreet.Coordination.Coordination;
using HarborStreet.Coordination.Domain;
using HarborStreet.Coordination.Events;
using HarborStreet.Coordination.Hosting;
using HarborStreet.Coordination.Payments;
using HarborStreet.Coordination.Seeding;
using Microsoft.Extensions.DependencyInjection;
using LawnDart.Aggregates;
using LawnDart.EventStore;

var dataDir = Path.Combine(Path.GetTempPath(), "harbor-street-payment", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dataDir);

var gateway = new FakePaymentGateway { NextCaptureSucceeds = false };
var services = new ServiceCollection();
services.AddHarborStreetCoordination(dataDir, gateway);

await using var provider = services.BuildServiceProvider();
var repo = provider.GetRequiredKeyedService<IAggregateRepository>(CoordinationComposition.ContextName);
var store = provider.GetRequiredKeyedService<IEventStore>(CoordinationComposition.ContextName);
var payments = provider.GetRequiredService<PaymentCaptureService>();

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var orderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var customerId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

await CheckoutSeed.SeedReservedOrderAsync(repo, titleId, orderId, customerId);

var titleBefore = await repo.GetOrCreateAsync<Title>(titleId);
Console.WriteLine("Harbor Street payment follow-on (payment failure, then release)");
Console.WriteLine($"  Before capture: available={titleBefore.State.AvailableUnits} reserved={titleBefore.State.ReservedUnits}");

await payments.CaptureAsync(orderId, amount: 52.00m);

var titleAfter = await repo.GetOrCreateAsync<Title>(titleId);
var order = await repo.GetOrCreateAsync<Order>(orderId);
var all = (await store.ReadByQueryAsync(Query.All())).Events;

Console.WriteLine($"  Gateway: declined");
Console.WriteLine($"  Order cancelled: {order.State.IsCancelled}");
Console.WriteLine($"  After reactions: available={titleAfter.State.AvailableUnits} reserved={titleAfter.State.ReservedUnits}");
Console.WriteLine($"  PaymentFailed: {all.Count(e => e.Event is PaymentFailed)}");
Console.WriteLine($"  OrderCancelled: {all.Count(e => e.Event is OrderCancelled)}");
Console.WriteLine($"  StockReleased: {all.Count(e => e.Event is StockReleased)}");
Console.WriteLine("  Teaching point: follow-on reactions — not inside Place Order.");