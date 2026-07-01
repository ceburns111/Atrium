# Atrium.Abstractions

## What it is
The tiny contract between the Portal host and its UI modules. It holds the `IModule` interface (and its `NavItem` record) and nothing else — the only types the host and the modules share by name.

## Role in the topology
**Abstractions.** Referenced by `Atrium.Portal` (which discovers modules by reflection) and by every `Atrium.Modules.*` project (which implements `IModule`). Keeping it minimal is what lets the host reference modules without naming any of them.

## Key types
- `IModule` — `Name`, `Description`, `BasePath`, optional `Accent`, `NavItems`, and `RegisterServices(IServiceCollection, IConfiguration)`.
- `NavItem` — a `record` of `Title`, `Path`, optional `Icon` for the shell nav.

## Run / test
Not run on its own; it is compiled into the Portal and every module. Comes up with the app via `cd src/Atrium.AppHost && aspire run`. No dedicated tests — its shape is exercised by the module-discovery path at Portal startup.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Solution layout" and module discovery.
- [ADR-0001](../../docs/adr/0001-modular-monolith.md) — modular monolith with reflection-discovered modules.
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md) — implementing `IModule` for a new app.
