# Topbar consolidation — design

Date: 2026-07-02

## Problem

The portal top-right cluster (`MainLayout.razor:19-33`) is crowded and inconsistent:

- **Two icon systems.** ThemeToggle renders a Unicode glyph `☾/☀` (solid); AssistantLauncher
  ("Support") renders a stroke SVG (outlined). Side by side, one looks filled, one doesn't.
- **Three text treatments adjacent.** `admin` is a muted `<span>` (`.topbar__user`); "Support" and
  "Sign out" are `.btn--ghost` buttons. Different weights/roles sitting together.
- **Icon+label mismatch.** Support shows icon *and* label; theme is icon-only.
- **Crowded.** Four independent top-level controls with room only to get worse.

No dropdown/menu primitive exists in `Atrium.Design` yet.

## Decision

**Hybrid, 2 icons + user menu** (user-selected):

```
TOPBAR RIGHT:  [ chat ]  [ theme ]        [ (A) ▾ ]
                Support   dark mode         UserMenu
```

- **Support** and **dark mode** stay in the bar as matching **icon-only** buttons (labels → tooltips).
- **Username + Sign out** collapse into an avatar dropdown (single initial, e.g. `admin` → `A`).

## Components

1. **`Atrium.Design/Components/Menu.razor`** — new reusable dropdown primitive.
   - Trigger slot + panel of items, positioned below-right.
   - Opens on click; closes on outside-click (transparent full-viewport backdrop, same no-JS trick as
     `nav-backdrop`) and on `Esc`.
   - ARIA: `aria-haspopup`, `aria-expanded`, `role="menu"`.
   - Smallest clean version: no `MenuItem` sub-component yet — consumers supply `.menu__header` /
     `.menu__item` markup (only consumer today is UserMenu).

2. **`ThemeToggle.razor`** — replace Unicode `☾/☀` with feather-style **stroke** moon/sun SVGs
   (`currentColor`, `stroke-width: 2`, 16×16) matching the Support chat icon exactly. Behavior unchanged.

3. **`AssistantLauncher.razor`** — drop the visible `@_surface.Name` text (icon-only); keep the name as
   `title`/`aria-label`. Standardize icon to 16×16.

4. **`Atrium.Portal/Components/Layout/UserMenu.razor`** — new. Avatar button (single initial in a filled
   `--accent` circle) opens the `Menu` with a `name / Signed in` header and a **Sign out** item.
   `NotAuthorized` → existing "Sign in" button. Replaces the user span + Sign out block in `MainLayout`.

5. **`atrium.css`** — add `.menu*`, `.avatar`, `.topbar__icon` from tokens (no literals). Remove the now
   unused `.topbar__user`.

## Consistency outcomes

- Every topbar icon is outlined, `currentColor`, same 16px size, same stroke weight.
- The bar holds icons + one avatar — no three-fonts-in-a-row problem; menu items share one type scale
  (`--text-sm`) and one hover (`--surface-2`).

## Scope guard

Only the right cluster and the shared ThemeToggle icon. No changes to breadcrumb, mobile Menu button,
or sidebar.

## Verification

`dotnet build` clean; Playwright screenshot of the topbar + open menu in **both** themes; confirm the
Sign out link still points at `/account/logout`.
