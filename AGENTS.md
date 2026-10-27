# Agent instructions

## Commits

The git author of a cloud agent commit is `Cursor Agent <cursoragent@cursor.com>`.
Every commit message must include a trailer that matches that author:

Signed-off-by: Cursor Agent <cursoragent@cursor.com>

`git commit -s` signs off as the configured user, which is not that author.
Add the author line explicitly. An extra sign-off for the connected GitHub user can stay in the message.

Subject: imperative mood, 72 characters or less, no trailing period.
The body says why. Leave a blank line before `Signed-off-by`.

## Scope

This repository holds community samples that **consume** LawnDart NuGet packages.
It does not publish the LawnDart library. Adapter packages live in LawnDart.Contrib.

InMemory and SQL Server are the stores demonstrated here.
The eShop service hosts, including the Identity login store, can also run on Postgres, picked with the `EShop:Database` setting. The AppHost stays on SQL Server.
InMemory is the LawnDart product transport used in tests and Identity. Hosts with an Aspire `eventbus` reference use `LawnDart.Messaging.RabbitMQ`.
Do not claim Redis, Azure Service Bus, or Wolverine. Do not claim a store or transport this sample does not run.

Prefer one folder per vertical slice under `src/dotnet-es-eshop/`.
Shared code is a thin kernel: events and tiny interfaces only.
An aggregate used by more than one slice lives in `<Service>.API/Aggregates/<AggregateName>/`. See `docs/AGENT_SLICE_PLAYBOOK.md` (Shared aggregates).
Slices add files. They do not edit `Program.cs` or shared aggregate core files. See `docs/AGENT_SLICE_PLAYBOOK.md` (Host wiring, Shared aggregates).
Do not add cross-slice application services.

Maintenance and command slices are API only. Do not add HTML pages or admin forms in a service. Admin UI is a later WebApp admin area. View slices keep `GET /views/{slice-name}` (`docs/AGENT_SLICE_PLAYBOOK.md`, `.cursor/rules/eshop-webapp.mdc`).

XML doc comments on public types and members a slice adds follow `docs/AGENT_SLICE_PLAYBOOK.md` (XML documentation).

## Verify before you finish

When a solution exists, run sample tests:

```bash
dotnet test
```

Default unit tests should not require Docker.
Do not commit `bin/` or `obj/`.

## Docs and skills

`.cursor/rules/docs-voice.mdc` applies when editing `docs/`, `README.md`, or sample READMEs.
`.cursor/rules/eshop-webapp.mdc` applies when editing the eShop WebApp or a slice that touches a storefront screen.
`.cursor/skills/BUILD_KIT.md` is the entry point for consumer skills.
Skills teach LawnDart APIs. Product docs (`docs/DI_GRAMMAR.md`, Library samples, and related paths) live in sellistixorg/LawnDart at the git tag that matches the LawnDart package version you consume. They are not in this repo.

## Secrets and models

Never commit `.cursor/mcp.json` (it holds secrets).
Event model exports belong under `docs/models/` when available.

Infrastructure before slices: see `docs/ESHOP_INFRA_PLAN.md`.

