using HarborStreet.Catalog;
using HarborStreet.Catalog.Commands;
using HarborStreet.Catalog.Events;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Runnable first cut: add The Lighthouse Keeper, restock one unit.
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddLawnDart(o =>
{
    o.RequireTenantId = false;
    o.EnableAuthorization = false;
});
services
    .AddBoundedContext("default")
    .UseInMemory()
    .WithEventTypes<TitleAddedToCatalog>();

await using var provider = services.BuildServiceProvider();
var repo = provider.GetRequiredKeyedService<IAggregateRepository>("default");

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

await repo.HandleCommandAsync(
    await repo.GetOrCreateAsync<Title>(titleId),
    new AddTitleToCatalogCommand(Guid.NewGuid(), titleId, "The Lighthouse Keeper", 52.00m, IsScarce: true));

await repo.HandleCommandAsync(
    await repo.GetOrCreateAsync<Title>(titleId),
    new RestockTitleCommand(Guid.NewGuid(), titleId, Quantity: 1));

var title = await repo.GetOrCreateAsync<Title>(titleId);
Console.WriteLine("Harbor Street — ch05 catalog first cut");
Console.WriteLine($"  Title: {title.State.Name}");
Console.WriteLine($"  Price: {title.State.Price:C}");
Console.WriteLine($"  Available: {title.State.AvailableUnits}");
Console.WriteLine($"  Scarce: {title.State.IsScarce}");
