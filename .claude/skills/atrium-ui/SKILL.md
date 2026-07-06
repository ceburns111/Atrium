---
name: atrium-ui
description: >-
  Use whenever building or editing ANY Atrium UI — the Blazor Server portal shell, a UI module
  (Storefront / Admin / Reports), or the Atrium.Design RCL itself. Enforces visual consistency and
  reuse: use MudBlazor components and the shared AtriumTheme instead of writing ad-hoc CSS,
  hard-coding colors/spacing, or duplicating components per module. Trigger this even when the
  request is just "add a page", "style this", "build the cart view", "make a table", or any
  Razor/component/CSS work in this repo — consistency erodes silently when each screen reinvents
  the basics.
---

# Atrium UI — consistency & reuse

Atrium is a modular host: many UI modules live behind one shell and must look like **one product**.
Consistency comes from **MudBlazor** (the component library) and **`AtriumTheme`** (the shared
`MudTheme` at `src/Atrium.Design/AtriumTheme.cs`), not from each module getting it "close enough."
This skill is the mechanics of staying consistent and lean. For aesthetic *direction* (palette mood,
typographic feel, what makes it not look templated), defer to the **frontend-design** skill — this
skill assumes the direction is set and keeps every screen faithful to it.

Migration decision: [ADR-0014](../../../docs/adr/0014-adopt-mudblazor.md).

## Reuse before you write

Before adding any markup or CSS, check whether MudBlazor already covers it:

1. **Need a component** (button, card, table, input, badge, page header, dialog, snackbar)? Use the
   MudBlazor component. Check the MudBlazor docs first; don't write markup for something the library
   provides.
2. **Need a value** (a color, spacing, elevation, radius)? Use `AtriumTheme` palette properties
   or MudBlazor's built-in theme-derived values — never a hard-coded hex or pixel literal.
3. **Genuinely new and visually distinct** (something MudBlazor has no equivalent for)? Add it to
   `Atrium.Design` so every module benefits — not to the module. `ProductThumb` is the reference:
   a domain-specific placeholder image with no MudBlazor equivalent, living in `Atrium.Design`.
4. **Truly module-local** (a layout only this page needs)? Keep it in the module, built from
   MudBlazor primitives and `AtriumTheme` values — not raw hex or pixel literals.

The test: if two modules would each write the same thing, it belongs in `Atrium.Design`.

## Rules that keep it coherent

- **MudBlazor first, custom markup last.** The primary reach is a MudBlazor component. Raw HTML or
  scoped CSS is the escape hatch when MudBlazor genuinely has no equivalent — not the first move.
- **`AtriumTheme` as the palette.** Brand colors, typography, spacing scale, elevation, and radius
  live in `AtriumTheme` (`src/Atrium.Design/AtriumTheme.cs`). A hard-coded hex or pixel value is a
  consistency leak and a review flag. Use `Color.Primary`, `Color.Success`, etc. — the theme fills
  in the actual value for the active light/dark mode.
- **Layout with `MudGrid` / `MudStack`.** Use `MudGrid` for page-level structure (toolbars, card
  grids) and `MudStack` for inline alignment. Predictable spacing via the theme's spacing scale, no
  magic-number nudges.
- **Every interactive element earns its states.** MudBlazor handles hover, focus, active, and
  disabled — verify that the component is wired to the correct `Disabled`, `Color`, and `Variant`
  props so keyboard and mouse states are both visible and accessible.
- **Smallest clean implementation.** This is a focused demo, not a framework. Prefer the simplest
  MudBlazor component that reads well over a configurable custom abstraction. Don't build wrappers
  for things Mud already handles. Avoid premature generalization as firmly as duplication — they're
  the two failure modes.

## After UI work

- Look at it. Use the **Playwright MCP** to screenshot the running screen and check spacing rhythm,
  alignment, focus states, and a narrow viewport before calling it done — don't ship UI unseen.
- A quick `code-simplifier` / `/simplify` pass catches duplication and over-abstraction that crept in.

## MudBlazor substitutes for the retired Atrium.Design primitives

The hand-rolled primitives are retired ([ADR-0014](../../../docs/adr/0014-adopt-mudblazor.md)).
Use the MudBlazor equivalents. `ProductThumb` stays in `Atrium.Design` (no MudBlazor equivalent).

| Retired primitive | MudBlazor replacement |
|---|---|
| `Button` (Primary/Accent/Secondary/Ghost) | `MudButton` — `Color.Primary` / `Color.Secondary`; `Variant.Filled` / `Variant.Outlined` / `Variant.Text` |
| `Badge` (Neutral/Success/Warning/Danger) | `MudChip` or `MudBadge` with `Color.*` |
| `Field` (label + hint/error form wrapper) | `MudTextField`, `MudSelect`, etc. — each has `Label`, `HelperText`, `Error`/`ErrorText` built in |
| `Notice` (whole-region message card) | `MudAlert` — `Severity.Info` / `.Success` / `.Warning` / `.Error` |
| `Dialog` (modal with focus trap) | `MudDialog` / `IDialogService` — equivalent a11y guarantees |
| `Menu` | `MudMenu` / `MudMenuList` |
| `PageHeader` (eyebrow / title / description / actions) | `MudText`, `MudBreadcrumbs`, `MudToolBar` combination |
| `ToastHost` / `ToastService` | `MudSnackbarProvider` / `ISnackbar` |
| `ThemeToggle` | Custom toggle setting `MudThemeProvider.IsDarkMode` wired to `AtriumTheme` |

**Kept in `Atrium.Design`:** `ProductThumb` (deterministic placeholder image with an `ImageUrl` param
as the one-spot seam for real photos); `AccessTokenHolder`; `HttpClientExtensions`; `Money`;
`SessionExpiredException` — these are auth/utility C#, not visual.

## Theming

`AtriumTheme` (`src/Atrium.Design/AtriumTheme.cs`) is the single source of truth for brand identity.
It exposes a `MudTheme` with `PaletteLight` and `PaletteDark` blocks carrying the teal brand accent,
neutral ramp, status colors (success/warning/error), Space Grotesk (headings) / Inter (body) typography, and the 8px
spacing scale — ported from the retired `tokens.css` value-for-value (ADR-0014).

The shell's `MudThemeProvider` is initialized with `AtriumTheme.Theme` and a reactive `IsDarkMode`
flag. Modules never set colors directly — they name `Color.Primary`, `Color.Success`, etc. and the
theme resolves the actual value for the active light/dark mode. **Add new brand values to
`AtriumTheme`, not to component CSS.**
