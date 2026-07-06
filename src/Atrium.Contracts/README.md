# Atrium.Contracts

## What it is
The DTO-only wire contracts that cross the HTTP boundary between the backend services (producers) and the UI modules and their typed clients (consumers). Records only — no behavior.

## Role in the topology
**Contracts.** Referenced by both sides of every call: `Atrium.Services.*` shape these DTOs on the way out, and `Atrium.Modules.*` clients deserialize them on the way in. A breaking change fails the build on both sides — that is the point.

## Key types
- Products: `ProductDto`, `CreateProductRequest`, `UpdateProductRequest`, `CategoryDto`.
- Orders: `CreateOrderRequest`, `OrderItemRequest`, `OrderDto`, `OrderLineDto`.
- Reports: `SalesReportDto`, `CategorySalesDto`.
- Chat: `FeedbackDto` — thumbs feedback payload shared by `FeedbackClient` (Design) and the Storefront feedback endpoint.

All are `sealed record` types.

## Run / test
Not run; compiled into services and modules and exercised whenever the app runs via `cd src/Atrium.AppHost && aspire run`. No dedicated test project — round-trips are covered indirectly by the integration tests.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Data" and request flow.
- [ADR-0006](../../docs/adr/0006-shared-contracts-then-nuget.md) — shared contracts project now, versioned NuGet later.
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md) — where contracts fit in a new vertical.
