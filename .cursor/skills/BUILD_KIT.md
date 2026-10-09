# LawnDart build kit (samples)

This file is the **only entry point** an agent needs for samples in this repo.
Follow the reading order. Do not invent types that are not on a skill's frozen
surface or in the published LawnDart packages you consume.

This repository is demos. Consumer skills are copied here for agents working on
samples. Authority for types is the LawnDart NuGet packages plus
[sellistixorg/LawnDart](https://github.com/sellistixorg/LawnDart) at the git tag
that matches the package version you consume (for example `0.4`). Product docs
(`docs/DI_GRAMMAR.md`, `samples/Library.*`,
`build-kit/library-slice.json`, MinVer CI) live in that LawnDart checkout, not
in this Samples repo.

Targets LawnDart 0.4 (match the package version you reference).

## Generic slice spec

The kit is written against a **modelling-tool-neutral** slice:

| Concept | Meaning |
|---|---|
| Command | Intent to change something. One handler. |
| Event | Immutable fact already committed. Catalog token is stable. |
| Entity | Consistency boundary that owns decision state (aggregate or DCB). |
| View | Read model folded from events. Not the entity's decision state. |
| Process | Reaction or follow-on work after events (optional). |
| GWT | Given / when / then proof that the slice behaves. |
| Vertical slice | One named bounded context that hosts the above. |

Skills teach how those concepts become LawnDart types. They never name a
modelling tool.

A modelling tool is an **adapter** that maps its export onto this spec:

| Adapter | Input | Where in this repo |
|---|---|---|
| eShop model export | Slice-based JSON event model | `docs/models/eshop/` |

eShop model JSON lives under `docs/models/eshop/`. Do not invent model JSON.

Product adapter docs and frozen Library excerpts live in sellistixorg/LawnDart
at the matching tag (`docs/DI_GRAMMAR.md`, `samples/Library.*`, and so on).

## Reading order

1. This file.
2. The event model for the sample (`docs/models/eshop/` for eShop).
   Skip if the input is already the generic spec.
3. Skills in this repo, in this order:
   1. [`lawndart-host-setup`](lawndart-host-setup/SKILL.md): hub, packages, registration order
   2. [`lawndart-event-store`](lawndart-event-store/SKILL.md): `UseInMemory` / `UseSqlServer`
   3. [`lawndart-bounded-context`](lawndart-bounded-context/SKILL.md): named contexts, handlers
   4. [`lawndart-domain-model`](lawndart-domain-model/SKILL.md): commands, events, aggregates, DCB
   5. [`lawndart-testing`](lawndart-testing/SKILL.md): `AggregateSpec` / `DcbSpec`
   6. [`lawndart-reactions`](lawndart-reactions/SKILL.md): `IReactor` / `AddInMemoryMessaging`
   7. [`lawndart-lightweight-projections`](lawndart-lightweight-projections/SKILL.md): host the read model
   8. [`lawndart-projection-authoring`](lawndart-projection-authoring/SKILL.md): projection classes
   9. [`lawndart-aspnet-hosting`](lawndart-aspnet-hosting/SKILL.md): HTTP commands and auth
4. Product docs in sellistixorg/LawnDart at the matching tag (not in this repo):
   - `docs/DI_GRAMMAR.md`
   - `docs/HTTP_COMMANDS.md`
   - `docs/testing/BDD_TESTING.md`
   - `docs/DCB_PATTERNS.md`
   - `docs/learning-path/06-reactions.md`
5. Canonical Library demo input and reference implementation also live in
   sellistixorg/LawnDart (`build-kit/library-slice.json`, `samples/Library.*`).
   Use those when you need frozen excerpts. Do not invent types.

## Sample layout (this repo)

Prefer under `src/dotnet-es-eshop/`:

```
Shared/       events and tiny shared interfaces only
<SliceName>/  command, handler, read model if needed, tests for that slice
Host/         Program.cs, AddLawnDart, UseInMemory / UseSqlServer
```

An aggregate used by more than one slice lives in `<Service>.API/Aggregates/<AggregateName>/`. See `docs/AGENT_SLICE_PLAYBOOK.md` (Shared aggregates).

## Rules

- Register handlers with `WithCommandHandlers<TMarker>()` on the bounded
  context. `AddLawnDartHttpCommands` only maps routes.
- Aggregates and DCB entities declare closed `Handle(TCommand)`. Persist with
  `HandleCommandAsync`.
- Skill code blocks that cite `samples/Library.*`, `docs/*.md`, or
  `build-kit/*` resolve in sellistixorg/LawnDart at the package-matching tag.
- Consume the LawnDart package version that matches the tag you use for docs.
- Add only published LawnDart packages and the packages this sample already uses.
- Durable backends here: InMemory and SQL Server only.
- eShop slice public types and members need XML doc comments. Follow
  `docs/AGENT_SLICE_PLAYBOOK.md` (XML documentation).

