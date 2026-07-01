# Atrium.Modules.Reports

## What it is
The sales-analytics UI module: stat cards and a CSS bar chart of sales by category. Violet accent.

## Role in the topology
**UI module.** A self-contained Razor Class Library the Portal discovers by reflection via `IModule`. Its client calls the **gateway** `/storefront/reports` route (the Storefront app vertical builds the report). References `Atrium.Abstractions`, `Atrium.Design`, and `Atrium.Contracts`.

## Key types
- `ReportsModule` — the `IModule` implementation.
- `ReportsClient` — typed client for the sales report via the gateway; attaches the bearer token and calls `ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`.
- `Pages/Dashboard.razor` — stat cards + bar chart.

## Run / test
Not run standalone; it loads in the Portal via `cd src/Atrium.AppHost && aspire run`. The report shaping it renders is produced server-side and unit-tested in `tests/Atrium.UnitTests/SalesReportBuilderTests.cs`.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Data" (reports compose Catalog).
- [ADR-0005](../../docs/adr/0005-slice-calls-core.md) — app vertical composes core over HTTP.
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md).
