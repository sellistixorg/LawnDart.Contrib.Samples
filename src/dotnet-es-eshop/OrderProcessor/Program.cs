using System.Reflection;
using LawnDart.Samples.EShop.Infrastructure;

if (EShopSchemaSetup.IsRequested(args))
{
    throw new InvalidOperationException(
        "Order Processor does not create tables. " +
        "Run Ordering.API with " + EShopSchemaSetup.Argument + " for orderingdb, then start Order Processor.");
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEShopDatabaseLaunchOption(args);

builder.AddServiceDefaults();

// Redis IViewStore is deferred. Basket keeps SQL views.
// Subscription name differs from Ordering.API so the two processes do not compete for the same messages.
// One scan of this service assembly. Every OrderProcessor slice is included.
var processorAssembly = Assembly.GetExecutingAssembly();
builder.AddEShopLawnDart(
    EShopDatabases.Ordering,
    context => context.AddOrderProcessorContext(),
    EShopServiceNames.OrderProcessor,
    enableOutbox: false);
builder.Services.AddEShopAutomationShell(EShopServiceNames.OrderProcessor);
builder.AddEShopSlices(processorAssembly);

var host = builder.Build();

await host.VerifyEShopLawnDartSchemaAsync();

host.Run();
