using HarborStreet.EndToEnd.Bookstore;
using HarborStreet.EndToEnd.Checkout;
using HarborStreet.EndToEnd.Domain;
using HarborStreet.EndToEnd.Events;
using HarborStreet.EndToEnd.Hosting;
using HarborStreet.EndToEnd.Payments;
using Microsoft.Extensions.DependencyInjection;
using LawnDart.Dcb;
using LawnDart.EventStore;

var dataDir = Path.Combine(Path.GetTempPath(), "harbor-street-end-to-end", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dataDir);

var gateway = new FakePaymentGateway { NextCaptureSucceeds = true };
var services = new ServiceCollection();
services.AddHarborStreetEndToEnd(dataDir, gateway);

await using var provider = services.BuildServiceProvider();
var bookstore = provider.GetRequiredService<HarborStreetBookstore>();
var dcb = provider.GetRequiredKeyedService<IDcbRepository>(EndToEndComposition.ContextName);
var store = provider.GetRequiredKeyedService<IEventStore>(EndToEndComposition.ContextName);

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
await bookstore.SeedLastCopyAsync(titleId);

// --- Last-copy race ---
var query = InventoryDecision.ForTitle(titleId);
var observedA = await dcb.GetOrCreateEntityAsync<InventoryDecision>(query);
var observedB = await dcb.GetOrCreateEntityAsync<InventoryDecision>(query);

var orderA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var orderB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
var race = await Task.WhenAll(
    bookstore.PlaceOrderAsync(orderA, Guid.Parse("a0000000-0000-0000-0000-000000000001"), titleId, preloaded: observedA),
    bookstore.PlaceOrderAsync(orderB, Guid.Parse("b0000000-0000-0000-0000-000000000002"), titleId, preloaded: observedB));

var winner = race.Single(r => r.Status == CheckoutStatus.Succeeded);
await bookstore.CapturePaymentAsync(winner.OrderId, 52.00m);

var inventory = await bookstore.GetInventoryAsync(titleId);
var order = await bookstore.GetOrderAsync(winner.OrderId);
var all = (await store.ReadByQueryAsync(Query.All())).Events;

Console.WriteLine("Harbor Street end to end (last-copy and payment)");
Console.WriteLine($"  Race A: {race[0].Status}");
Console.WriteLine($"  Race B: {race[1].Status}");
Console.WriteLine($"  StockReserved: {all.Count(e => e.Event is StockReserved)}");
Console.WriteLine($"  Winner paid: {order.State.IsPaid}; fulfillment: {order.State.FulfillmentStarted}");
Console.WriteLine($"  Inventory available/reserved: {inventory.State.AvailableUnits}/{inventory.State.ReservedUnits}");
Console.WriteLine($"  FulfillmentStarted events: {all.Count(e => e.Event is FulfillmentStarted)}");
Console.WriteLine("  Teaching point: DCB last-copy win, then payment follow-on — not inside Place Order.");