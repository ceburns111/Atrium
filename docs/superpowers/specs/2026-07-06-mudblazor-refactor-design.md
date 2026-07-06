# MudBlazor Refactor — Design Spec

**Date:** 2026-07-06
**Status:** Approved — ready for implementation planning
**Type:** UI framework refactor (unattended run)

## Goal

Replace Atrium's custom design system with idiomatic **MudBlazor** across the
Portal shell and all three UI modules (Storefront, Admin, Reports). MudBlazor is
a **required element of this interview demo** that was omitted in the original
build; the objective is to demonstrate idiomatic MudBlazor usage, not to reduce
CSS. The current brand identity is preserved by porting it into a custom
`MudTheme`.

### Non-goals

- No new features or pages.
- No backend / `Atrium.Services.*` / `Atrium.Contracts` changes.
- No auth-flow changes (token flow per ADR-0004 / ADR-0008 stays intact).
- No test rewrites beyond keeping the build+test gate green.

## Current surface (measured)

- 33 `.razor` files: 10 `Atrium.Design` primitives + 13 Portal + 10 across modules.
- ~1,350 lines of CSS: `tokens.css` (149) + `atrium.css` (1,090) + scoped (108).
- 3 modules, ~8 module pages total.
- Render mode: **InteractiveServer**.
- Load-bearing non-visual C# in `Atrium.Design` (stays): `AccessTokenHolder`,
  `HttpClientExtensions`, `Toasts`, `Money`, `Enums`, `SessionExpiredException`.

## Design

### 1. Governance (first task of the run)

- Write **ADR-0014** "Adopt MudBlazor as the UI component library," superseding
  **ADR-0010** (native dialog primitive / no external UI library). Record that
  MudBlazor is a project requirement and the tokens-and-Atrium.Design-only
  stance is retired.
- Update the **`atrium-ui`** and **`atrium-module`** skills to reference
  MudBlazor + the shared `MudTheme` instead of "never add a UI library / use
  Atrium.Design primitives." Without this the run fights its own guardrails on
  every page.

### 2. Package & wiring

- Add `MudBlazor` (latest 8.x) to **`Atrium.Design`** so it flows transitively
  to Portal + all modules.
- `builder.Services.AddMudServices()` in `Atrium.Portal/Program.cs`.
- Providers in `MainLayout`: `MudThemeProvider` (with `@bind-IsDarkMode`),
  `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`.
- `App.razor`: replace the two `Atrium.Design` CSS links + Google-font links
  with `_content/MudBlazor/MudBlazor.min.css` and `MudBlazor.min.js`; keep the
  pre-Blazor theme-detection script to seed initial dark mode. Render mode stays
  **InteractiveServer**.
- `_Imports.razor`: add `@using MudBlazor` (Portal + each module).

### 3. Brand → MudTheme

One shared `AtriumTheme.cs` (a static `MudTheme`) in `Atrium.Design`:

- `PaletteLight` / `PaletteDark` mapped from `tokens.css`: teal → `Primary`;
  neutral ramp → `Surface` / `Background` / `TextPrimary` / `TextSecondary` /
  `Lines`; status colors → `Success` / `Warning` / `Error`.
- `Typography`: Space Grotesk (headings) / Inter (body) / JetBrains Mono (mono);
  map the existing type scale.
- Default border radius from the tokens' radius value.

### 4. Shell conversion

`MainLayout` → `MudLayout` + `MudAppBar` (brand, dark-mode toggle, `UserMenu` →
`MudMenu`) + `MudDrawer` + `MudNavMenu`/`MudNavLink` for module-discovered nav.
`SessionErrorBoundary` behavior preserved; `ToastHost` → `MudSnackbar` via
`ISnackbar`. Module discovery via `IModule` is unchanged.

### 5. Primitive → MudBlazor mapping

| Atrium.Design primitive | Replacement |
|---|---|
| Button | `MudButton` / `MudIconButton` |
| Field | `MudTextField` / `MudSelect` (inside `MudForm`) |
| Dialog | `MudDialog` |
| Menu | `MudMenu` |
| Badge | `MudChip` |
| Notice | `MudAlert` |
| PageHeader | `MudText` + layout (small shared razor helper acceptable) |
| ToastHost | `MudSnackbar` (via `ISnackbar`) |
| ThemeToggle | `MudThemeProvider` dark-mode binding |
| ProductThumb | **kept** — generative SVG placeholder, wrapped in `MudCard` |

**Deleted:** the 10 primitive `.razor` files, `atrium.css`, `tokens.css`,
`dialog.js`, `theme.js`, and the scoped `.razor.css` for deleted primitives.
**Kept unchanged in `Atrium.Design`:** `AccessTokenHolder`,
`HttpClientExtensions`, `Toasts`, `Money`, `Enums`, `SessionExpiredException`.

### 6. Module page conversions (8 pages)

- **Storefront:** Shop → `MudGrid` of `MudCard`; CartPage → `MudTable`;
  Checkout → `MudForm` + `MudTextField`; OrdersPage → `MudTable` /
  `MudExpansionPanels`.
- **Admin:** Products → `MudDataGrid` + `MudDialog` create/edit form.
- **Reports:** Dashboard → `MudPaper` stat cards + `MudChart` (bar).

### 7. Verification (unattended safety net)

After the gate is green (`dotnet csharpier format .` → `dotnet build Atrium.slnx
-v q` at 0 warnings/0 errors → `dotnet test Atrium.slnx`), run the system via
`aspire run`, drive it with Playwright, log in, and **screenshot every page**
(Home, Shop, Cart, Checkout, Orders, Products + create/edit dialog, Dashboard) in
both light and dark mode. Self-check each renders (not blank/broken) before
declaring done; save screenshots for final human review.

### 8. Sequencing (keep the build green throughout)

1. Governance (ADR-0014 + skill updates).
2. Package + wiring + `AtriumTheme`.
3. Shell (`MainLayout`, nav, `App.razor`).
4. Module pages, converted one at a time; delete each primitive once it is no
   longer referenced anywhere.
5. Verification.

Commit per logical step so the SAFE-REVERT-POINT discipline holds.

## Risks & mitigations

- **`MudDataGrid` vs `MudTable` for Products** — default to `MudDataGrid`
  (richer, better interview signal); fall back to `MudTable` if it complicates
  the dialog edit flow.
- **Dark-mode init flash** — the pre-Blazor detection script mitigates;
  acceptable for a demo.
- **Tests referencing deleted primitives** — `AccessTokenHolderTests` is pure C#
  and unaffected; any test touching a primitive is updated as that primitive is
  removed.
- **Unattended visual drift** — mitigated by the Playwright screenshot pass;
  final human review still required.
