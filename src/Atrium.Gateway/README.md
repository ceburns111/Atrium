# Atrium.Gateway

## What it is
The YARP reverse proxy that is the single ingress in front of the backend services. It matches `/catalog/{**catch-all}` and `/storefront/{**catch-all}` and forwards to the two service clusters.

## Role in the topology
**Gateway.** The Portal's typed clients call the gateway (via `https+http://gateway` Aspire service discovery); the gateway routes to Catalog and Storefront. Routes and clusters are declared in `appsettings.json`.

## Key types
- `Program.cs` — wires YARP (`AddReverseProxy`) with Aspire service discovery.
- `appsettings.json` — the route/cluster map (`/catalog`, `/storefront`).

## Run / test
Not run standalone; it comes up as part of the app via `cd src/Atrium.AppHost && aspire run`. Behavior is exercised end to end when modules call through it; no dedicated test project.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Topology" and "Ingress is the gateway."
- [ADR-0003](../../docs/adr/0003-yarp-keycloak-auth.md) — YARP gateway + Keycloak auth.
