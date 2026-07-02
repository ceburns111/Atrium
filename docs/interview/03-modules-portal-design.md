# Interview study — Modules, portal shell & design system

> My voice, my architecture. Everything below maps to real files in this repo — file refs are inline so I can jump to the code mid-answer if asked. Nothing here is "the AI did that."

---

## The 90-second explanation

Atrium is a **modular monolith**: one Blazor Server host (`Atrium.Portal`) that presents several apps — Storefront, Admin, Reports — each of which *feels* like an independent application but ships in the same process. Each app is a **Razor Class Library** that implements exactly one interface, `IModule` (`src/Atrium.Abstractions/IModule.cs`). That interface is the *only* type the host and a module share by name.

At startup the host **discovers modules by reflection** — `ModuleLoader.Discover()` scans every `Atrium.Modules.*.dll` in the app directory for concrete `IModule` types, instantiates them, and drops them into a `ModuleCatalog` singleton (`src/Atrium.Portal/Modularity/`). That one catalog is the single source of truth for three things: the router's `AdditionalAssemblies`, the nav menu, and the homepage cards. Each module also gets to `RegisterServices(...)` into the host container — its typed HTTP clients, its cart service, whatever — so it's wired exactly like first-party code.

The result is the property I was actually buying: **adding an app = one project reference + one `IModule` class.** No host edits, no central registry. On top of that sits a **tokens-first design system** (`Atrium.Design`): every color/space/type value lives in `tokens.css`, every screen is built from shared primitives (Button, Card, Badge, Dialog…), and dark mode is just a redefinition of the color tokens. So all three apps look like one product even though nothing forces them to.

The whole thing deliberately mirrors the target architecture minus the AI pieces — the seam of separate apps without the runtime cost of separate deployables.

---

## How it actually works

### 1. The contract: `IModule` is the entire shared surface

`src/Atrium.Abstractions/IModule.cs` is tiny and it's the whole API between host and module:

```csharp
public interface IModule
{
    string Name { get; }                       // homepage card + nav label
    string Description { get; }                 // homepage card blurb
    string BasePath { get; }                    // route prefix it owns, e.g. "/storefront"
    string? Accent => null;                     // brand accent, default = host accent
    string? RequiredRole => null;               // role gate, default = everyone
    IEnumerable<NavItem> NavItems { get; }
    IEnumerable<AgentSurface> AgentSurfaces => []; // chat surfaces, default = none
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
```

`Atrium.Abstractions` holds *only* this, `NavItem`, and `AgentSurface` (`NavItem.cs`, `AgentSurface.cs`) — three types, no behavior. Keeping it minimal is exactly what lets the host reference module projects without naming any of them in code. The host depends on the *shape*, never the implementation.

Notice the **default interface members**: `Accent`, `RequiredRole`, and `AgentSurfaces` all have defaults. That's deliberate — it's how the contract grows without breaking existing modules. `AgentSurfaces` was added when the chatbot slice landed; every pre-existing module kept compiling and just returned "no surfaces" for free. A module that only cares about nav implements four members and ignores the rest.

### 2. Discovery → catalog → one source of truth

`src/Atrium.Portal/Modularity/ModuleLoader.cs`:

- Enumerates `Atrium.Modules.*.dll` in `AppContext.BaseDirectory`. Referenced RCLs land in the app directory at build, so a single project reference is all it takes to be discovered.
- For each assembly, resolves it into the default load context (reuses the already-loaded instance if present, else `Assembly.Load`), then reflects for `{ IsClass: true, IsAbstract: false }` types assignable to `IModule` and `Activator.CreateInstance`s them.
- Returns a `ModuleCatalog(modules, assemblies)`.

`ModuleCatalog.cs` is an immutable pair: `IReadOnlyList<IModule> Modules` and `IReadOnlyList<Assembly> Assemblies`. It's registered as a **singleton** in `Program.cs` so the shell (nav, home) and the router draw from the *same* discovered set — no double-scanning, no drift.

`src/Atrium.Portal/Program.cs` (lines ~96–102, 153–157) ties it together:

```csharp
var moduleCatalog = ModuleLoader.Discover();
foreach (var module in moduleCatalog.Modules)
    module.RegisterServices(builder.Services, builder.Configuration);
builder.Services.AddSingleton(moduleCatalog);
// ...
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies([.. moduleCatalog.Assemblies]);
```

And the router, `src/Atrium.Portal/Components/Routes.razor`:

```razor
@inject Atrium.Portal.Modularity.ModuleCatalog Catalog
<Router AppAssembly="typeof(Program).Assembly" AdditionalAssemblies="Catalog.Assemblies" ...>
```

The nav (`Components/Layout/NavMenu.razor`) and homepage (`Components/Pages/Home.razor`) both `@inject ModuleCatalog` and iterate `Catalog.Modules` — nav renders one link per `NavItem`, home renders one card per module. Nothing about Storefront is hard-coded anywhere in the host.

### 3. A module's anatomy — Storefront end to end

`src/Atrium.Modules.Storefront/StorefrontModule.cs`:

- **Identity:** `Name`/`Description`/`BasePath = "/storefront"`, `Accent => "#b45309"` (amber, distinct from the shell's teal). It does *not* set `RequiredRole`, so it inherits the default `null` → visible to everyone including anonymous.
- **Nav:** `NavItems => [new NavItem("Storefront", "/storefront")]`.
- **Agent surface:** `AgentSurfaces => [new AgentSurface("Support", "storefront/agent", StarterPrompts: [...])]` — a gateway-relative endpoint (no leading slash) the shell's assistant launcher renders.
- **Services:** `RegisterServices` adds `CartService`, `CartPersistence`, `PaymentService`, and two **typed HTTP clients** pointed at the gateway:

```csharp
services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new Uri("https+http://gateway"));
services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new Uri("https+http://gateway"));
```

`https+http://gateway` is a *logical* service-discovery name — the host wires `AddServiceDiscovery()` + a bearer-token handler as `ConfigureHttpClientDefaults` (`Program.cs` ~30–32), so every module client resolves the gateway and attaches the signed-in user's token **without the module wiring any of that itself**.

- **The clients** (`Catalog/CatalogClient.cs`, `Orders/OrdersClient.cs`) are typed wrappers that speak `Atrium.Contracts` DTOs and follow the house pattern on every call: `request.Authorize(tokens)` → `LogIfUnsuccessful` → **`ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`** so a 401 becomes a graceful re-login rather than a raw 500.
- **Pages** are just `@page` components (`Pages/Shop.razor` → `/storefront`, `Checkout.razor`, `CartPage.razor`, `OrdersPage.razor`). They resolve automatically because the module assembly is in the router's `AdditionalAssemblies`.

Its `.csproj` references only `Atrium.Abstractions`, `Atrium.Design`, and `Atrium.Contracts` — never another module. The boundary is enforced by what it's *allowed* to reference.

### 4. Role-gating, applied identically in three places

The gate is one predicate — "no required role, or the user is in that role" — and it shows up three times, deliberately identical:

- **Nav** (`NavMenu.razor`): per-`NavItem` `RequiredRole` wrapped in `<AuthorizeView Roles="...">`; also computes a "_N_ of _M_ modules visible" footer using `IsVisible(module, user)`.
- **Home cards** (`Home.razor`): each `ModuleCard` for a role-gated module is wrapped in `<AuthorizeView Roles="@module.RequiredRole">`.
- **Assistant launcher** (`Components/Layout/AssistantLauncher.razor`): `ResolveSurface()` only considers modules where `IsVisible(m, _user)` is true — so a role-gated module's agent never leaks to a user without the role. Comment in the file literally says "identical to `NavMenu.IsVisible` / the home-card gate."

Actual values (from `docs/diagrams/module-discovery.md`, verified against code): Storefront `RequiredRole = null`, Admin and Reports `= "admin"`. Net effect: anonymous/`customer` sees Storefront only; `admin` sees all three.

That's **defense in depth, not the only defense** — the visual gate hides things, but a wrong-role user deep-linking to a gated route by full-page GET is denied at the endpoint (403) and routed to the clean `/forbidden` page (`Program.cs` `AccessDeniedPath`; `Routes.razor` `NotAuthorized` → `<Forbidden />`). The UI gate is UX; the endpoint is the real boundary.

### 5. The design system — tokens first, primitives, native dialog

`src/Atrium.Design` is an RCL every module references.

- **`wwwroot/css/tokens.css`** is the single source of truth: font roles, a type scale, the neutral ramp, one brand accent (`--accent: #117b68`), status colors, radius, an 8px spacing rhythm, elevation, motion, layout dims. The header comment says it outright: *"consumers never hard-code values."*
- **`wwwroot/css/atrium.css`** is the base + shell + primitive styles, and *everything* derives from tokens — 300+ `var(--…)` references, near-zero literal values. Header: *"All values derive from tokens.css."*
- **Primitives** (`Components/`): `Button`, `Badge`, `Card`, `Field`, `PageHeader`, `Dialog`, `Notice`, `ProductThumb`, `ThemeToggle`, `ToastHost`, plus `AgentChat`. They're thin — `Button.razor` maps a `ButtonVariant` enum to a BEM class (`btn btn--accent`) and forwards unmatched attributes; `Badge.razor` is the same shape. The styling lives in the shared CSS keyed off BEM class names, so a variant looks identical in every module.
- **Dark mode** is *only* a color-token redefinition under `:root[data-theme="dark"]` — type/spacing/radius/motion are shared. `App.razor` sets `data-theme` from `localStorage` (or `prefers-color-scheme`) via an inline script before first paint, so there's no flash; `ThemeToggle` handles runtime changes. There's also a `@media (prefers-color-scheme: dark)` no-JS fallback.
- **`Dialog`** is built on the **native `<dialog>` element** opened with `showModal()` (per ADR-0010): focus trap, Esc-to-close, top-layer stacking, `::backdrop`, and return-focus come from the *browser*, not hand-rolled JS. The interop is a ~10-line ES module (`wwwroot/js/dialog.js`) exposing just `showModal`/`close`, each guarded so Blazor re-renders can't double-open. `OnAfterRenderAsync` syncs imperative calls to the declarative two-way `Open` param; `@onclose` relays Esc back through `OpenChanged`. Backdrop clicks intentionally do **not** dismiss (would discard in-progress edits).

The assistant launcher reusing `Dialog` + `AgentChat` is the payoff: one primitive, every modal across every module reuses it instead of reinventing an overlay.

---

## Why it's built this way

**Modular monolith vs the extremes (ADR-0001).** The two obvious poles are (a) one flat Blazor app — fast to start, rots into a big ball of mud, no boundary enforcement, which is literally why I'm rebuilding CozenDemo — and (b) true micro-frontends — real independent deploy, but iframes/module-federation/orchestration overhead with no payoff at demo scale. I wanted the *seam* of separate apps (a module can't reach into another's internals, adding one is nearly free) **without** paying for separate deployables yet. The modular monolith buys exactly that, and because a module already owns its routes, services, and nav, promoting one to an independent deployable later is a packaging change, not a rewrite. The exit is documented, not built.

**Reflection discovery vs explicit registration.** I could have a static `services.AddModule<StorefrontModule>()` list in `Program.cs`. That reintroduces the central registry I'm trying to kill — every new app is a host edit, and it's the exact coupling point that rots. Reflection over a naming convention (`Atrium.Modules.*`) means the host has *zero* compile-time knowledge of any module. I explicitly rejected MEF / a plugin framework too (ADR-0001) — that's more machinery than a one-interface reflection scan needs. The loader's doc comment even sketches the upgrade path: swap the one `Assembly.Load` step for an `AssemblyLoadContext` over a `/modules` folder and modules become runtime-droppable, unchanged.

**Tokens vs per-module CSS.** If each module shipped its own CSS, consistency erodes silently — screen by screen the basics get reinvented and drift. Centralizing every value in `tokens.css` and every control in `Atrium.Design` means visual consistency is the *default*, not a review checklist. It's also what makes dark mode a ~40-line token block instead of a per-component slog, and what makes a new module look right on day one with zero styling work.

**Native `<dialog>` vs hand-rolled overlay (ADR-0010).** A hand-rolled modal invites a pile of a11y work — focus trap, Esc, top-layer, backdrop, return-focus — that's easy to get subtly wrong and expensive to own. The platform does all of it correctly via `showModal()`. I rejected a third-party modal library too: it pulls a dependency for one primitive and cuts against the tokens-and-`Atrium.Design`-only rule.

---

## What's impressive here / talking points

- **"Add an app = one project ref + one `IModule`."** No host edits, no central list. I can demo it: reference the RCL, implement `IModule`, and it shows up as a homepage card, a nav link, routable pages, registered services, and (optionally) an assistant surface — because every one of those reads from the same discovered catalog.
- **The reflection loader is ~30 lines and load-bearing.** Convention-based discovery over `Atrium.Modules.*`, concrete-`IModule` filter, into an immutable singleton catalog that's the *single source of truth* for router + nav + home. One scan, one list, no drift.
- **Boundaries enforced by references, not by discipline.** A module can only reference `Abstractions`, `Design`, `Contracts`. Cross-module coupling has nowhere to hide — it wouldn't compile.
- **Role-gating is the same predicate in three surfaces**, and it's UX layered *over* a real endpoint-level authorization boundary. I can point to `NavMenu.IsVisible`, the `Home.razor` `AuthorizeView`, and `AssistantLauncher.ResolveSurface`'s `IsVisible` filter and show they agree by construction.
- **Design-system token discipline.** 300+ `var(--…)` refs, essentially zero hard-coded colors/spacing; dark mode as a pure token redefinition; primitives that are thin BEM wrappers so a `btn--accent` is identical everywhere.
- **Default interface members let the contract grow without breaking modules** — `AgentSurfaces` was added for the chatbot slice and every existing module kept compiling untouched.
- **Modules extend the shell without the shell knowing them** — Storefront contributes an `AgentSurface` and the launcher renders it generically.

---

## Likely interview questions → strong answers

**Q: How does the shell discover modules?**
Reflection over convention. `ModuleLoader.Discover()` enumerates `Atrium.Modules.*.dll` in the app base directory, loads each, finds concrete types implementing `IModule`, instantiates them, and returns a `ModuleCatalog`. Referenced RCLs land in the output directory at build, so a single project reference is enough to be discovered — the host names no module in code.

**Q: Why reflection instead of registering modules explicitly?**
Explicit registration reintroduces the central registry I'm trying to eliminate — every new app becomes a host edit, which is exactly the coupling that rots. Reflection gives the host zero compile-time knowledge of any module. I rejected MEF/a plugin framework as more machinery than a one-interface scan needs (ADR-0001).

**Q: How do you prevent modules from stepping on each other?**
Three ways. (1) Reference boundaries — a module can only reference `Abstractions`/`Design`/`Contracts`, never another module, so cross-module calls don't compile. (2) Each module owns a `BasePath` route prefix (`/storefront`, `/admin`…) so routes don't collide. (3) DI: each module registers its own services in `RegisterServices`; shared plumbing (service discovery, bearer token) is host-level defaults, so modules don't fight over it.

**Q: How do you keep the UI consistent across independent modules?**
A shared design system, `Atrium.Design`. All design values live in `tokens.css`; all screens build from shared primitives (Button, Card, Badge, Dialog, PageHeader…) whose styles derive entirely from tokens. Modules don't write ad-hoc CSS or pull UI libraries — consistency is the default. Dark mode is a token redefinition, so it applies uniformly for free.

**Q: How does a module make authenticated API calls?**
It registers typed `HttpClient`s pointed at the logical `https+http://gateway`. The host wires service discovery and a bearer-token handler as client defaults, and the shell captures the signed-in user's access token into a scoped `AccessTokenHolder` (`MainLayout` reads it from the principal). The clients call `request.Authorize(tokens)` then `ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`, so a 401 becomes graceful re-login. The module writes none of the discovery/auth plumbing itself.

**Q: How is role-based access enforced?**
`IModule.RequiredRole` and per-`NavItem.RequiredRole`, checked with the same predicate in the nav, the home cards, and the assistant launcher via `<AuthorizeView Roles>` / `IsInRole`. That's the UX layer — it hides what you can't use. The real boundary is the route/endpoint: a wrong-role deep-link is denied (403) and sent to `/forbidden`. Defense in depth.

**Q: Why two AuthorizeView layers — isn't hiding the nav link enough?**
No. Hiding the link is UX only; a user can still type or bookmark the URL. So the gate is repeated at every surface that exposes the module (nav, card, agent) *and* backed by endpoint authorization, which is the part that actually can't be bypassed.

**Q: How would you isolate one module's failure so it can't take down the host?**
Today it's a single process — all modules rise and fall together (accepted tradeoff, ADR-0001). To isolate: (1) wrap module render trees in error boundaries — the shell already uses a `SessionErrorBoundary` around `@Body`, which I'd extend per module surface; (2) fault-isolate the *service* calls with resilience handlers (timeouts, retries, circuit breakers) on the typed clients so a slow gateway degrades one app, not the shell; (3) the real isolation story is extracting a hot module to its own deployable — cheap because it already owns its routes/services/nav.

**Q: How would you load modules dynamically at runtime?**
The loader is already shaped for it. Right now it `Assembly.Load`s real project references. The doc comment calls it out: swap that one step for an `AssemblyLoadContext` over a `/modules` folder, and you can drop a DLL in at runtime — the modules themselves stay unchanged. You'd add contract versioning and a load/unload lifecycle, but the discovery → catalog → nav/router pipeline doesn't change.

**Q: What has to happen for a module's `@page` to resolve on a hard refresh?**
Its assembly must be registered in **two** places: `<Router AdditionalAssemblies>` in `Routes.razor` (the interactive client-side router) *and* `MapRazorComponents().AddAdditionalAssemblies()` in `Program.cs` (server-side endpoint routing for SSR/deep-links). Both read from the same `ModuleCatalog.Assemblies`. Miss the second and links work in-app but 404 on refresh.

**Q: Why native `<dialog>` for the modal?**
It hands you focus trap, Esc-to-close, top-layer stacking, backdrop, and return-focus from the platform — all the a11y that's easy to get subtly wrong by hand. The interop is ~10 guarded lines. I rejected both a hand-rolled overlay (reimplements what the browser already does correctly) and a third-party modal library (a dependency for one primitive, against the tokens-only rule). ADR-0010.

**Q: How does a new module contribute to the shell without the shell knowing about it?**
It returns data from `IModule` members and the shell renders generically. `NavItems` → nav links, the module itself → a home card, `AgentSurfaces` → an assistant launcher button + `Dialog` + `AgentChat`. The shell iterates the catalog; it never references a concrete module type.

---

## Gotchas & things that could trip you up

- **The two-places assembly registration.** This is the one that bit me and it's documented in ADR-0001. Module pages need the assembly in *both* `Routes.razor`'s `<Router AdditionalAssemblies>` *and* `Program.cs`'s `AddAdditionalAssemblies()`. The first drives the interactive router; the second makes static SSR / deep-links / refresh resolve module routes. With only the first, everything works until someone refreshes on a module page and gets a 404. Both pull from `ModuleCatalog.Assemblies`, so they can't silently disagree — but you must wire both.
- **Discovery order isn't guaranteed.** `Directory.EnumerateFiles` returns modules in an unspecified order, so nav/home card order isn't deterministic across environments. Where order matters I sort explicitly — the assistant launcher's `ResolveSurface()` does `OrderBy(m => m.BasePath)` precisely so the off-section fallback surface doesn't depend on discovery order. If I wanted stable nav ordering I'd add an explicit `Order` to `IModule`.
- **A module with no nav / no surface is valid.** `NavItems` can be empty and `AgentSurfaces` defaults to `[]`. Such a module still registers services and routes but contributes no nav link and no launcher button — that's intentional, but it means "I don't see it in the nav" doesn't mean "it didn't load." The nav footer's "_N_ of _M_ modules visible" count helps surface the gap.
- **`AgentSurfaces` allocates a fresh record every call.** It's a computed property, so record value-equality would always differ (the `StarterPrompts` array compares by reference). The launcher compares by `Endpoint` (stable identity) instead of the record, otherwise every navigation forces a needless re-render. Worth knowing before you "simplify" that comparison.
- **`RequiredRole` on the module vs on a `NavItem` are separate gates.** A module can be ungated but expose a gated nav item, or vice-versa. They're checked independently; don't assume one implies the other.
- **Endpoint of an `AgentSurface` has no leading slash; a `NavItem.Path` does.** The surface endpoint is a gateway-relative *service-topology* path (`storefront/agent`) resolved against the gateway base by `AgentChat`; the nav path is an absolute *portal route* (`/storefront/cart`). Mixing them up sends the chat to the wrong base. It's documented in `AgentSurface.cs`.
- **The visual role gate is not the security boundary.** If asked, be crisp: `AuthorizeView` hides UI; endpoint authorization (403 → `/forbidden`) is what actually stops access. Never defend the nav gate as "the security."

---

## If they push deeper / how I'd evolve it

- **Dynamic / hot-loaded modules.** Move discovery to an `AssemblyLoadContext` over a `/modules` drop folder so DLLs load at runtime without a redeploy. The loader is already commented for this — the modules don't change. Adds a load/unload lifecycle and a collectible ALC for true unload.
- **A versioned module contract.** Once modules can be dropped in independently, `IModule` needs a compatibility guarantee — a contract version the host checks on load, so an old host rejects a module built against a newer `Abstractions`. Default interface members already give me additive, non-breaking growth; a version stamp handles the breaking case.
- **Module-level authorization as data, not just role strings.** Today `RequiredRole` is a single string. I'd evolve toward policy names or a capability/permission set the module declares, evaluated against ASP.NET Core authorization policies, so gating isn't limited to flat roles.
- **Publishing modules as NuGet packages.** `Atrium.Abstractions` and `Atrium.Design` become published packages (ADR-0006 already sketches "shared contracts, then NuGet"), so a module can be built in its own repo/CI against a pinned contract version and consumed by the host as a package rather than a project reference. That's the last step before genuine independent-team ownership.
- **Fault isolation before full extraction.** Per-module error boundaries + resilience handlers (timeout/retry/circuit-breaker) on the typed clients, so one flaky downstream degrades one app rather than the shell — buys most of the isolation benefit of microservices without the deployment cost, and it's the natural precursor to extracting a hot module to its own deployable.

---

*Cross-refs: ADR-0001 (modular monolith), ADR-0010 (native dialog), ADR-0004/0008 (token propagation, graceful session expiry), `docs/diagrams/module-discovery.md`.*
