# Infrastructure

Shared LawnDart host registration for eShop services that own an Aspire SQL database.

## Register

Call `AddEShopLawnDart` from the service host. Pass the Aspire SQL connection name (`catalogdb`, `basketdb`, and the other names in `EShopSqlConnections`). Pass the service hook (`AddCatalogContext`, and the rest). Pass a subscription client from `EShopServiceNames` when the host has the Aspire `eventbus` reference.

Call `InitializeEShopLawnDartSqlAsync` after `Build` and before the host runs. That calls LawnDart `InitializeSchemaAsync` for the event log, transactional outbox, view store, checkpoints, and durable inbox. The calls are idempotent. LawnDart 0.5 has no versioned migrator.

```csharp
builder.AddServiceDefaults();
builder.AddEShopLawnDart(
    EShopSqlConnections.Catalog,
    context => context.AddCatalogContext(),
    EShopServiceNames.Catalog);

var app = builder.Build();
await app.InitializeEShopLawnDartSqlAsync();
await app.InitializeEShopRabbitMqAsync();
app.MapDefaultEndpoints();
app.Run();
```

SQL connection strings come from `ConnectionStrings:{name}`. The Aspire AppHost injects them through `WithReference`. The RabbitMQ URI is `ConnectionStrings:eventbus`.

## What this wires

One bounded context named `default` per process. SQL schema `dbo`.

- SQL event log (`UseSqlServer`)
- SQL view store and checkpoints (`AddSqlProjectionStores`)
- Transactional outbox (`EnableOutbox` plus `AddMessageTransportOutboxPublisher`). Pass `enableOutbox: false` when this process must not poll that outbox. OrderProcessor does this. It shares `orderingdb` with Ordering.API, and Ordering.API publishes that outbox.
- Durable inbox (`AddSqlInboxStore`)
- Sample RabbitMQ `IMessageTransport` when a subscription client is passed
- In-memory message transport when no subscription client is passed (Identity, and unit tests)

`RequireTenantId` is false, matching a single-tenant sample host.

## RabbitMQ adapter

`RabbitMqMessageTransport` implements LawnDart `IMessageTransport`. It is sample code in this project. LawnDart product packages ship InMemory messaging only.

Topology:

- Topic exchange `eshop.integration`
- One durable queue per service: `eshop.basket`, `eshop.catalog`, `eshop.ordering`, `eshop.order-processor`, `eshop.payment-processor`, `eshop.webhooks`, `eshop.webapp`
- Routing key is the event `EventTypeName` token
- Instances of one service share that queue (competing consumers)
- A second service has its own queue and receives its own copy

`InitializeEShopRabbitMqAsync` declares the exchange and that service's queue before a subscriber starts. Hosts that call `AddEShopLawnDart` run it next to `InitializeEShopLawnDartSqlAsync`. WebApp calls `InitializeEShopRabbitMqAsync` only. Publish and subscribe still declare the same objects on first use. Identity keeps InMemory. The helper returns when `RabbitMqMessageTransport` is absent.

The SQL outbox is the publish path. The SQL inbox is the consume path for reactors. `AddRabbitMQClient("eventbus")` supplies the Aspire `IConnection`. The connection client name is the subscription client.

Identity has no `eventbus` reference, so it keeps InMemory. WebApp registers the sample transport without the SQL host kit. `AddOrderStatusNotifications` calls `AddEShopMessageTransport(EShopServiceNames.WebApp)`. The subscription client is `webapp` and the queue is `eshop.webapp`. WebApp subscribes to order status events only. It has no SQL outbox or inbox. WebhookClient does not register the transport.

## Slice hooks

`AddIdentityContext`, `AddBasketContext`, `AddOrderingContext`, `AddOrderProcessorContext`, `AddPaymentContext`, and `AddWebhooksContext` take no assembly and leave the catalog empty. `AddCatalogContext()` does the same.

`AddCatalogContext(Assembly)` scans that assembly once for event types, command handlers, and projections. Catalog.API `Program.cs` passes its own assembly into the shared-assembly overload, inside `AddEShopLawnDart`. Later slices in that assembly are included. A second call throws because the catalog is already set. A slice does not call `WithEventTypes` or `WithCommandHandlers`.

`AddOrderingContext(Assembly, Assembly)` scans the Ordering.API assembly once for event types, command handlers, and projections, and scans the Shared assembly for integration events. Ordering.API `Program.cs` passes both, inside `AddEShopLawnDart`. A second call throws because the catalog is already set.

`AddPaymentContext(Assembly, Assembly)` does the same for PaymentProcessor. PaymentProcessor `Program.cs` passes both. The projection set is empty until a view slice adds a projection class.

`AddIdentityContext(Assembly)` scans that assembly once for event types, command handlers, and projections. Identity.API `Program.cs` passes its own assembly. `WithProjections` runs in that hook, after `WithEventTypes`.

`AddLawnDartHttpCommands` scans an assembly for HTTP commands. Each command host calls it once in `Program.cs` on the service assembly. `MapLawnDartCommands` stays once in `Program.cs`. That call maps `POST /api/catalog/define-catalog-brand`. PaymentProcessor leaves both calls out. Payment runs from integration events. View routes are the HTTP surface on that host.

Call `WithEventTypes` before `WithCommandHandlers` and `WithProjections`. Projection stores are already registered when the hook runs. The service hooks call `WithProjections` on the service assembly.

## Slice discovery

`AddEShopSlices(serviceAssembly)` registers every `IReactor<TEvent>` and `IEventProcessor<TEvent>` in that assembly. The hosted service opens one scope for each message and resolves the reactor or processor in that scope. It then calls `AddServices` on every `IEShopSliceModule` in the assembly, in full-name order. `MapEShopSlices(serviceAssembly)` calls `MapEndpoints` on those modules. A worker that has no HTTP pipeline skips `MapEShopSlices`. OrderProcessor does this.

The scan keeps the per-service queue, the SQL inbox, and the routing key from the event type token. `AddEShopLawnDart` already registers those. The inbox key matches LawnDart `AddReactor`: `reactor`, a hash of the reactor full name, and the message id. A second scan leaves one hosted service for each reactor and event pair. A missing `Apply` method on aggregate state throws the first time that event is applied.

LawnDart 0.5 resolves the SQL event store only after `WithEventTypes`. An empty hook gets `EShopHostCatalogEvent` (`eshop-host-catalog-ready`) so the service can start and create tables. A hook that calls `WithEventTypes` supplies the catalog, and the stand-in type is left out.

## Identity and storefront glue

Identity maps a demo auth stand-in: `GET /identity/callbacks` and `POST /identity/demo-token`. The token is the literal `demo-token`. It does not authenticate a caller. Account slices replace it. Callback URLs come from the AppHost environment names `BasketApiClient`, `OrderingApiClient`, `WebhooksApiClient`, `WebhooksWebClient`, and `WebAppClient`.

Identity opens `identitylogindb` for the ASP.NET Core Identity login store. `identitydb` stays the LawnDart event log. Startup calls `Database.EnsureCreatedAsync` on the login database. This repo does not ship an EF migration for it. The same startup creates `ShopperSigningKeys` when that table is missing and inserts one RSA private key when the table is empty. The key is not in source. A restart reads the same row. Identity publishes the public key at `GET /.well-known/jwks.json`.

Sign In Shopper returns a one-hour RSA JWT. `sub` is the shopper account id. `name` is the display name. Basket and Ordering call `AddShopperBearerAccess` and read `Identity:Url` (`Identity__Url` from the Identity endpoint in AppHost). They check the issuer, the audience, the lifetime, and the signature. Shopper routes require that bearer token. The gate runs after routing and uses the matched route pattern, so a trailing slash does not skip it. A buyer id or account id that is not `sub` gets 403. A shopper command with no buyer or account id gets 400. A body over 64 KB gets 413. A body that is not UTF-8 gets 415. Register Shopper and Sign In Shopper stay anonymous. Catalog routes are not on this gate. Cancel order requires the token and the order buyer must be `sub`. Mark paid, ship, stock confirmed, awaiting validation, and cancel for insufficient stock return 404 over HTTP. Reactors still call those handlers in process. LawnDart command mapping has no endpoint filter, so the check is middleware registered with the host. Identity publishes `GET /.well-known/openid-configuration` with `jwks_uri`. Basket and Ordering load that JWKS asynchronously. A failed fetch waits 30 seconds before another try.

Basket, Ordering, and Webhooks call `AddEShopIdentityUrl` for `Identity:Url` (`Identity__Url` in AppHost). When that value is an absolute URL, the host registers an HTTP client named `identity`.

WebApp calls `AddEShopStorefront`. That registers HTTP clients `catalog`, `basket`, and `ordering` at `https+http://catalog-api`, `https+http://basket-api`, and `https+http://ordering-api`. Service discovery from ServiceDefaults resolves those names. When `IdentityUrl` is an absolute URL, it also registers an HTTP client named `identity` at that address. `IdentityUrl` and `CallBackUrl` stay on `EShopStorefrontLinks`. The Blazor pages live in WebApp. Header display name and checkout address call `GET /views/shopper-account-profile` on the `identity` client. The catalog list, search, and item page call the `catalog` client. The cart page and the header bag call the `basket` client. Checkout submit posts Place Order on the `ordering` client. The orders page uses `OrdersByShopperClient` and refreshes from order status messages. See `WebApp/README.md`.

OrderProcessor and PaymentProcessor call `AddEShopAutomationShell`. The shell stays idle until a slice adds reactors.

## Redis

Redis `IViewStore` is deferred. Basket keeps SQL views. AppHost still provisions Redis.
