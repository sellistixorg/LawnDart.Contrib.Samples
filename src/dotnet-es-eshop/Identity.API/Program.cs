using System.Reflection;
using LawnDart.AspNetCore;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.API.Seed;
using LawnDart.Samples.EShop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEShopDatabaseLaunchOption(args);
var schemaSetup = EShopSchemaSetup.IsRequested(args);
if (schemaSetup)
    builder.Configuration.Mark();

builder.AddServiceDefaults();

// Identity has no Aspire eventbus reference, so this host keeps the InMemory transport.
// Redis IViewStore is deferred. Basket keeps SQL views.
// One scan of this service assembly. Every Identity slice is included.
var identityAssembly = Assembly.GetExecutingAssembly();
builder.AddShopperLoginStore();
builder.AddEShopLawnDart(
    EShopDatabases.Identity,
    context => context.AddIdentityContext(identityAssembly));
builder.Services.AddIdentityDemoAuth(builder.Configuration);
builder.Services.AddLawnDartHttpCommands(identityAssembly);
builder.AddEShopSlices(identityAssembly);
builder.Services.AddShopperAccessTokens();
if (!schemaSetup)
    builder.AddIdentitySeed();

var app = builder.Build();

if (schemaSetup)
{
    await app.InitializeEShopLawnDartSchemaAsync();
    await app.InitializeShopperLoginStoreAsync();
    await app.DisposeAsync();
    return;
}

await app.VerifyEShopLawnDartSchemaAsync();
await app.VerifyShopperLoginStoreAsync();

app.MapDefaultEndpoints();
app.MapShopperJwks();
app.MapIdentityDemoAuth();
app.MapLawnDartCommands();
app.MapEShopSlices(identityAssembly);

app.Run();
