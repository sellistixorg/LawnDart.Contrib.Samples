# HarborStreet.Shell

Trivial smoke project for the sample solution.

## Objective

Prove `src/samples` can restore and build against LawnDart 0.7.0-alpha.3 from nuget.org.

## Run

```powershell
cd src/facts-before-features
dotnet restore HarborStreet.Samples.slnx
dotnet build HarborStreet.Samples.slnx
dotnet run --project smoke-check/HarborStreet.Shell/HarborStreet.Shell.csproj
```

## GWT

N/A — no domain behavior.

## What AI must not invent

Domain events or Harbor Street types. This project is wiring only.
