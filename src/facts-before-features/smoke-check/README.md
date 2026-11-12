# Smoke check

A small project that proves the solution restores LawnDart from nuget.org.

## Objective

Restore and build against LawnDart 0.7.0-alpha.5 from nuget.org.

## Run

```bash
cd src/facts-before-features
dotnet restore HarborStreet.Samples.slnx
dotnet build HarborStreet.Samples.slnx
dotnet run --project smoke-check/HarborStreet.Shell
```

## GWT

No domain behavior.

## What AI must not invent

Domain events or Harbor Street types. This project is wiring only.
