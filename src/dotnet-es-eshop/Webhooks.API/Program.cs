using LawnDart.Samples.EShop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEShopDatabaseLaunchOption(args);

builder.AddServiceDefaults();

// Wave 4 leaves this host as a scaffold. Redis IViewStore is deferred.
builder.AddEShopLawnDart(
    EShopDatabases.Webhooks,
    context => context.AddWebhooksContext(),
    EShopServiceNames.Webhooks);
builder.Services.AddEShopIdentityUrl(builder.Configuration);

var app = builder.Build();

await app.InitializeEShopLawnDartSchemaAsync();

app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok(new { status = "scaffold", service = "webhooks-api" }));

app.Run();
