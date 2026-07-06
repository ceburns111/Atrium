# ADR-0014 — Adopt MudBlazor as the UI component library

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** pre-demo refactor (2026-07-06)

## Context

Atrium's UI was built as a hand-rolled design system: CSS custom-property tokens (`tokens.css`), a
shared stylesheet (`atrium.css`), and ~10 BEM primitives in `Atrium.Design` (`Button`, `Badge`,
`Field`, `Dialog`, `Menu`, `Notice`, `PageHeader`, `ToastHost`, `ThemeToggle`). The rationale
documented in [ADR-0010](0010-native-dialog-primitive.md) was to avoid a third-party dependency and
demonstrate CSS discipline.

MudBlazor was a required element of this interview demo that was omitted from the original build.
Its absence is a gap in the demo story: Atrium is a Blazor Server platform and MudBlazor is the
de-facto component library for Blazor — an interviewer expects to see it, or a deliberate rationale
for not using it. The existing rationale ("demonstrates real CSS skill") is a weaker story than
idiomatic Blazor with a brand-themed component library.

The hand-rolled system's functional concern — brand identity — is fully addressable by porting the
`tokens.css` palette and typography into a custom `MudTheme` (`AtriumTheme`), so no brand value
is lost in the transition.

## Decision

Adopt **MudBlazor** as the UI component library across the Portal shell and all three modules
(Storefront, Admin, Reports).

- **`AtriumTheme`** (`src/Atrium.Design/AtriumTheme.cs`): a shared `MudTheme` that carries the
  brand identity forward from `tokens.css` — teal accent, neutral ramp, status colors,
  Roboto/Roboto Mono typography, 8px spacing scale — in `PaletteLight` and `PaletteDark` blocks.
  This is the single source of truth that replaces `tokens.css`.
- **MudBlazor replaces the custom primitives.** `Button` → `MudButton`; `Badge` → `MudChip` /
  `MudBadge`; `Field` → `MudTextField` / `MudSelect`; `Dialog` → `MudDialog` / `IDialogService`;
  `Menu` → `MudMenu`; `Notice` → `MudAlert`; `PageHeader` → `MudText` / `MudBreadcrumbs` /
  `MudToolBar`; `ToastHost` / `ToastService` → `MudSnackbarProvider` / `ISnackbar`; `ThemeToggle`
  → custom toggle setting `MudThemeProvider.IsDarkMode`. The custom primitives, `atrium.css`,
  `tokens.css`, and their JS interop are retired.
- **Kept unchanged in `Atrium.Design`:** `ProductThumb` (a domain-specific placeholder image with
  no MudBlazor equivalent), `AccessTokenHolder`, `HttpClientExtensions`, `Money`, and
  `SessionExpiredException` — these are auth/utility C#, not visual components.
- **NuGet dependency added.** `MudBlazor` (latest stable) is referenced from `Atrium.Design` so it
  flows transitively to the Portal and every module.

## Consequences

- **Supersedes [ADR-0010](0010-native-dialog-primitive.md).** The native `<dialog>` primitive is
  retired in favor of `MudDialog`, which provides equivalent accessibility (focus trap, Esc-to-close,
  backdrop) via the library.
- **Retires** `atrium.css`, `tokens.css`, `dialog.js`, `theme.js`, and the ~10 custom primitive
  `.razor` files. Brand identity is preserved through `AtriumTheme`, not through bespoke CSS.
- **Adds a NuGet dependency.** MudBlazor is well-maintained and the hiring-market idiom for Blazor
  Server; the dependency risk is low relative to the demo value gained.
- **Guardrails updated.** The `atrium-ui` skill now enforces MudBlazor components + `AtriumTheme`
  rather than custom tokens + primitives; the `atrium-module` skill's design section updated
  accordingly.

## Alternatives rejected

- **Keep the hand-rolled system.** Leaves a gap in the demo story (no mainstream Blazor component
  library); the "demonstrates real CSS skill" rationale is sound but weaker than idiomatic Blazor
  with a brand-themed library.
- **Feature-flag the migration.** Adds complexity without benefit; old CSS and MudBlazor would
  conflict on specificity during the transition.
- **Use a different component library** (Radzen, Telerik, Fluent UI Blazor). MudBlazor is the
  hiring-market idiom for Blazor Server; it's what an interviewer expects to see.
