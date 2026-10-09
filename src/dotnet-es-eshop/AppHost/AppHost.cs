var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure resources.
// SQL Server is the durable LawnDart backend (event log, views, checkpoints, outbox, inbox).
// The container image is mcr.microsoft.com/mssql/server:2025-latest.
// Service hosts read the Aspire SQL connection string in AddEShopLawnDart.
// Publisher and consumer hosts read eventbus for the sample RabbitMQ IMessageTransport.
// That adapter lives in this sample. It is not a LawnDart product transport.
// Redis IViewStore is deferred. Basket keeps SQL views.

var redis = builder.AddRedis("redis")
    .WithDataVolume();

var rabbitMq = builder.AddRabbitMQ("eventbus")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var sql = builder.AddSqlServer("sql")
    .WithImageTag("2025-latest")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// identitydb is the LawnDart event log. identitylogindb is the ASP.NET Core Identity login store.
var identityDb = sql.AddDatabase("identitydb");
var identityLoginDb = sql.AddDatabase("identitylogindb");
var catalogDb = sql.AddDatabase("catalogdb");
var basketDb = sql.AddDatabase("basketdb");
var orderingDb = sql.AddDatabase("orderingdb");
var paymentDb = sql.AddDatabase("paymentdb");
var webhooksDb = sql.AddDatabase("webhooksdb");

var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";

// Service projects. Each API hosts its slices under Slices/.
var identityApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Identity_API>("identity-api")
    .WithExternalHttpEndpoints()
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(identityLoginDb).WaitFor(identityLoginDb)
    .WithHttpHealthCheck("/health");

var identityEndpoint = identityApi.GetEndpoint(launchProfileName);

var basketApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Basket_API>("basket-api")
    .WithReference(redis) // Redis IViewStore is deferred. Basket keeps SQL views.
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(basketDb).WaitFor(basketDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);
redis.WithParentRelationship(basketApi);

var catalogApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Catalog_API>("catalog-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

var orderingApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Ordering_API>("ordering-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderingDb).WaitFor(orderingDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);

builder.AddProject<Projects.LawnDart_Samples_EShop_OrderProcessor>("order-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderingDb).WaitFor(orderingDb)
    .WaitFor(orderingApi);

builder.AddProject<Projects.LawnDart_Samples_EShop_PaymentProcessor>("payment-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(paymentDb).WaitFor(paymentDb)
    .WithHttpHealthCheck("/health");

var webHooksApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Webhooks_API>("webhooks-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(webhooksDb).WaitFor(webhooksDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);

// Optional: Yarp mobile-bff can be added later to match upstream eShop mobile routes.
// builder.AddYarp("mobile-bff")...

var webhooksClient = builder.AddProject<Projects.LawnDart_Samples_EShop_WebhookClient>("webhooksclient")
    .WithReference(webHooksApi)
    .WithEnvironment("IdentityUrl", identityEndpoint);

var webApp = builder.AddProject<Projects.LawnDart_Samples_EShop_WebApp>("webapp")
    .WithExternalHttpEndpoints()
    .WithReference(basketApi)
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WithReference(rabbitMq).WaitFor(rabbitMq) // order status messages for the orders page
    .WaitFor(identityApi)
    .WithEnvironment("IdentityUrl", identityEndpoint);

// Callback URLs (self-referencing), matching original eShop AppHost wiring.
webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));
webhooksClient.WithEnvironment("CallBackUrl", webhooksClient.GetEndpoint(launchProfileName));

// Identity callback URLs. The demo auth stand-in reads these. Account slices replace that stand-in later.
identityApi.WithEnvironment("BasketApiClient", basketApi.GetEndpoint("http"))
           .WithEnvironment("OrderingApiClient", orderingApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksApiClient", webHooksApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksWebClient", webhooksClient.GetEndpoint(launchProfileName))
           .WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName));

builder.Build().Run();

static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "ESHOP_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);
    return int.TryParse(envValue, out int result) && result == 1;
}
