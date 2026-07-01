# Atrium.Services.Catalog

## What it is
The **core** backend service for the product catalog. It owns `catalogdb` and exposes products and categories over a JWT-secured HTTP API. It makes no cross-service calls.

## Role in the topology
**Core service.** Sits behind the gateway on `/catalog`. Callers reach it only through YARP; it validates the Keycloak JWT (shared `atrium` audience) and requires the `admin` policy for writes. Owns its data — no cross-database joins.

## Key types
- `Catalog/CatalogEndpoints` — `MapCatalogEndpoints` on the `/catalog` route group (`.WithTags("Catalog").RequireAuthorization()`).
- `Catalog/CatalogRepository` — Dapper calls into `usp_Product_*` / `usp_Category_GetList` sprocs.
- `Catalog/CatalogMapper` (Mapperly), `Catalog/ProductRow`.
- `Data/DatabaseInitializer` — DbUp migrations + programmability at startup; `Program.cs` — DI/auth/DB wiring.

## Run / test
Not run standalone; it comes up (with its DB) via `cd src/Atrium.AppHost && aspire run`. Integration tests: `tests/Atrium.IntegrationTests/CatalogRepositoryTests.cs` (against a real SQL Server via `SqlServerFixture`).

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Two service shapes" and "Data."
- [ADR-0002](../../docs/adr/0002-dapper-sprocs-dbup.md) · [ADR-0007](../../docs/adr/0007-feature-folders-and-repository-testing.md) · [ADR-0003](../../docs/adr/0003-yarp-keycloak-auth.md) · [ADR-0009](../../docs/adr/0009-service-root-route-nesting.md).
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md).
