# Atrium.UnitTests

## What it is
Fast, in-process unit tests for pure logic — no database, no network. Covers domain rules and client-side behavior that can be exercised in isolation.

## Role in the topology
**Tests (unit).** References the projects under test directly and runs their logic without spinning up the app or its dependencies.

## Key types
- `OrderPricingTests` — Storefront `OrderPricing` math.
- `SalesReportBuilderTests` — Storefront `SalesReportBuilder` category bucketing.
- `CartServiceTests` — Storefront module `CartService` state.
- `SessionExpiredTests` — the 401 → `SessionExpiredException` mapping.

## Run / test
```
dotnet test tests/Atrium.UnitTests
```
No external services required — safe to run anytime.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md).
- [ADR-0007](../../docs/adr/0007-feature-folders-and-repository-testing.md) — testing strategy.
- [ADR-0008](../../docs/adr/0008-graceful-session-expiry-handling.md) — session-expiry handling.
