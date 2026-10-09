var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Wave 4 leaves WebhookClient as a scaffold.
// It has no Aspire SQL database and no eventbus reference, so it does not call AddEShopLawnDart.
// Redis IViewStore is deferred.

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok(new { status = "scaffold", service = "webhooksclient" }));

app.Run();
