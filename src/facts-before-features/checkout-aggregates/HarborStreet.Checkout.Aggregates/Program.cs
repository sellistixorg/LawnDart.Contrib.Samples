using HarborStreet.Checkout.Aggregates.Catalog;
using HarborStreet.Checkout.Aggregates.Catalog.Commands;
using HarborStreet.Checkout.Aggregates.Catalog.Events;
using HarborStreet.Checkout.Aggregates.Checkout;
using HarborStreet.Checkout.Aggregates.Hosting;
using HarborStreet.Checkout.Aggregates.Orders.Events;
using Microsoft.Extensions.DependencyInjection;
using LawnDart.Aggregates;
using LawnDart.EventStore;

// Demo: two Place Order attempts that both observed the last unit — aggregates + choreography pain.
var dataDir = Path.Combine(Path.GetTempPath(), "harbor-street-aggregates", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dataDir);

var services = new ServiceCollection();
services.AddAggregateCheckout(dataDir);

await using var provider = services.BuildServiceProvider();
var repo = provider.GetRequiredKeyedService<IAggregateRepository>(CheckoutComposition.ContextName);
var checkout = provider.GetRequiredService<AggregateCheckoutChoreography>();
var store = provider.GetRequiredKeyedService<IEventStore>(CheckoutComposition.ContextName);

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
await repo.HandleCommandAsync(
    await repo.GetOrCreateAsync<Title>(titleId),
    new AddTitleToCatalogCommand(Guid.NewGuid(), titleId, "The Lighthouse Keeper", 52.00m, IsScarce: true));
await repo.HandleCommandAsync(
    await repo.GetOrCreateAsync<Title>(titleId),
    new RestockTitleCommand(Guid.NewGuid(), titleId, Quantity: 1));

// Both customers "see" the last copy (stale observation), then Place Order + Reserve.
var observed = await repo.GetOrCreateAsync<Title>(titleId);
Console.WriteLine($"Observed available before race: {observed.State.AvailableUnits}");

var orderA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var orderB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
var customerA = Guid.Parse("a0000000-0000-0000-0000-000000000001");
var customerB = Guid.Parse("b0000000-0000-0000-0000-000000000002");

var resultA = await checkout.PlaceOrderAsync(orderA, customerA, titleId, skipStockCheck: true);
var resultB = await checkout.PlaceOrderAsync(orderB, customerB, titleId, skipStockCheck: true);

var all = (await store.ReadByQueryAsync(Query.All())).Events;
var placed = all.Count(e => e.Event is OrderPlaced);
var reserved = all.Count(e => e.Event is StockReserved);
var title = await repo.GetOrCreateAsync<Title>(titleId);

Console.WriteLine("Harbor Street aggregates checkout (last-copy race)");
Console.WriteLine($"  A: {resultA.Status}");
Console.WriteLine($"  B: {resultB.Status}");
Console.WriteLine($"  OrderPlaced events: {placed}");
Console.WriteLine($"  StockReserved events: {reserved}");
Console.WriteLine($"  Available units: {title.State.AvailableUnits}");
Console.WriteLine($"  Reserved units: {title.State.ReservedUnits}");
Console.WriteLine("  Teaching point: more OrderPlaced than StockReserved → compensation required.");