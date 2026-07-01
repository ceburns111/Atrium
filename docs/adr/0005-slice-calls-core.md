# ADR-0005 — App verticals compose core services over HTTP with a bearer relay

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** 4c

## Context

Storefront needs product data — names and prices — to build orders and to bucket sales by category in
reports. Catalog **owns** that data in its own database (`catalogdb`), separate from Storefront's
(`storefrontdb`). The question is how Storefront gets product data it doesn't own.

The tempting shortcut is a cross-database join or a second connection string into `catalogdb`. That
quietly couples the two services at the schema level and destroys the "each service owns its data"
property that the whole topology rests on.

## Decision

Split services into two shapes and let them compose **over HTTP**, never over the database:

- A **core service** (Catalog) owns a capability's data and exposes it through its API. It makes no
  calls to other services.
- An **app vertical** (Storefront) owns *its own* database **and** calls core services for anything it
  doesn't own. Storefront calls Catalog service-to-service at its discovery address
  (`https+http://catalog`) for the product→price and product→category maps.

Auth across that hop uses a **bearer relay**: Storefront reads the caller's access token from
`IHttpContextAccessor` and forwards it on the outbound call to Catalog. This is valid **because a
normal API request has an `HttpContext`** — unlike a Blazor circuit (contrast
[ADR-0004](0004-token-propagation-and-option-b.md)). The shared `atrium` audience
([ADR-0003](0003-yarp-keycloak-auth.md)) is what lets Catalog accept the relayed token.

## Consequences

- **Data ownership stays intact.** No cross-database coupling; Storefront gets product data the same
  way any client would — over the API, authorized as the same user.
- **One pattern, reused.** The pricing relay and the reports composition (`/storefront/reports/sales`
  fans out to Catalog for categories) are the *same* mechanism, which is why Reports landed cheaply.
- **Cost: an extra network hop and partial-failure surface.** If Catalog is down, Storefront degrades —
  the honest trade for real service isolation. Caching / resilience (Polly) is a production concern,
  not a demo one.
- **Clear extraction seam.** When a second slice needs orders, Orders can graduate from
  "owned by the Storefront vertical" to its own **core service** with no change to this pattern — see
  [BEYOND-THE-DEMO.md](../BEYOND-THE-DEMO.md) item 2.

## Alternatives rejected

- **Cross-database join / shared connection string** — fast, but couples schemas and breaks data
  ownership. The thing we're explicitly avoiding.
- **A shared "products" library linked into both services** — moves the coupling from the DB to the
  binary; same problem, different layer.
- **Duplicating product data into `storefrontdb`** — introduces a sync problem (staleness, write path)
  with no benefit at this scale.
