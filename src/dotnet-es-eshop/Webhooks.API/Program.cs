using LawnDart.Samples.EShop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEShopDatabaseLaunchOption(args);
var schemaSetup = EShopSchemaSetup.IsRequested(args);
if (schemaSetup)
    builder.Configuration.Mark();

builder.AddServiceDefaults();

// Wave 4 leaves this host as a scaffold. Redis IViewStore is deferred.
builder.AddEShopLawnDart(
    EShopDatabases.Webhooks,
    context => context.AddWebhooksContext(),
    EShopServiceNames.Webhooks);
builder.Services.AddEShopIdentityUrl(builder.Configuration);

var app = builder.Build();

if (schemaSetup)
{
    await app.InitializeEShopLawnDartSchemaAsync();
    await app.DisposeAsync();
    return;
}

await app.VerifyEShopLawnDartSchemaAsync();

app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok(new { status = "scaffold", service = "webhooks-api" }));

app.Run();
