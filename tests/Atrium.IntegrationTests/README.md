# Atrium.IntegrationTests

## What it is
Integration tests that exercise the service repositories against a **real SQL Server**, including the DbUp-provisioned schema and stored procedures. Proves the Dapper + sprocs data layer end to end.

## Role in the topology
**Tests (integration).** Spins up a SQL Server via `SqlServerFixture` and runs the Catalog and Storefront repositories against it — the data path the services use at runtime.

## Key types
- `SqlServerFixture` — provisions the database (DbUp migrations + programmability) for the test run.
- `CatalogRepositoryTests` — Catalog `usp_Product_*` / `usp_Category_*` paths.
- `OrderRepositoryTests` — Storefront `usp_Order_*` paths.

## Run / test
```
dotnet test tests/Atrium.IntegrationTests
```
Requires a reachable SQL Server (Docker / the Aspire-provisioned instance); slower than the unit suite.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Data."
- [ADR-0002](../../docs/adr/0002-dapper-sprocs-dbup.md) — Dapper + sprocs + DbUp.
- [ADR-0007](../../docs/adr/0007-feature-folders-and-repository-testing.md) — repository integration testing.
