---
name: lawndart-aspnet-hosting
description: Map LawnDart HTTP commands and optional HTTP authorization. Register handlers with WithCommandHandlers, then AddLawnDartHttpCommands and MapLawnDartCommands. Use when exposing commands over HTTP.
---

# ASP.NET hosting

> Paths such as `samples/Library.*`, `docs/*.md`, and `build-kit/*` resolve in sellistixorg/LawnDart at the package-matching tag, not in this Samples repo.

Register handlers with `WithCommandHandlers<TMarker>()`, then
`AddLawnDartHttpCommands` / `MapLawnDartCommands` for routing.
Map GETs with `MapProjectionQueries(name)`.

Excerpt from `samples/Library.Host/LibraryHost.cs` (`AddInMemoryLibrary` + `MapLibraryHttp`):

```csharp
ctx.WithCommandHandlers<BorrowBookHandler>();
services.AddLawnDartHttpCommands(typeof(BorrowBookHandler).Assembly);
services.AddHttpAuthorizationContext();
app.MapLawnDartCommands();
app.MapProjectionQueries("default");
```

`LawnDart.AspNetCore` does not reference `LawnDart.Authorization.AspNetCore`.
Commands without `[RequiresPermission]` run without a claims provider.

For repository checks, set `EnableAuthorization` on `AddLawnDart` and call
`AddLawnDartAuthorization`. Academy WebApi is the runnable auth host.

Routes: strip `Command`, kebab-case, last meaningful namespace segment as
group (`Commands` and `Patterns` are skipped), default prefix `api`.

Status: handler success `202`, failed auth `403`, `ConcurrencyException`
`409`, `DomainException` `422`. See `docs/HTTP_COMMANDS.md`.

## eShop slices

In this sample, maintenance and command slices are API only.
`AddLawnDartHttpCommands` and `MapLawnDartCommands` are the command surface.
Do not add HTML pages or admin forms in a service. Admin UI is a later
WebApp admin area. View slices keep `GET /views/{kebab-case-slice-name}`.
