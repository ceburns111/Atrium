# Atrium.Services.Storefront

## What it is
The **app-vertical** backend service for orders and sales reports. It owns `storefrontdb` **and** composes the Catalog core service over HTTP to price orders and label report data.

## Role in the topology
**App vertical.** Sits behind the gateway on `/storefront`. Owns its own data, but for product prices/categories it calls Catalog over HTTP, **relaying the caller's bearer token** (via `IHttpContextAccessor`) rather than issuing its own. JWT-secured with the shared `atrium` audience.

## Key types
- `Orders/OrdersEndpoints` (`/orders`), `Reports/ReportsEndpoints` (`/reports`) — route groups under the service root.
- `Orders/OrderRepository`, `Reports/ReportRepository` — Dapper over `usp_Order_*` / `usp_Report_*` sprocs.
- `Orders/OrderPricing`, `Reports/SalesReportBuilder` — pure domain logic (pricing, category bucketing).
- `Catalog/StorefrontCatalogClient` — bearer-relay client to Catalog.
- `Data/DatabaseInitializer` (DbUp); `Program.cs` — DI/auth/DB wiring.

## Run / test
Not run standalone; it comes up (with its DB) via `cd src/Atrium.AppHost && aspire run`. Unit tests: `OrderPricingTests`, `SalesReportBuilderTests` (`tests/Atrium.UnitTests`). Integration: `OrderRepositoryTests` (`tests/Atrium.IntegrationTests`).

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Two service shapes," "Data," bearer relay.
- [ADR-0005](../../docs/adr/0005-slice-calls-core.md) · [ADR-0002](../../docs/adr/0002-dapper-sprocs-dbup.md) · [ADR-0007](../../docs/adr/0007-feature-folders-and-repository-testing.md) · [ADR-0009](../../docs/adr/0009-service-root-route-nesting.md).
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md).
