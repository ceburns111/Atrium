# MudBlazor Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Atrium's custom `Atrium.Design` UI primitives with idiomatic MudBlazor across the Portal shell and all three modules, preserving brand identity via a custom `MudTheme`.

**Architecture:** Add `MudBlazor` to the shared `Atrium.Design` RCL so it flows transitively to the Portal and every module. Convert the shell to `MudLayout`/`MudAppBar`/`MudDrawer`, port the `tokens.css` palette + fonts into a single shared `AtriumTheme` (`MudTheme`), then convert each module page to MudBlazor components. Delete the custom primitives, CSS, and JS once nothing references them. Auth infra (`AccessTokenHolder`, `HttpClientExtensions`, `Money`, `Enums`, `SessionExpiredException`) is pure C# and stays untouched.

**Tech Stack:** .NET 10, Blazor Server (InteractiveServer), MudBlazor 8.x, xUnit v3 (MTP), Playwright (verification), Aspire (run).

## Global Constraints

- Gate must be clean after every task: `dotnet csharpier format .` → `dotnet build Atrium.slnx -v q` at **0 warnings / 0 errors** → `dotnet test tests/Atrium.UnitTests`. CSharpier runs in check mode during build, so format first.
- Render mode stays **InteractiveServer**. Do not change it.
- **Do not touch** the token flow: `MainLayout` copies `access_token` into the scoped `AccessTokenHolder` in `OnParametersSetAsync`; typed clients send via `SendForJsonAsync` which calls `ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`. No factory-registered `DelegatingHandler` for the bearer (ADR-0004, ADR-0008).
- **Do not touch** backend (`Atrium.Services.*`), `Atrium.Contracts`, or `Atrium.Gateway`. UI only.
- Module discovery via `IModule` / `ModuleCatalog` is unchanged; nav is still contributed by modules, never hard-coded.
- Preserve the **session-ended gating** shipped in commit `21b0ee5`: `AccessTokenHolder.SessionEnded` + `Changed` event, set from `SessionErrorBoundary`, must still flip the account control to "Sign in" in lock-step with the expiry banner.
- Commit per task. Branch: `run/mudblazor-refactor`.
- Keep it idiomatic and clean (interview demo) — assemble stock MudBlazor components; do not build clever abstractions over them.

---

### Task 1: Governance — ADR-0014 + skill updates

**Files:**
- Create: `docs/adr/0014-adopt-mudblazor.md`
- Modify: `docs/adr/0010-native-dialog-primitive.md` (mark superseded)
- Modify: `docs/adr/README.md` (add 0014 to the index)
- Modify: skill `atrium-ui` (SKILL.md) — replace "tokens + Atrium.Design primitives / never add a UI library" guidance with "use MudBlazor components + the shared `AtriumTheme`"
- Modify: skill `atrium-module` (SKILL.md) — update any "Atrium.Design primitives" reference to MudBlazor; keep the typed-client / `ThrowIfSessionExpired` guidance verbatim
- Check: `AGENTS.md`, `docs/ARCHITECTURE.md`, per-project READMEs — update the one-line `Atrium.Design` description ("tokens + primitives") to note MudBlazor + `AtriumTheme`

- [ ] **Step 1: Write ADR-0014** following the repo's ADR format (Status: Accepted; Context: MudBlazor was a required element omitted from the original build; Decision: adopt MudBlazor as the UI component library, port brand into a custom `MudTheme`; Consequences: supersedes ADR-0010, retires `atrium.css`/`tokens.css`/custom primitives, adds a dependency). Read an existing ADR (e.g. `0013`) first to match the exact heading structure.

- [ ] **Step 2: Mark ADR-0010 superseded** — change its Status line to `Superseded by ADR-0014` with a one-line pointer; do not delete its body.

- [ ] **Step 3: Update the ADR README index** to list 0014.

- [ ] **Step 4: Update the `atrium-ui` and `atrium-module` skills** and the doc/README `Atrium.Design` descriptions so the guardrails point at MudBlazor. Grep for `Atrium.Design` primitive names and "tokens.css" across `docs/` and `*/README.md` to catch stragglers: `grep -rn "primitives\|tokens.css\|atrium.css" docs AGENTS.md`.

- [ ] **Step 5: Gate + commit** (docs only, build unaffected but run it anyway).

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q
git add docs AGENTS.md .claude
git commit -m "docs: ADR-0014 adopt MudBlazor; supersede ADR-0010; update UI skills"
```

---

### Task 2: Add MudBlazor package + wiring

**Files:**
- Modify: `src/Atrium.Design/Atrium.Design.csproj` (add `MudBlazor` PackageReference)
- Modify: `src/Atrium.Portal/Program.cs` (`AddMudServices()`)
- Modify: `src/Atrium.Portal/Components/App.razor` (swap CSS/JS includes)
- Modify: `src/Atrium.Design/_Imports.razor` and each `_Imports.razor` (Portal + 3 modules): add `@using MudBlazor`

**Interfaces:**
- Produces: MudBlazor components + services available project-wide; `ISnackbar`, `IDialogService` injectable.

- [ ] **Step 1: Add the package to `Atrium.Design`.**

```bash
dotnet add src/Atrium.Design package MudBlazor
```

Confirm it resolves to 8.x. Because Portal and all modules reference `Atrium.Design`, the package flows transitively — do not add it to each project.

- [ ] **Step 2: Register services** in `src/Atrium.Portal/Program.cs`, near the other `builder.Services` calls:

```csharp
using MudBlazor.Services;
// ...
builder.Services.AddMudServices();
```

- [ ] **Step 3: Swap head includes in `App.razor`.** Replace the two `Atrium.Design` stylesheet links (lines 28–29) with the MudBlazor stylesheet; keep the Google-font links (the theme uses them) and keep the pre-Blazor theme script (it seeds initial dark mode). Add the MudBlazor JS before `</body>`, after the blazor script line.

Replace:
```html
<link rel="stylesheet" href="_content/Atrium.Design/css/tokens.css" />
<link rel="stylesheet" href="_content/Atrium.Design/css/atrium.css" />
```
with:
```html
<link rel="stylesheet" href="_content/MudBlazor/MudBlazor.min.css" />
```
and in `<body>` after the blazor script:
```html
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```
Keep `<link rel="stylesheet" href="@Assets["Atrium.Portal.styles.css"]" />` (scoped-CSS bundle).

- [ ] **Step 4: Add `@using MudBlazor`** to `src/Atrium.Design/_Imports.razor`, `src/Atrium.Portal/Components/_Imports.razor`, and the `_Imports.razor` in each of Storefront/Admin/Reports.

- [ ] **Step 5: Gate.** Build will still reference the old primitives (fine — they're not deleted yet). Expect 0/0.

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q && dotnet test tests/Atrium.UnitTests
```

- [ ] **Step 6: Commit.**

```bash
git add -A && git commit -m "build(ui): add MudBlazor to Atrium.Design and wire providers/includes"
```

---

### Task 3: AtriumTheme (brand palette + typography)

**Files:**
- Create: `src/Atrium.Design/AtriumTheme.cs`
- Reference: `src/Atrium.Design/wwwroot/css/tokens.css` (source of truth for the palette — read it, do not guess hex values)

**Interfaces:**
- Produces: `public static class AtriumTheme { public static readonly MudTheme Instance; }` consumed by the `MudThemeProvider` in Task 4.

- [ ] **Step 1: Read `tokens.css`** and pull the exact values: brand accent (teal), neutral ramp (paper/surface/ink/muted/faint/line), status colors (success/warning/danger), radius, and the light + dark theme blocks.

- [ ] **Step 2: Write `AtriumTheme.cs`** — a static `MudTheme` with `PaletteLight` and `PaletteDark` mapped from those values:
  - `Primary` = teal accent; `Surface`/`Background`/`AppbarBackground`/`DrawerBackground` from the neutral ramp; `TextPrimary`/`TextSecondary` from ink/muted; `LinesDefault`/`LinesInputs` from line; `Success`/`Warning`/`Error` from the status colors. Set both light and dark blocks from the two theme definitions in `tokens.css`.
  - `Typography`: `Default`/`Body1`/`Body2` → Inter; `H1`–`H6`/`Subtitle*` → Space Grotesk; a mono role → JetBrains Mono. Map the existing type scale (xs–xl) to font sizes.
  - `LayoutProperties.DefaultBorderRadius` from the tokens' radius.

  Use `PaletteLight`/`PaletteDark` (MudBlazor 8 names), not the deprecated `Palette`. Colors are strings ("#rrggbb").

- [ ] **Step 3: Gate + commit** (not wired yet; just must compile).

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q
git add -A && git commit -m "feat(ui): AtriumTheme MudTheme ported from tokens.css palette + fonts"
```

---

### Task 4: Shell conversion (MudLayout + providers + nav + account menu)

**Files:**
- Modify: `src/Atrium.Portal/Components/Layout/MainLayout.razor`
- Modify: `src/Atrium.Portal/Components/Layout/NavMenu.razor`
- Modify: `src/Atrium.Portal/Components/Layout/UserMenu.razor`
- Delete: `src/Atrium.Design/ThemeToggle.razor` (+ its scoped css if any) — replaced by dark-mode binding
- Modify: any page/service injecting `ToastService` → `ISnackbar` (grep first); delete `ToastService.cs` + `ToastHost.razor` once unreferenced
- Reference: `src/Atrium.Portal/Components/Layout/SessionErrorBoundary.razor` (unchanged logic; it already calls `Tokens.EndSession()`)

**Interfaces:**
- Consumes: `AtriumTheme.Instance` (Task 3), `AccessTokenHolder.SessionEnded`/`Changed` (existing).
- Produces: the app shell; `ISnackbar` as the toast mechanism for all modules.

- [ ] **Step 1: Providers.** In `MainLayout.razor`, add at the top level:

```razor
<MudThemeProvider @ref="_themeProvider" @bind-IsDarkMode="_isDarkMode" Theme="AtriumTheme.Instance" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />
```

- [ ] **Step 2: Layout.** Replace the `.app-shell` div tree with `MudLayout` → `MudAppBar` (breadcrumb/section text via `MudText`, a spacer, the dark-mode `MudIconButton`, and `<UserMenu />`) + `MudDrawer` (`Open="_navOpen"`, responsive) containing `<NavMenu />` + `MudMainContent` wrapping the `SessionErrorBoundary` around `@Body`. Keep `OnParametersSetAsync` (token capture), `OnLocationChanged` (nav close + `_errorBoundary.Recover()`), `Section`, and `IDisposable` exactly as they are. Drop the old `#blazor-error-ui` styling reliance (MudBlazor doesn't need it; keep the element — Blazor still targets `#blazor-error-ui`).

- [ ] **Step 3: Dark-mode toggle.** Replace `<ThemeToggle />` with a `MudIconButton` bound to `_isDarkMode`. Seed `_isDarkMode` from the system preference on first render via `_themeProvider.GetSystemPreferenceAsync()` in `OnAfterRenderAsync(firstRender)`. (Persisting to `localStorage` like the old toggle is optional polish; the pre-Blazor script already prevents a flash.)

- [ ] **Step 4: NavMenu → MudNavMenu.** Convert the brand block to a `MudText`/`MudIcon` header (keep the custom skylight SVG — it's brand art), the links to `MudNavLink` (preserve `Match="NavLinkMatch.All"` on Home and the per-item role-gating `AuthorizeView` exactly), and the footer module-count text to `MudText`. Keep the `ModuleCatalog` injection and `_visibleCount` logic verbatim.

- [ ] **Step 5: UserMenu → MudMenu, preserving session gating.** Keep `@implements IDisposable`, the `AccessTokenHolder` injection, `OnInitialized`/`Dispose` subscribing to `Tokens.Changed`, and the `@if (Tokens.SessionEnded) { SignIn } else { menu }` gate. Convert the avatar trigger to a `MudMenu` with an `MudAvatar` activator showing `Initial(...)`, items `Signed in`/name + a `Sign out` link to `/account/logout`. The `NotAuthorized` branch and the shared `SignIn` prompt become a `MudButton`/link to `/account/login`.

- [ ] **Step 6: Toasts → Snackbar.** `grep -rn "ToastService" src` — for each injector, replace `ToastService.Show(msg, variant)` with `Snackbar.Add(msg, Severity.Normal|Success|Error)` (`@inject ISnackbar Snackbar`). Once no references remain, delete `ToastService.cs` and `ToastHost.razor` and remove the `ToastService` DI registration.

- [ ] **Step 7: Gate + commit.**

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q && dotnet test tests/Atrium.UnitTests
git add -A && git commit -m "feat(ui): convert Portal shell to MudBlazor (layout, nav, account menu, snackbar)"
```

---

### Task 5: Storefront — Shop page

**Files:**
- Modify: `src/Atrium.Modules.Storefront/Pages/Shop.razor`

**Interfaces:**
- Consumes: the module's typed client (unchanged) — do not alter data loading or token handling.

- [ ] **Step 1: Read the current page.** Note every `Atrium.Design` primitive used (ProductThumb, Badge, Field, Button, Menu) and the data/loading/error states.
- [ ] **Step 2: Convert markup only.** Product grid → `MudGrid` of `MudCard` (each with `ProductThumb` kept as the card media, name, price, an add-to-cart `MudButton`); category filter → `MudSelect`/`MudChipSet`; loading → `MudProgressCircular`/`MudSkeleton`; error/empty (`Notice`) → `MudAlert`. Keep all `@code` (data fetch via typed client, cart calls, `Snackbar.Add` feedback) intact.
- [ ] **Step 3: Gate + commit.**

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q
git add -A && git commit -m "feat(storefront): convert Shop page to MudBlazor"
```

---

### Task 6: Storefront — Cart + Checkout pages

**Files:**
- Modify: `src/Atrium.Modules.Storefront/Pages/CartPage.razor`
- Modify: `src/Atrium.Modules.Storefront/Pages/Checkout.razor`

- [ ] **Step 1: Read both pages.** Note primitives (Button, Notice, Field) and the checkout form fields + validation + submit flow.
- [ ] **Step 2: Convert CartPage.** Cart line-item table → `MudTable`/`MudSimpleTable` (thumb, name, qty stepper via `MudNumericField`, line total, remove `MudIconButton`); totals in a `MudPaper`; empty state (`Notice`) → `MudAlert`; checkout CTA → `MudButton`. Preserve all `@code`.
- [ ] **Step 3: Convert Checkout.** Order summary in `MudPaper`; payment form → `MudForm` + `MudTextField`s (keep existing field names, hints, and validation); place-order → `MudButton` with the existing submit handler; success/failure feedback via `Snackbar` or `MudAlert`. Preserve the typed-client order call and `ThrowIfSessionExpired` path (it lives in the client, untouched).
- [ ] **Step 4: Gate + commit.**

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q
git add -A && git commit -m "feat(storefront): convert Cart and Checkout pages to MudBlazor"
```

---

### Task 7: Storefront — Orders page

**Files:**
- Modify: `src/Atrium.Modules.Storefront/Pages/OrdersPage.razor`

- [ ] **Step 1: Read the page** (uses Notice, PageHeader, Badge).
- [ ] **Step 2: Convert.** Page header → `MudText`; order history → `MudTable` or `MudExpansionPanels` (one panel per order, line items inside); status `Badge` → `MudChip` with severity color; empty/error → `MudAlert`. Preserve `@code`.
- [ ] **Step 3: Gate + commit.**

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q
git add -A && git commit -m "feat(storefront): convert Orders page to MudBlazor"
```

---

### Task 8: Admin — Products page (DataGrid + dialog form)

**Files:**
- Modify: `src/Atrium.Modules.Admin/Pages/Products.razor`

- [ ] **Step 1: Read the page.** It uses Dialog, Button, Field, Badge, PageHeader, Notice, and a create/edit flow driven by the custom `Dialog`.
- [ ] **Step 2: Convert.** Product list → `MudDataGrid<ProductDto>` (columns for name/price/etc., an edit `MudIconButton` per row, a "New product" `MudButton` in the toolbar). Create/edit → `MudDialog` opened via `IDialogService` (or an inline `MudDialog` bound to a `_dialogOpen` flag) containing a `MudForm` + `MudTextField`s mirroring the current fields and validation. On save/delete, keep the existing typed-client calls and refresh, and surface result via `Snackbar`. **Fallback:** if `MudDataGrid` complicates the edit flow, use `MudTable` instead — note the choice in the commit message.
- [ ] **Step 3: Gate + commit.**

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q
git add -A && git commit -m "feat(admin): convert Products to MudDataGrid + MudDialog form"
```

---

### Task 9: Reports — Dashboard (stat cards + chart)

**Files:**
- Modify: `src/Atrium.Modules.Reports/Pages/Dashboard.razor`

- [ ] **Step 1: Read the page** (PageHeader, Badge, Notice, custom bar chart in CSS).
- [ ] **Step 2: Convert.** Header → `MudText`; stat cards → `MudPaper`/`MudCard` in a `MudGrid`; the bar chart → `MudChart ChartType="ChartType.Bar"` fed from the existing report data (map series/labels from the current model); error/empty → `MudAlert`. Preserve the typed-client report fetch (admin-gated) and `@code`.
- [ ] **Step 3: Gate + commit.**

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q
git add -A && git commit -m "feat(reports): convert Dashboard to MudBlazor stat cards + MudChart"
```

---

### Task 10: Delete dead primitives, CSS, and JS

**Files:**
- Delete under `src/Atrium.Design/`: `Button.razor`, `Field.razor`, `Dialog.razor`(+`.razor.css`+`dialog.js`), `Menu.razor`, `Badge.razor`, `Notice.razor`(+`.razor.css`), `PageHeader.razor`, `ToastHost.razor` (if not already in Task 4), `wwwroot/css/tokens.css`, `wwwroot/css/atrium.css`, `wwwroot/js/theme.js`
- **Keep:** `ProductThumb.razor` (still used), `AccessTokenHolder.cs`, `HttpClientExtensions.cs`, `Money.cs`, `Enums.cs`, `SessionExpiredException.cs`

- [ ] **Step 1: Prove they're unreferenced.** For each primitive name, `grep -rn "<Button\|<Field\|<Dialog\|<Menu\|<Badge\|<Notice\|<PageHeader\|ThemeToggle\|ToastHost" src`. Expect **no** hits outside the files being deleted. If any hit remains, that page wasn't fully converted — fix it before deleting.
- [ ] **Step 2: Delete** the files above. Also grep `tokens.css`/`atrium.css`/`theme.js`/`dialog.js` references and confirm none remain (App.razor was updated in Task 2).
- [ ] **Step 3: Gate.** Full test run, not just unit — `dotnet test Atrium.slnx` (Docker for integration lane).

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q && dotnet test Atrium.slnx
```

- [ ] **Step 4: Commit.**

```bash
git add -A && git commit -m "chore(ui): remove custom design primitives, tokens.css, atrium.css after MudBlazor migration"
```

---

### Task 11: Verification — Playwright screenshot pass

**Files:**
- None committed (screenshots saved to the session scratchpad for human review)

- [ ] **Step 1: Run the system.** `cd src/Atrium.AppHost && aspire run`. Wait for the Portal to come up; note the full click-ready Portal URL from the Aspire dashboard.
- [ ] **Step 2: Log in** through Keycloak with a seeded user (and an admin user for Admin/Reports).
- [ ] **Step 3: Screenshot every page in light AND dark:** Home, Shop, Cart (with an item), Checkout, Orders, Admin Products (list + open the create/edit dialog), Reports Dashboard. Use the Playwright MCP tools; toggle dark mode via the topbar button between passes.
- [ ] **Step 4: Self-check each screenshot** — page renders, no blank/overlapping/broken layout, brand palette present (teal primary, correct fonts), dark mode actually dark. Note any regressions.
- [ ] **Step 5: Tear down** cleanly (kill the `runfile/apphost` binary and clear `~/.aspire/cli/bch/*` per the teardown gotcha, else the next `aspire run` hangs).
- [ ] **Step 6: Report** to the human: summary of pages verified, screenshots location, any issues found. Do not open a PR unattended — leave the branch for review.

---

## Self-Review

**Spec coverage:** governance (Task 1) · package+wiring (Task 2) · theme (Task 3) · shell (Task 4) · 8 module pages (Tasks 5–9) · delete primitives/CSS (Task 10) · Playwright verification (Task 11). All spec sections mapped.

**Preservation constraints checked:** token flow untouched (stated in Global Constraints + Tasks 4/6); `SessionEnded` gating preserved (Task 4 Step 5); `ProductThumb` kept (Tasks 5, 10); auth/util C# kept (Task 10).

**Known adaptation:** Razor markup isn't unit-tested here, so per-task verification is build-green + commit, with the Playwright pass (Task 11) as the visual regression net — consistent with the spec.
