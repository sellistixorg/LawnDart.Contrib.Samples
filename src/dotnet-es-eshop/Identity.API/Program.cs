using System.Reflection;
using LawnDart.AspNetCore;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.API.Seed;
using LawnDart.Samples.EShop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Identity has no Aspire eventbus reference, so this host keeps the InMemory transport.
// Redis IViewStore is deferred. Basket keeps SQL views.
// One scan of this service assembly. Every Identity slice is included.
var identityAssembly = Assembly.GetExecutingAssembly();
builder.AddShopperLoginStore();
builder.AddEShopLawnDart(
    EShopSqlConnections.Identity,
    context => context.AddIdentityContext(identityAssembly));
builder.Services.AddIdentityDemoAuth(builder.Configuration);
builder.Services.AddLawnDartHttpCommands(identityAssembly);
builder.AddEShopSlices(identityAssembly);
builder.Services.AddShopperAccessTokens();
builder.AddIdentitySeed();

var app = builder.Build();

await app.InitializeEShopLawnDartSqlAsync();
await app.InitializeShopperLoginStoreAsync();

app.MapDefaultEndpoints();
app.MapShopperJwks();
app.MapIdentityDemoAuth();
app.MapLawnDartCommands();
app.MapEShopSlices(identityAssembly);

app.Run();
