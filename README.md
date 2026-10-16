# LawnDart.Contrib.Samples

Samples that use [LawnDart](https://www.nuget.org/packages/LawnDart). This repository is MIT licensed. See `LICENSE`.

The sample under `src/dotnet-es-eshop/` is an event-sourced rebuild of [dotnet/eShop](https://github.com/dotnet/eShop) on LawnDart, .NET 10, and .NET Aspire. LawnDart packages are pinned to `0.6.0-alpha.1`.

## Prerequisites

- .NET 10 SDK. Run from `src/dotnet-es-eshop` so `global.json` applies. It pins `10.0.401` and rolls forward to the latest feature band.
- A container runtime (Docker or Podman) for SQL Server, Redis, and RabbitMQ.
- Aspire 13.6.1. Run from `src/dotnet-es-eshop` so `aspire.config.json` applies. `dotnet run --project AppHost` uses the AppHost SDK pin.

## Run the AppHost

From `src/dotnet-es-eshop`:

```bash
cd src/dotnet-es-eshop
dotnet run --project AppHost
```

The AppHost starts SQL Server, Redis, and RabbitMQ, then the services. The Aspire dashboard prints the WebApp URL.

Seeded shoppers are `alice` and `bob`. The password is `Pass123$`. Those users, and the masked demo card, come from the eShop seed. Payments are simulated. Checkout still posts that stand-in card.

## Services

| Project | Role |
|---|---|
| `AppHost` | Aspire host for SQL Server, Redis, RabbitMQ, and the services |
| `Identity.API` | Shopper accounts on ASP.NET Identity. Sign-in returns an access token |
| `Catalog.API` | Catalog items, pictures, and stock |
| `Basket.API` | Shopper basket. Views are SQL |
| `Ordering.API` | Orders |
| `OrderProcessor` | Grace period after an order is placed |
| `PaymentProcessor` | Simulated payment |
| `WebApp` | Blazor storefront |
| `Webhooks.API`, `WebhookClient` | Scaffolds |
| `Infrastructure` | LawnDart SQL host kit and RabbitMQ transport registration |
| `Shared` | Events shared across services |

LawnDart product stores in this sample are InMemory (tests) and SQL Server. Hosts with an Aspire `eventbus` reference use `LawnDart.Messaging.RabbitMQ`. The AppHost starts Redis. Basket keeps SQL views.

## Slices

Each service keeps one folder per vertical slice under `Slices/`. An aggregate used by more than one slice lives in `<Service>.API/Aggregates/<AggregateName>/`. `src/dotnet-es-eshop/.slices/index.json` maps slice names to folders. A view is `GET /views/{slice-name}` on the owning service.

The event model is the JSON under `docs/models/eshop/`. Those files are a read-only export. The plan next to them is `Dotnet_LawnDart_eShop_Model_Plan.md`.

Hosting detail is in `docs/ESHOP_INFRA_PLAN.md`. How to run tests, and a closer look at the services, is in `src/dotnet-es-eshop/README.md`.

## Tests

```bash
dotnet test src/dotnet-es-eshop/LawnDart.Samples.EShop.sln
```

Default unit tests do not need Docker. With `ESHOP_REQUIRE_RABBITMQ=1`, the broker tests connect to RabbitMQ and fail when it is down. `.github/workflows/eshop.yml` starts RabbitMQ for that run.

## Caveats

This sample is a demo. Payments are simulated. `OrderPlaced` on the bus carries the ship-to address, the cardholder name, the last four digits, and the expiry. The Payment Attempt Status view is anonymous. Shopper tokens last one hour and have no refresh. `POST /identity/demo-token` is still mapped. It returns a stand-in token and does not authenticate anyone.

## LawnDart

Install the packages from [nuget.org](https://www.nuget.org/packages/LawnDart). Product source is [sellistixorg/LawnDart](https://github.com/sellistixorg/LawnDart), at the tag that matches `0.6.0-alpha.1`.

Storefront files copied from dotnet/eShop are listed in `src/dotnet-es-eshop/WebApp/THIRD-PARTY-NOTICES.md`. Catalog seed pictures are covered by `src/dotnet-es-eshop/Catalog.API/Seed/NOTICE`.
