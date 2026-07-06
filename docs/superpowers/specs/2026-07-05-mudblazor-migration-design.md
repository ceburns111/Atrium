# MudBlazor migration — design

**Date:** 2026-07-05
**Status:** Approved (spec); implementation plan to follow
**Executes:** second of two sequential initiatives — **after** the Support-agent retirement
(`2026-07-05-retire-support-agent-design.md`). That removal deletes `AgentChat` + its CSS + JS
first, shrinking this migration's surface.

## Context

`Atrium.Design` is a hand-rolled design system: BEM primitives styled by CSS custom-property tokens
(`tokens.css`, 149 lines) plus a 1,091-line `atrium.css` covering the shell and every page. It served
the build well, but for the demo the pragmatic senior call is a maintained component library:
velocity, accessibility, responsive behavior, and idiomatic-Blazor signal. The pre-demo checklist in
`docs/interview/07-CLARIFICATIONS.md` names this ("Reimplement the UI with MudBlazor"), including its
two sub-questions — what happens to the `Dialog` primitive, and whether JS interop shrinks.

**Decisions (2026-07-05):**
- Full cutover, not piecemeal (piecemeal leaves two styling systems and orphaned utilities).
- Keep the Atrium identity by **mapping the existing tokens into a `MudTheme`** (light + dark
  palettes, typography, radius) rather than shipping stock Material or chasing pixel fidelity.
- The migration is honest in the docs: a superseding ADR records the trade-off; the `atrium-ui`
  skill is rewritten (today it forbids UI libraries — it would fight every edit).

## Package & wiring

- `MudBlazor` NuGet added to **`Atrium.Design.csproj` only** — Portal and all modules already
  reference Design, so components flow transitively from one pin point. Pin the latest stable at
  execution time (8.x line or newer); first task verifies it restores + builds against `net10.0`.
- `src/Atrium.Portal/Program.cs`: `builder.Services.AddMudServices();`
- `src/Atrium.Design/_Imports.razor` (and module `_Imports.razor` as needed): `@using MudBlazor`.
- `App.razor`:
  - Remove `_content/Atrium.Design/css/tokens.css` and `_content/Atrium.Design/css/atrium.css` links.
  - Add `_content/MudBlazor/MudBlazor.min.css` (head) and `_content/MudBlazor/MudBlazor.min.js`
    (before `blazor.web.js`).
  - **Keep** the Google Fonts link (Space Grotesk / Inter / JetBrains Mono) — the theme's typography
    uses them; no Roboto needed.
  - Keep (adapt) the inline no-flash theme script.

## Theming — `AtriumTheme.cs` (new, in `Atrium.Design`)

Single source of truth replacing `tokens.css`: a static `MudTheme` with

- `PaletteLight` / `PaletteDark` mapped from the current token values (paper `#fbfbfa`, surface
  `#ffffff`, ink `#18181b`, teal accent `#117b68` as `Primary`, success/warning/danger + their soft
  fills mapped to the corresponding Mud slots; dark values from the existing `data-theme=dark`
  block). The implementation plan embeds the full value table extracted from `tokens.css`.
- `Typography`: Space Grotesk for headings, Inter for body/buttons, JetBrains Mono for code.
- `LayoutProperties`: border radius, drawer width (current sidebar width), appbar height.

Custom components that keep bespoke CSS consume **Mud's emitted CSS variables**
(`--mud-palette-*`) instead of the old tokens.

**Dark mode:** `MudThemeProvider @bind-IsDarkMode` in `MainLayout`. Persistence keeps the existing
15-line `theme.js` (localStorage + initial-paint script): first interactive render reads the stored
preference (falling back to `GetSystemPreference()`), toggle writes it back. `ThemeToggle` becomes a
`MudIconButton` flipping the bound flag.

## Component & shell mapping

| Today (`Atrium.Design`) | Becomes | Notes |
|---|---|---|
| `Button` + `ButtonVariant` | `MudButton` | Variant map: Primary→Filled/Primary, Accent→Filled/Secondary, Secondary→Outlined, Ghost→Text. Delete `Button.razor`, enum. |
| `Badge` + `BadgeVariant` | `MudChip` (small) | Soft fills approximated by `Variant.Text`/theme colors. |
| `Field` + raw inputs | `MudTextField` / `MudNumericField` / `MudSelect` | Built-in Label/HelperText/Error replace the wrapper. Delete `Field.razor`. |
| `Dialog` (native `<dialog>` + `dialog.js`) | `MudDialog` via `IDialogService` | Kills `dialog.js`. Callers (Admin product edit, any confirms) move to service-based dialogs. |
| `Menu` | `MudMenu` + `MudMenuItem` | UserMenu restructures; `MenuTests` deleted with it. |
| `ToastHost` + `ToastService` (`Toasts.cs`) | `MudSnackbar` (`ISnackbar`) | Delete both; call sites switch to `ISnackbar.Add`. |
| `ThemeToggle` | `MudIconButton` + `IsDarkMode` binding | Keeps `theme.js` persistence (above). |
| Shell: `app-shell` grid, sidebar, topbar, mobile drawer (`atrium.css`) | `MudLayout` + `MudAppBar` + `MudDrawer` + `MudMainContent` + `MudNavMenu`/`MudNavLink` | Responsive drawer for free; brand SVG mark stays in the drawer header. Nav items still come from `IModule.NavItems` with role gates. |
| Data tables (Admin products, Orders) | `MudTable<T>` | Deliberately **not** `MudDataGrid` — simpler reads cleaner for the demo. |
| Reports stats + CSS bars | `MudPaper` stat cards + `MudProgressLinear` | `MudChart` bar chart is an optional stretch item, not a gate. |
| Module/home cards, page scaffolding | `MudCard`/`MudPaper` + `MudGrid` + Mud spacing utilities | Replaces the corresponding `atrium.css` sections. |
| Icons (inline feather SVGs) | `Icons.Material.*` | Brand mark SVG excepted. |

**Stays custom** (restyled to sit on Mud):
- `ProductThumb` — deterministic SVG art, no Mud equivalent; palette references switch to
  `--mud-palette-*`.
- `Notice` — centered full-page state card (session expired / error / empty); rebuilt thin on
  `MudPaper` + Mud typography.
- `PageHeader` — thin layout component on Mud typography.
- `SessionErrorBoundary` — logic unchanged; its rendered content uses `Notice`/`MudButton`.
- `ReconnectModal` — Blazor framework reconnect UI; untouched.

**Deleted when the last consumer migrates:** `atrium.css`, `tokens.css`, `dialog.js`,
`Button.razor`, `Badge.razor`, `Field.razor`, `Menu.razor`, `Dialog.razor` (+ `.razor.css`),
`ToastHost.razor`, `Toasts.cs`, `ThemeToggle.razor` (replaced), `Enums.cs`.

**Untouched:** `AccessTokenHolder`, `HttpClientExtensions`, `SessionExpiredException`, `Money`,
all typed clients, all backend/service code, gateway, auth flow.

**JS interop after migration** (answers the checklist question): `cart-storage.js` +
`theme.js` (15 lines) + the framework's `ReconnectModal.razor.js`. `dialog.js` gone; `agentchat.js`
already gone with the agent slice.

## Migration order (each step leaves the app green)

1. **Foundation:** package, wiring, `AtriumTheme.cs`, providers in `MainLayout` — old CSS still
   loaded; app renders unchanged.
2. **Shell:** `MudLayout`/`MudAppBar`/`MudDrawer`/nav/UserMenu/ThemeToggle.
3. **Storefront module:** Shop, Cart, Checkout, Orders.
4. **Admin module:** product table + edit dialog (first `IDialogService` use).
5. **Reports module** + Portal home cards.
6. **Teardown:** delete superseded components/CSS/JS, final sweep of stray classes.
7. **Docs & skills:** ADR-0014, `atrium-ui` skill rewrite, CLAUDE.md/ARCHITECTURE.md/AGENTS.md.

## Documentation & story consistency

- **New `docs/adr/0014-adopt-mudblazor.md`** — supersedes the hand-rolled BEM/token decision
  honestly: what the owned system bought during the build, why a maintained library is the call now
  (velocity, a11y, responsive primitives, hiring-market idiom), what was kept (theme = the identity;
  custom `ProductThumb`/`Notice`; the token palette lives on inside `AtriumTheme`).
- **`atrium-ui` skill** (`.claude/skills/atrium-ui/`) rewritten: enforce `AtriumTheme` + Mud
  components + Mud utility classes; forbid ad-hoc CSS and hand-rolled primitives; keep the
  "no per-module styling drift" spirit.
- **`CLAUDE.md` / `docs/ARCHITECTURE.md` / `AGENTS.md`**: `Atrium.Design` is now "MudBlazor theme +
  a few custom components + shared HTTP utilities"; BEM/token references updated; ADR range bumped.
- `docs/interview/07-CLARIFICATIONS.md`: tick the MudBlazor pre-demo item; the §03 BEM answer gets a
  pointer to ADR-0014 (the "real fork" was taken deliberately).

## Validation

Same run mechanics as the agent-retirement plan (runbook discipline, atomic commits, orchestrator
re-runs gates, revert-to-green protocol).

### Lane A — deterministic gate (per commit; authoritative)
```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q   # 0/0
dotnet test Atrium.slnx
```
Strengthened for this run: **bUnit render-smoke tests per migrated page** (render under
`AddMudServices` + `MudPopoverProvider`/providers with `JSInterop.Mode = Loose`; assert a
load-bearing element per page). These are the overnight backbone — they run in the gate with no
browser. `MenuTests` is deleted with `Menu`; `SessionExpiredTests` untouched.

### Lane B — live Playwright validation (per milestone 2–6 + final sweep)
Boot: `cd src/Atrium.AppHost && aspire run` (background); health-wait on
http://localhost:5260/health and http://localhost:5109/health. Portal at https://localhost:7001
(fallback http://localhost:5035); Keycloak at http://localhost:8080. Users: `testuser` / `password`
(customer), `admin` / `password` (admin).

Per milestone: drive the migrated pages, assert **functional** outcomes only —
- shop → filter → add to cart → cart qty edit → checkout → order confirmation → orders list;
- admin: create, edit (MudDialog), delete a product;
- reports: stats render with data;
- role gating: `testuser` sees no Admin/Reports nav;
- zero browser console errors per page.

Theme/responsive sweep (final): toggle dark mode and screenshot **every page light + dark** at
desktop and at 390×844 (drawer behavior). Screenshots → gitignored `artifacts/` folder, referenced
from the run LOG.

**Failure protocol:** functional assertion fails → 2 attempts → revert-to-green, mark BLOCKED, move
on (circuit-breaker at 2 consecutive). Visual/subjective doubt (spacing, contrast, "looks off") →
do **not** block: mark the item `[~]` best-effort with the screenshot flagged for morning review.
Unattended runs never declare visual polish "done".

## Out of scope
- `MudDataGrid`, virtualization, or any component upgrades beyond parity with today's UI.
- Restyling `ReconnectModal`; changing auth/token flow; touching services or gateway.
- `MudChart` for Reports (optional stretch only, never a gate).
