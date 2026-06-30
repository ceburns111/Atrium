---
name: atrium-ui
description: >-
  Use whenever building or editing ANY Atrium UI — the Blazor Server portal shell, a UI module
  (Storefront / Admin / Reports), or the Atrium.Design RCL itself. Enforces visual consistency and
  reuse: pull from the shared design tokens and Atrium.Design primitives instead of writing ad-hoc
  CSS, hard-coding colors/spacing, duplicating components per module, or adding UI libraries. Trigger
  this even when the request is just "add a page", "style this", "build the cart view", "make a
  table", or any Razor/component/CSS work in this repo — consistency erodes silently when each screen
  reinvents the basics.
---

# Atrium UI — consistency & reuse

Atrium is a modular host: many UI modules live behind one shell and must look like **one product**.
Consistency comes from a shared design system (`Atrium.Design`), not from each module getting it
"close enough." This skill is the mechanics of staying consistent and lean. For aesthetic *direction*
(palette mood, typographic feel, what makes it not look templated), defer to the **frontend-design**
skill — this skill assumes the direction is set and keeps every screen faithful to it.

Plan & rationale: `docs/ATRIUM-PLAN.md` (UI / design strategy section).

## Reuse before you write

Before adding any markup or CSS, check whether `Atrium.Design` already covers it:

1. **Need a component** (button, card, table, input, badge, page header, toast)? Use the
   `Atrium.Design` primitive. If it's *almost* right, extend the primitive (a parameter, a variant
   class) — don't fork a near-copy into the module.
2. **Need a value** (a color, a gap, a radius, a font size, a transition)? Use a **design token**
   (CSS custom property), never a literal. `padding: var(--space-3)`, not `padding: 12px`.
3. **Genuinely new and reusable?** Add it to `Atrium.Design` so every module benefits — not to the
   module. A component used by one module today is used by three tomorrow.
4. **Truly module-local** (a layout only this page needs)? Keep it in the module, but still built
   from tokens and primitives.

The test: if two modules would each write the same thing, it belongs in `Atrium.Design`.

## Rules that keep it coherent

- **Tokens, not literals.** Color, spacing, radius, shadow, type size, and transition duration all
  come from CSS custom properties. A hard-coded hex or pixel value is a consistency leak and a review
  flag. Spacing follows the **8px scale**; type follows the **type scale** — don't introduce
  off-scale one-offs.
- **Layout with flex/grid.** Use flexbox and CSS grid for structure (toolbars, card grids, the app
  shell, tables). Reach for the box model deliberately — predictable padding/margin via the spacing
  scale, no magic-number nudges.
- **Every interactive element earns its states.** `:hover`, `:focus-visible`, `:active`, and
  `:disabled` are all styled, and `:focus-visible` is never removed without a visible replacement
  (keyboard users and accessibility). Motion is snappy: ~120–160ms transitions, not slow fades.
- **No new UI dependencies.** MudBlazor was removed on purpose — the point is a hand-rolled system
  that demonstrates real CSS skill. Don't add a component library, an icon mega-pack, or a CSS
  framework. A small set of crafted primitives beats a dependency.
- **Smallest clean implementation.** This is a focused demo, not a framework. Prefer the simplest
  thing that reads well over a configurable abstraction. Don't build variants, props, or theming hooks
  no screen uses yet. Avoid premature generalization as firmly as you avoid duplication — they're the
  two failure modes, and the right cut is the smallest design that stays DRY.

## After UI work

- Look at it. Use the **Playwright MCP** to screenshot the running screen and check spacing rhythm,
  alignment, focus states, and a narrow viewport before calling it done — don't ship CSS unseen.
- A quick `code-simplifier` / `/simplify` pass catches duplication and over-abstraction that crept in.

## Concrete primitives & tokens

Filled in once **Phase 2** builds `Atrium.Design`. Until then, follow the planned set from
`docs/ATRIUM-PLAN.md`: primitives `Button`, `Card`, `DataTable`, `Field`, `Badge`, `PageHeader`,
`Toast`; tokens for palette (neutrals + one accent + semantic success/danger), 8px spacing scale,
radius/shadow scale, type scale, and transition timings. **Update this section with the real
class/component/token names when they land**, so future UI work references reality, not the plan.
