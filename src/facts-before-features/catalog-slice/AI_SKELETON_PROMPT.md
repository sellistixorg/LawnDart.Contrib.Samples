# Skeleton prompt (Catalog slice)

Use with `SLICE.md` attached as context. Paste into your AI coding tool when regenerating this sample.

---

Generate a .NET 10 vertical slice for Harbor Street Books **Catalog** only.

Constraints:

1. Obey `SLICE.md` exactly — no new commands or events.
2. Layout must be:

```text
Domain/Commands, Domain/Events, Domain/Title.cs, Domain/TitleState.cs, Domain/CatalogEventTypes.cs
Hosting/CatalogComposition.cs
Program.cs (thin)
*.Tests with GWT for Add Title to Catalog
```

3. Use LawnDart via `..\..\LawnDart.refs.props` (nuget.org, 0.7.0-alpha.3).
4. Events: JSON sealed records + `[EventTypeName("HarborStreet.…")]`. Host: `AddLawnDart` + `UseInMemory` + `WithEventTypes`. Tests: `BddTestContext.CreateInMemory`.
5. No cart, checkout, payment, or shipping code.
6. Hosting must not contain business rules.

Exit when `dotnet test` on the test project passes.
