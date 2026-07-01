# Atrium.AppHost

## What it is
The single-file .NET Aspire AppHost that orchestrates the whole system for local development: Keycloak, SQL Server, the two databases, the services, the gateway, and the Portal — wired together with service discovery and injected secrets.

## Role in the topology
**AppHost.** The composition root for `aspire run`. It references the runnable projects and declares their dependencies, connection strings, and the Portal's Keycloak client secret (`Keycloak__PortalSecret`).

## Key types
- `apphost.cs` — the single-file Aspire model: resources, references, and wiring.

## Run / test
This *is* the run entry point for everything:

```
cd src/Atrium.AppHost && aspire run
```

Then open the Aspire dashboard URL it prints. No dedicated test project.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Topology" and "Auth model."
- [docs/HANDOFF.md](../../docs/HANDOFF.md) — how to run and known limitations.
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md) — registering a new service in the AppHost.
