# dotnet-es-eshop

Event-sourced rebuild of [dotnet/eShop](https://github.com/dotnet/eShop) on [LawnDart](https://www.nuget.org/packages/LawnDart), .NET 10, and .NET Aspire.

| Pin | Version |
|---|---|
| LawnDart family | 0.6.0-alpha.1 |
| Aspire (AppHost SDK and hosting) | 13.6.1 |
| .NET SDK | 10.0.401 (`global.json`, roll forward to the latest feature band) |

## Prerequisites

- .NET 10 SDK.
- A container runtime (Docker or Podman). The AppHost starts SQL Server (`mcr.microsoft.com/mssql/server:2025-latest`), Redis, and RabbitMQ.
- Aspire 13.6.1. `dotnet run --project AppHost` uses the AppHost SDK pin. `aspire run` needs an Aspire CLI on that same version.

## Run

From this folder:

```bash
dotnet run --project AppHost
```

The Aspire dashboard prints the WebApp URL.

## RabbitMQ management

The AppHost calls `WithManagementPlugin()` on `eventbus`. On the Aspire dashboard, open the `eventbus` resource and follow the `management` endpoint. That page is the RabbitMQ management UI.

Sign in as `guest`. Aspire does not take a user name for this resource, so the user stays `guest`. The password is the generated parameter `eventbus-password`. The dashboard shows that parameter on the `eventbus` resource. The same password is in the `eventbus` connection string.

A message that fails all 5 attempts is dead-lettered. The exchange is `{event-type}.dlx`. The queue is `{event-type}.{subscription}.dlq`. In the UI, open Queues and find that name. `order-paid` for Catalog is `order-paid.catalog.dlq`.

Seeded shoppers:

| User | Password |
|---|---|
| `alice` | `Pass123$` |
| `bob` | `Pass123$` |

The seed also copies the eShop demo card (masked number, security code 123 for alice and 456 for bob). Payments are simulated. Checkout still posts that stand-in card.

## Services

```
AppHost/              Aspire host: SQL Server, Redis, RabbitMQ, services
ServiceDefaults/      OpenTelemetry, health, service discovery, resilience
Infrastructure/       LawnDart SQL host kit and RabbitMQ transport registration
Shared/               Events shared across services
Identity.API/         Shopper accounts (ASP.NET Identity) and sign-in tokens
Catalog.API/          Items, pictures, stock
Basket.API/           Shopper basket. Views are SQL. Redis view store is deferred
Ordering.API/         Orders
OrderProcessor/       Grace period after an order is placed
PaymentProcessor/     Simulated payment
Webhooks.API/         Scaffold
WebhookClient/        Scaffold
WebApp/               Blazor storefront (interactive server)
tests/                Per-service tests
.slices/index.json    Slice name to folder map
```

LawnDart product stores here are InMemory (tests) and SQL Server. Services that own a SQL database call `AddEShopLawnDart` in `Infrastructure/`. Hosts with an Aspire `eventbus` reference use `LawnDart.Messaging.RabbitMQ`. The AppHost starts Redis. Basket keeps SQL views.

## Slices

One folder per vertical slice, under `<Service>/Slices/<SliceName>/`. An aggregate used by more than one slice lives in `<Service>.API/Aggregates/<AggregateName>/`. The slice adds a partial file next to its command. It does not edit the shared aggregate core or `Program.cs`.

`.slices/index.json` maps each slice name to its folder. View slices expose `GET /views/{slice-name}` on the owning service. The WebApp calls those routes.

The event model is `docs/models/eshop/` (repository `docs/` folder). The JSON files are a read-only export.

## Tests

From this folder:

```bash
dotnet test
```

That runs every project under `tests/`. Default unit tests do not open SQL Server or start the AppHost.

`ESHOP_REQUIRE_RABBITMQ=1` makes these tests fail when RabbitMQ is down:

- `Outbox_publish_reaches_two_service_queues`
- `Competing_consumers_on_one_queue_deliver_once`
- `Reactor_inbox_drops_a_replayed_outbox_message`
- `Failed_handler_dead_letters_after_five_attempts`

`Outbox_publisher_delivers_to_in_memory_subscribers` stays in memory and runs with the rest.

Pull requests to `main` and pushes to `main` run `.github/workflows/eshop.yml`. The job restores, builds, and tests `LawnDart.Samples.EShop.sln` on `ubuntu-latest`. The SDK comes from `global.json`. Restore uses `nuget.config` in this folder, which lists nuget.org only. The job starts a RabbitMQ container and sets `ESHOP_REQUIRE_RABBITMQ=1`. It does not start SQL Server.
