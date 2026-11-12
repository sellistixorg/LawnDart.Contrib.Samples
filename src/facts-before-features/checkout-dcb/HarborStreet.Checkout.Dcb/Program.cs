using HarborStreet.Checkout.Dcb;
using HarborStreet.Checkout.Dcb.Checkout;
using HarborStreet.Checkout.Dcb.Events;
using HarborStreet.Checkout.Dcb.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LawnDart.Dcb;
using LawnDart.EventStore;

// Demo: concurrent Place Order against one scarce unit — DCB exactly-one-winner.
var dataDir = Path.Combine(Path.GetTempPath(), "harbor-street-dcb", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dataDir);

var services = new ServiceCollection();
services.AddDcbCheckout(dataDir);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var sp = scope.ServiceProvider;

var dcb = sp.GetRequiredKeyedService<IDcbRepository>(DcbCheckoutComposition.ContextName);
var store = sp.GetRequiredKeyedService<IEventStore>(DcbCheckoutComposition.ContextName);
var checkout = new DcbCheckoutService(dcb);

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
await checkout.SeedLastCopyAsync(titleId);

var query = CheckoutDecision.ForTitle(titleId);
var observedA = await dcb.GetOrCreateEntityAsync<CheckoutDecision>(query);
var observedB = await dcb.GetOrCreateEntityAsync<CheckoutDecision>(query);
Console.WriteLine($"Observed available (A/B): {observedA.State.AvailableUnits}/{observedB.State.AvailableUnits}");

var orderA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var orderB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
var customerA = Guid.Parse("a0000000-0000-0000-0000-000000000001");
var customerB = Guid.Parse("b0000000-0000-0000-0000-000000000002");

// Same stale decision state, then race the appends (AppendCondition).
var results = await Task.WhenAll(
    checkout.PlaceOrderAsync(orderA, customerA, titleId, preloaded: observedA),
    checkout.PlaceOrderAsync(orderB, customerB, titleId, preloaded: observedB));

var all = (await store.ReadByQueryAsync(Query.All())).Events;
var placed = all.Count(e => e.Event is OrderPlaced);
var reserved = all.Count(e => e.Event is StockReserved);
var decision = await dcb.GetOrCreateEntityAsync<CheckoutDecision>(query);

Console.WriteLine("Harbor Street DCB checkout (last-copy race)");
Console.WriteLine($"  A: {results[0].Status}");
Console.WriteLine($"  B: {results[1].Status}");
Console.WriteLine($"  OrderPlaced events: {placed}");
Console.WriteLine($"  StockReserved events: {reserved}");
Console.WriteLine($"  Available units: {decision.State.AvailableUnits}");
Console.WriteLine($"  Reserved units: {decision.State.ReservedUnits}");
Console.WriteLine("  Teaching point: exactly one winner; loser leaves no order (no compensation saga).");