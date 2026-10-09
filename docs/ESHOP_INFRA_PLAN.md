# eShop infrastructure

Hosting design for `src/dotnet-es-eshop`.

`dotnet run --project AppHost` starts Identity, Catalog, Basket, Ordering, OrderProcessor, PaymentProcessor, Webhooks, WebhookClient, and WebApp. Each API that owns a database registers LawnDart against its Aspire SQL Server database. Cross-service messages travel RabbitMQ through the sample `IMessageTransport` in `Infrastructure/`, with a SQL outbox and a SQL inbox.

`aspire run` needs an Aspire CLI that matches AppHost 13.6.1. `dotnet run --project AppHost` uses the AppHost SDK pin (`AspireUseCliBundle=true`).

## What the host runs

- Aspire AppHost and ServiceDefaults (Aspire 13.6.1).
- SQL Server (`mcr.microsoft.com/mssql/server:2025-latest`), Redis, and RabbitMQ in the AppHost.
- LawnDart packages pinned to `0.5.0-alpha.1`.
- One registration helper, `AddEShopLawnDart`, used by Catalog, Basket, Ordering, Identity, OrderProcessor, PaymentProcessor, and Webhooks.
- SQL schema init on startup. `InitializeEShopLawnDartSqlAsync` calls the package `InitializeSchemaAsync`. LawnDart 0.5 has no versioned migrator. Re-runs are idempotent DDL.
- Per-service bounded-context hooks (`AddCatalogContext`, and the rest). The service `Program.cs` calls the hook. An assembly scan runs once there.
- ServiceDefaults health, OpenTelemetry, discovery, and resilience as the outer host shell.

WebApp and WebhookClient do not call the host kit. Neither has a SQL database.

Empty hooks do not call `WithEventTypes`. LawnDart 0.5 resolves the SQL event store only after a catalog exists. The host registers `EShopHostCatalogEvent` in that case so the process can start and create tables. A hook that calls `WithEventTypes` supplies the catalog.

OrderProcessor shares `orderingdb` with Ordering.API.

## Cross-process bus

Original eShop uses RabbitMQ as the integration bus. LawnDart product packages ship an in-memory `IMessageTransport`. The adapter below is sample code under `Infrastructure/`.

1. RabbitMQ `IMessageTransport` lives in `src/dotnet-es-eshop/Infrastructure/`.
2. Publisher and consumer hosts register it from the Aspire `eventbus` connection: Basket, Catalog, Ordering, OrderProcessor, PaymentProcessor, and Webhooks. Identity has no eventbus reference and keeps the in-memory transport. WebApp registers the sample transport with subscription client `webapp` (`AddOrderStatusNotifications` calls `AddEShopMessageTransport(EShopServiceNames.WebApp)`). It subscribes to order status events only and has no SQL outbox or inbox.
3. Publish goes through the SQL outbox. Consume goes through the SQL inbox.
4. Subscription client names are `basket`, `catalog`, `ordering`, `order-processor`, `payment-processor`, `webhooks`, and `webapp`. Each name is its own durable queue on the `eshop.integration` topic exchange. `InitializeEShopRabbitMqAsync` declares that exchange and queue at startup. Hosts that call `AddEShopLawnDart` run it next to `InitializeEShopLawnDartSqlAsync`. WebApp calls `InitializeEShopRabbitMqAsync` only. Publish and subscribe still declare them on first use.
5. `RabbitMqTransportSmokeTests` covers outbox publish to RabbitMQ and a second queue, reactor inbox dedupe, and competing consumers on one queue. The test runs when RabbitMQ is reachable (`ESHOP_RABBITMQ`, or `amqp://guest:guest@127.0.0.1:5672`). Routing, codec, and DI tests run without a broker. Set `ESHOP_REQUIRE_RABBITMQ=1` to fail that smoke test when the broker is down.

## Redis

Deferred. Basket keeps the SQL view store from the host kit. AppHost still provisions Redis. Redis is not a LawnDart product store.

The open choice is whether Basket stays on SQL views or later uses a Redis view store for the original Redis-as-basket shape. The current choice is SQL views.

## Identity and WebApp

Identity signs a shopper in with ASP.NET Identity and returns an access token. WebApp stores that token in an HttpOnly cookie and sends `Authorization: Bearer` on Basket, Ordering, and Identity calls. The Identity host still maps `POST /identity/demo-token`.

Sign-in returns an access token. Catalog serves item pictures at `GET /api/catalog/items/{id}/pic`. WebApp forwards `GET /product-images/{id}` to that route.

Callback URLs come from the AppHost (`BasketApiClient`, `OrderingApiClient`, `WebhooksApiClient`, `WebhooksWebClient`, `WebAppClient`).

WebApp uses Aspire service-discovery HTTP clients for Catalog, Basket, and Ordering (`https+http://catalog-api`, `basket-api`, `ordering-api`). `IdentityUrl` and `CallBackUrl` come from the AppHost. The storefront is Blazor interactive server. Storefront pages call `GET /views/{slice-name}` on the owning service. Screen rules: `.cursor/rules/eshop-webapp.mdc`.

OrderProcessor runs the grace-period timer and publishes `GracePeriodConfirmed`. PaymentProcessor charges through a simulated gateway. Both hosts still register slice reactors on the context hook.

Webhooks and WebhookClient stay scaffolds. Webhooks binds `Identity:Url` because the AppHost already injects it.

## Slice boundary

A slice adds or changes that slice folder, its tests, and shared event types that slice owns. A slice does not reshape AppHost, ServiceDefaults, the transport adapter, or host kit registration.

Model order is Identity, then Catalog, Basket, Ordering, and Payment, plus the OrderProcessor grace period. Webhooks stay later.

## Out of scope for the host

- Changing LawnDart product packages beyond the public `0.5.0-alpha.1` APIs.
- Treating Redis or RabbitMQ as a LawnDart product store or transport. Postgres is not used here.

## Known gaps

1. The SQL outbox publishes every appended event. `OrderPlaced` goes on the bus with the ship-to address, the cardholder name, the last four digits, and the expiry.
2. A service queue binds to a routing key when its reactor first subscribes. A message published before that bind is dropped.
3. A failed delivery is requeued once, then dropped. There is no dead-letter queue.
4. The inbox row is written after the command returns, not in the same transaction as the command.
5. `GracePeriodConfirmed` is sent straight to the transport. There is no outbox row for it.
6. Four catalog views each read a stored row with their own helper.

## References

- Sample root: `src/dotnet-es-eshop/`
- Event model: `docs/models/eshop/`
- LawnDart packages: [nuget.org/packages/LawnDart](https://www.nuget.org/packages/LawnDart) at `0.5.0-alpha.1`
- Upstream shape: [dotnet/eShop](https://github.com/dotnet/eShop)
- WebApp screen rule: `.cursor/rules/eshop-webapp.mdc`
