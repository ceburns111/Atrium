# ADR-0009 — Nest a service's routes under one service-root group; features map relative subtrees

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** 7 (post-polish)

## Context

Each Storefront feature declared its own **absolute** route prefix: `OrdersEndpoints` mapped a group at
`"/storefront/orders"` and `ReportsEndpoints` mapped `"/storefront/reports"`. That re-typed the
`/storefront` service prefix in every feature file (and would again for a third), and it meant the route
structure **didn't mirror the feature folders** ([ADR-0007](0007-feature-folders-and-repository-testing.md)) —
each feature independently re-stated where the service lived. Shared concerns like
`RequireAuthorization()` were also repeated per feature.

## Decision

State the **service boundary once** as a parent group in `Program.cs`, and have each feature map its
**relative** subtree onto it. In `Atrium.Services.Storefront/Program.cs`:

```csharp
var storefront = app.MapGroup("/storefront").RequireAuthorization();
storefront.MapOrderEndpoints();   // owns "/orders"
storefront.MapReportEndpoints();  // owns "/reports"
```

- Each feature's endpoint extension now takes the parent group and maps a **relative** child:
  `MapOrderEndpoints(this IEndpointRouteBuilder storefront)` does `storefront.MapGroup("/orders")`,
  `MapReportEndpoints` does `storefront.MapGroup("/reports")`. The full paths (`/storefront/orders`,
  `/storefront/reports`) resolve identically — this is a pure move.
- **Shared auth lifts to the parent.** `RequireAuthorization()` is stated once on the `/storefront`
  group instead of per feature.
- **Per-feature OpenAPI stays per feature.** Each child group keeps its own `WithTags("Orders")` /
  `WithTags("Reports")`.
- **One rule, both services:** service-root group, features as relative subgroups. `Atrium.Services.Catalog`
  is the **single-feature degenerate case** — its one `/catalog` group (in `CatalogEndpoints`) already
  *is* the service root, so there's no separate parent to factor out and the commit left it unchanged.

## Consequences

- **Routes mirror the folders.** The URL tree (Storefront › Orders, Storefront › Reports) now matches
  the feature-folder tree, reinforcing the vertical-slice grain of ADR-0007.
- **The service prefix is stated once.** Adding a third Storefront feature is one `storefront.MapXEndpoints()`
  line plus a relative child group — the `/storefront` literal and the shared `RequireAuthorization()`
  aren't re-typed.
- **Behaviour-preserving.** Routes resolve to identical paths; the Portal's typed clients are unchanged;
  build stayed at 0 warnings.

## Alternatives rejected

- **Keep absolute prefixes per feature.** Duplicates the service prefix and the auth call, and lets the
  route structure drift from the folder structure.
- **A single flat `Program.cs` route table.** Pulls per-feature routing back out of the feature folders,
  undoing ADR-0007's slice organization.

**Diagrams:** [checkout-flow.md](../diagrams/checkout-flow.md) — the `/storefront/checkout` and `/storefront/orders` routes in the end-to-end flow.
