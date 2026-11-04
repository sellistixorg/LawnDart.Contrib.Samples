using HarborStreet.Catalog.Slice.Domain;
using HarborStreet.Catalog.Slice.Domain.Commands;
using HarborStreet.Catalog.Slice.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LawnDart.Aggregates;

// Thin host: composition + one happy-path demo. Domain rules live under Domain/.
var dataDir = Path.Combine(Path.GetTempPath(), "harbor-street-catalog-slice", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dataDir);

var services = new ServiceCollection();
services.AddCatalogSlice(dataDir);

await using var provider = services.BuildServiceProvider();
var repo = provider.GetRequiredKeyedService<IAggregateRepository>(CatalogComposition.ContextName);

var titleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

await repo.HandleCommandAsync(
    await repo.GetOrCreateAsync<Title>(titleId),
    new AddTitleToCatalogCommand(Guid.NewGuid(), titleId, "The Lighthouse Keeper", 52.00m, IsScarce: true));

await repo.HandleCommandAsync(
    await repo.GetOrCreateAsync<Title>(titleId),
    new RestockTitleCommand(Guid.NewGuid(), titleId, Quantity: 1));

var title = await repo.GetOrCreateAsync<Title>(titleId);
Console.WriteLine("Harbor Street catalog vertical slice");
Console.WriteLine($"  Title: {title.State.Name}");
Console.WriteLine($"  Available: {title.State.AvailableUnits}");
Console.WriteLine($"  Scarce: {title.State.IsScarce}");
Console.WriteLine($"  Layout: Domain/ + Hosting/ (see SLICE.md)");
