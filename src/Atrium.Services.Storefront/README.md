# Atrium.Services.Storefront

## What it is
The **app-vertical** backend service for orders, sales reports, and order-support chat. It owns `storefrontdb` **and** composes the Catalog core service over HTTP to price orders and label report data.

## Role in the topology
**App vertical.** Sits behind the gateway on `/storefront`. Owns its own data; for product prices/categories it calls Catalog over HTTP, **relaying the caller's bearer token** (via `IHttpContextAccessor`). JWT-secured with the shared `atrium` audience. `Atrium.ServiceDefaults` supplies telemetry, JWT wiring, API docs, and database init.

## Key types
- `Orders/OrdersEndpoints` (`/orders`), `Reports/ReportsEndpoints` (`/reports`) — route groups under the service root.
- `Orders/OrderRepository`, `Reports/ReportRepository` — Dapper over `usp_Order_*` / `usp_Report_*` sprocs.
- `Orders/OrderPricing`, `Reports/SalesReportBuilder` — pure domain logic (pricing, category bucketing).
- `Catalog/StorefrontCatalogClient` — bearer-relay client to Catalog.
- **`Support/`** — order-support AI agent slice:
  - `SupportAgent` — Microsoft Agent Framework `ChatClientAgent` over the configured `IChatClient` (Fake / Ollama / FoundryLocal / AzureFoundry), with `GetOrderStatus` and `FindProduct` tools.
  - `GuardrailChatClient` — safety-filter decorator; rejects out-of-scope requests before they reach the model.
  - `SupportTelemetry` — OTel GenAI span names/meters; traces export to the Aspire dashboard.
  - `FeedbackEndpoints` (`/storefront/agent/feedback`) — stores thumbs feedback as OTel spans + structured logs (no DB).
  - `StepUpMfa` — optional step-up MFA requirement for the `/storefront/agent` endpoint.
  - `SupportEndpoints` — maps the AG-UI SSE endpoint at `/storefront/agent`.
- `Program.cs` — DI/auth/DB wiring (uses `AddAtriumJwtAuth`, `AddAtriumTelemetry`, `DatabaseInitializer.Initialize`).

## Run / test
Not run standalone; it comes up (with its DB) via `cd src/Atrium.AppHost && aspire run`. Unit tests: `OrderPricingTests`, `SalesReportBuilderTests`, `Support/` folder (`tests/Atrium.UnitTests`). Integration: `OrderRepositoryTests` (`tests/Atrium.IntegrationTests`). LLM evals: `tests/Atrium.Evals` (requires Ollama).

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Two service shapes," "Data," bearer relay.
- [ADR-0005](../../docs/adr/0005-slice-calls-core.md) · [ADR-0002](../../docs/adr/0002-dapper-sprocs-dbup.md) · [ADR-0007](../../docs/adr/0007-feature-folders-and-repository-testing.md) · [ADR-0009](../../docs/adr/0009-service-root-route-nesting.md) · [ADR-0012](../../docs/adr/0012-shared-deployment-infrastructure.md).
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md).
