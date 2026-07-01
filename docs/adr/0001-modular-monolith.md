# ADR-0001 — Modular monolith with reflection-discovered UI modules

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** 1–2

## Context

Atrium's premise is "N apps, one host": several UI areas (Storefront, Admin, Reports) that feel like
independent applications but ship in one portal. The two obvious extremes are a single tangled
Blazor app (fast to start, rots into a big ball of mud) and true micro-frontends (independent deploy,
but heavy — iframes, module federation, an orchestration story we don't need for a demo).

We want the *seam* of separate apps — a module can't reach into another's internals, and adding one is
nearly free — without paying the runtime cost of separate deployables yet.

## Decision

Build a **modular monolith**. Each UI area is a **Razor Class Library** that implements a single
contract, `IModule` (`Atrium.Abstractions`):

```csharp
public interface IModule
{
    string Name { get; }
    string Description { get; }
    string BasePath { get; }          // route prefix the module owns, e.g. "/storefront"
    string? Accent => null;           // brand accent for the shell
    IEnumerable<NavItem> NavItems { get; }
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
```

The host (`Atrium.Portal`) discovers modules by **reflection** (`Modularity/ModuleLoader` scans
`Atrium.Modules.*` assemblies for `IModule`), lets each register its own services, and surfaces its
nav + homepage card. **The host references the module projects but names none of them in code** — it
has no compile-time knowledge of a module's internals.

## Consequences

- **Adding a module = a project reference + one `IModule` class.** No host edits, no central registry
  to update. That is the property we were buying.
- **Enforced boundaries.** Modules share only `Atrium.Abstractions` (the contract) and
  `Atrium.Design` (primitives) by type. Cross-module coupling has nowhere to hide.
- **A clean extraction path.** Because a module already owns its routes, services, and nav, promoting
  one to an independently deployed UI later is a packaging change, not a rewrite — see
  [BEYOND-THE-DEMO.md](../BEYOND-THE-DEMO.md) item 6.
- **Gotcha we hit: routing needs the module assemblies registered in _two_ places** —
  `<Router AdditionalAssemblies>` in `Routes.razor` *and*
  `MapRazorComponents().AddAdditionalAssemblies()` in `Program.cs`. The second is what makes
  deep-links / SSR resolve a module's pages; miss it and links 404 only on refresh.
- **Single process, single deploy.** All modules rise and fall together. Acceptable now; the exit is
  documented, not built.

## Alternatives rejected

- **One flat Blazor app** — no boundary enforcement; the whole reason we're rebuilding CozenDemo.
- **Micro-frontends now** — real independent deploy, but iframes/module-federation overhead with no
  payoff at demo scale. Kept as the heavy option in BEYOND-THE-DEMO.md.
- **MEF / a plugin framework** — more machinery than a one-interface reflection scan needs.
