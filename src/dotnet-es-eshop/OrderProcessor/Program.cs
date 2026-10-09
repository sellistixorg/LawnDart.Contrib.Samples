using System.Reflection;
using LawnDart.Samples.EShop.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Redis IViewStore is deferred. Basket keeps SQL views.
// Queue name differs from Ordering.API so the two processes do not compete for the same messages.
// One scan of this service assembly. Every OrderProcessor slice is included.
var processorAssembly = Assembly.GetExecutingAssembly();
builder.AddEShopLawnDart(
    EShopSqlConnections.Ordering,
    context => context.AddOrderProcessorContext(),
    EShopServiceNames.OrderProcessor,
    enableOutbox: false);
builder.Services.AddEShopAutomationShell(EShopServiceNames.OrderProcessor);
builder.AddEShopSlices(processorAssembly);

var host = builder.Build();

await host.InitializeEShopLawnDartSqlAsync();
await host.InitializeEShopRabbitMqAsync();

host.Run();
