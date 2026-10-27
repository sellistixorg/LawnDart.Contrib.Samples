using System.Reflection;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEShopDatabaseLaunchOption(args);

builder.AddServiceDefaults();

// Redis IViewStore is deferred. Basket keeps SQL views.
// One scan of this service assembly. Every PaymentProcessor slice is included.
// Shared supplies integration events so the outbox can publish their tokens.
// Payment runs from integration events. View routes are the HTTP surface on this host.
var paymentAssembly = Assembly.GetExecutingAssembly();
builder.AddEShopLawnDart(
    EShopDatabases.Payment,
    context => context.AddPaymentContext(paymentAssembly, typeof(OrderStarted).Assembly),
    EShopServiceNames.PaymentProcessor);
builder.Services.AddEShopAutomationShell(EShopServiceNames.PaymentProcessor);
builder.AddEShopSlices(paymentAssembly);

var app = builder.Build();

await app.InitializeEShopLawnDartSchemaAsync();

app.MapDefaultEndpoints();
app.MapEShopSlices(paymentAssembly);

app.MapGet("/", () => Results.Ok(new { status = "scaffold", service = "payment-processor" }));

app.Run();
