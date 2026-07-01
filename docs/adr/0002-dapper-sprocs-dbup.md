# ADR-0002 — Dapper + stored procedures + DbUp + Mapperly (not EF Core)

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** 4

## Context

Every service needs data access, schema management, and row→DTO mapping. EF Core is the .NET default
and would be a reasonable choice. But this codebase deliberately demonstrates the **explicit-SQL**
stack that a lot of teams run in production: hand-written stored procedures, a migration runner they
control, and no ORM change-tracker in the hot path. The goal is to show that stack done cleanly, not
to relitigate ORM-vs-not.

## Decision

Per service, the data recipe is:

- **Stored procedures** own all SQL. Reads and writes go through sprocs
  (`usp_Product_GetList/Create/Update`, `usp_Order_Create`, `usp_Report_SalesByProduct`, …); write
  sprocs `SELECT` the affected row back so the app gets the persisted state in one round trip.
- **Dapper** executes them — thin, fast, no tracking.
- **DbUp** manages schema in **two lanes**:
  - `Data/Scripts/Migrations/*` — run **once**, in order (schema + seed). DbUp records them in a
    journal table.
  - `Data/Scripts/Programmability/*` — run **always**, written as `CREATE OR ALTER` (the sprocs).
    Re-running is idempotent, so procedure changes ship without a new migration.
- **Mapperly** generates the row→DTO mapping at **compile time** (source generator) — no reflection,
  no AutoMapper runtime cost.
- SQL files are **embedded resources**; `DatabaseInitializer` applies them at service startup so a
  fresh DB is provisioned automatically (Aspire spins up SQL Server in a container with a data volume).

## Consequences

- **The DB schema is the source of truth**, versioned in the repo and diffable in review. No "what did
  the ORM generate?" surprises.
- **Procedure edits are a one-file change** — edit the `CREATE OR ALTER` script, restart, done. This is
  why Admin's create/update landed cleanly: add two run-always sprocs, no migration.
- **More boilerplate than EF.** Each new query is a sproc + a Dapper call + a Mapperly mapping. That's
  the tax for explicitness; accepted.
- **No lazy loading / change tracking / LINQ provider.** Composition across aggregates is explicit
  (e.g. Storefront reports call Catalog for categories rather than joining) — which we wanted anyway,
  since the data lives in separate databases (see [ADR-0005](0005-slice-calls-core.md)).
- **Version pin worth remembering:** DbUp 7.x uses `LogToConsole()` (not `LogToAutodetectedLog()`).

## Alternatives rejected

- **EF Core** — great default, but hides the SQL this project is meant to showcase, and its migration
  model competes with DbUp's two-lane split.
- **Inline SQL strings in C#** — loses the run-always-sproc idempotency and makes review harder.
- **AutoMapper** — runtime reflection where a compile-time source generator (Mapperly) is strictly
  cheaper and catches mapping breaks at build time.
