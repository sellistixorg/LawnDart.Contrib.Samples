using System.Reflection;
using LawnDart.AspNetCore;
using LawnDart.Samples.EShop.Catalog.API.Seed;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEShopDatabaseLaunchOption(args);

builder.AddServiceDefaults();

// Redis IViewStore is deferred. Basket keeps SQL views.
// One scan of this service assembly. Every Catalog slice is included.
// Shared supplies integration events so the outbox can publish their tokens.
var catalogAssembly = Assembly.GetExecutingAssembly();
builder.AddEShopLawnDart(
    EShopDatabases.Catalog,
    context => context.AddCatalogContext(catalogAssembly, typeof(OrderStockConfirmed).Assembly),
    EShopServiceNames.Catalog);

builder.Services.AddLawnDartHttpCommands(catalogAssembly);
builder.AddEShopSlices(catalogAssembly);
builder.AddCatalogSeed();

var app = builder.Build();

await app.InitializeEShopLawnDartSchemaAsync();

app.MapDefaultEndpoints();
app.MapLawnDartCommands();
app.MapEShopSlices(catalogAssembly);
app.MapCatalogItemPictures();

app.MapGet("/", () => Results.Ok(new { status = "scaffold", service = "catalog-api" }));

app.Run();
