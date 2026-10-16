using System.Reflection;
using LawnDart.AspNetCore;
using LawnDart.Samples.EShop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Basket reads stay on SQL views. Redis IViewStore is deferred.
// One scan of this service assembly. Every Basket slice is included.
var basketAssembly = Assembly.GetExecutingAssembly();
builder.AddEShopLawnDart(
    EShopSqlConnections.Basket,
    context => context.AddBasketContext(basketAssembly),
    EShopServiceNames.Basket);
builder.Services.AddEShopIdentityUrl(builder.Configuration);
builder.Services.AddShopperBearerAccess(builder.Configuration);
builder.Services.AddLawnDartHttpCommands(basketAssembly);
builder.AddEShopSlices(basketAssembly);

var app = builder.Build();

await app.InitializeEShopLawnDartSqlAsync();

app.MapDefaultEndpoints();
app.MapLawnDartCommands();
app.MapEShopSlices(basketAssembly);

app.MapGet("/", () => Results.Ok(new { status = "scaffold", service = "basket-api" }));

app.Run();
