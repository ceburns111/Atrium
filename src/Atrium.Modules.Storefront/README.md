# Atrium.Modules.Storefront

## What it is
The customer-facing UI module — Shop, Cart, and Orders — rendered inside the Portal shell. Amber accent.

## Role in the topology
**UI module.** A self-contained Razor Class Library the Portal discovers by reflection via `IModule`. Its typed clients call the **gateway** (`/catalog`, `/storefront/orders`), never the services directly. References `Atrium.Abstractions`, `Atrium.Design`, and `Atrium.Contracts`.

## Key types
- `StorefrontModule` — the `IModule` (name, base path, nav items, `RegisterServices`).
- `Catalog/CatalogClient`, `Orders/OrdersClient` — typed HTTP clients that attach the bearer token and call `ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`.
- `Cart/CartService` — in-session cart state.
- Pages: `Shop.razor`, `CartPage.razor`, `OrdersPage.razor`.

## Run / test
Not run standalone; it loads in the Portal via `cd src/Atrium.AppHost && aspire run`. Unit tests: `tests/Atrium.UnitTests/CartServiceTests.cs`.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — module discovery and request flow.
- [ADR-0004](../../docs/adr/0004-token-propagation-and-option-b.md) · [ADR-0008](../../docs/adr/0008-graceful-session-expiry-handling.md).
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md).
