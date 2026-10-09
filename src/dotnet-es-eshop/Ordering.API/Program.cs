using System.Reflection;
using LawnDart.AspNetCore;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API;
using LawnDart.Samples.EShop.Shared.Events;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Redis IViewStore is deferred. Basket keeps SQL views.
// One scan of this service assembly. Every Ordering slice is included.
// Shared supplies integration events so the outbox can publish their tokens.
var orderingAssembly = Assembly.GetExecutingAssembly();
builder.AddEShopLawnDart(
    EShopSqlConnections.Ordering,
    context => context.AddOrderingContext(orderingAssembly, typeof(OrderStarted).Assembly),
    EShopServiceNames.Ordering);
builder.Services.AddEShopIdentityUrl(builder.Configuration);
builder.Services.AddShopperBearerAccess(builder.Configuration);
builder.Services.AddScoped<IShopperOrderBuyerLookup, ShopperOrderBuyerLookup>();
builder.Services.AddLawnDartHttpCommands(orderingAssembly);
builder.AddEShopSlices(orderingAssembly);

var app = builder.Build();

await app.InitializeEShopLawnDartSqlAsync();
await app.InitializeEShopRabbitMqAsync();

app.MapDefaultEndpoints();
app.MapLawnDartCommands();
app.MapEShopSlices(orderingAssembly);

app.MapGet("/", () => Results.Ok(new { status = "scaffold", service = "ordering-api" }));

app.Run();
