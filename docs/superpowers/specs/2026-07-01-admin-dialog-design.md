# Design — Dialog primitive + modal-ise Admin create/edit

**Date:** 2026-07-01 · **Status:** Approved, ready to implement · **Supersedes:** the task-#8 CSS patch
for the Admin edit-row action overflow.

## Problem

The Admin Products page has two layouts for the same four-field form (Name / Category / Price / Blurb):
a **create card** at the top and an **inline edit `<tr>`**. The inline edit row also has a layout bug —
its Save/Cancel actions overflow the table's right edge (screenshot, 2026-07-01). Two layouts for one
form is duplication; the overflow is a symptom of cramming a form into a table row.

## Goal

Replace both with **one modal dialog** containing **one shared form**, driven by mode
(`create` vs `edit`). This deletes the duplication and dissolves the overflow bug, and gives the design
system a reusable, accessible `Dialog` primitive.

## Approach

Build on the **native HTML `<dialog>` element** driven by `showModal()`, not a hand-rolled overlay.
The platform provides focus trapping, `Esc`-to-close, top-layer stacking, `::backdrop`, and
return-focus-to-trigger — accessibly, without a home-grown focus trap (which is a review risk). Cost is
a ~10-line JS interop module (`showModal` / `close`). No third-party UI dependency (per atrium-ui).

## Components

### 1. `Dialog` primitive — `Atrium.Design/Components/Dialog.razor` (+ scoped `Dialog.razor.css`)

- **Parameters:** `Open` (bool) + `OpenChanged` (`EventCallback<bool>`, two-way bind); `Title` (string);
  `ChildContent` (body); `Footer` (`RenderFragment`). Header shows the title and an **X** close button.
- **Open/close sync:** internal `_shown` flag. In `OnAfterRenderAsync`: `Open && !_shown` → JS
  `showModal()`, set `_shown`; `!Open && _shown` → JS `close()`, clear `_shown`.
- **Native close → Blazor:** the `<dialog>`'s `close`/`cancel` event binds to `@onclose`, which fires
  `OpenChanged(false)` so Esc/programmatic closes keep component state in sync.
- **Close affordances:** Esc (native), X button, and whatever the caller puts in `Footer` (Cancel).
  **No backdrop-click dismiss** — avoids losing in-progress edits on a stray click.
- **Styling:** scoped `Dialog.razor.css`, tokens only — panel uses the `.card` surface/line/radius
  language, dimmed `::backdrop`, `--space-*` rhythm, `:focus-visible` on the X.
- **JS:** `Atrium.Design/wwwroot/js/dialog.js` — `export function showModal(el)` / `close(el)`,
  loaded as a JS module via `IJSRuntime`.

### 2. Admin refactor — `Atrium.Modules.Admin/Pages/Products.razor`

- One `ProductForm` + one `Dialog`. `_editingId` (`null` = create) drives the dialog title
  ("New product" / "Edit product") and whether Save calls `CreateProductAsync` or `UpdateProductAsync`.
- **Remove:** the top create-card block, the inline-edit `<tr>` branch, and their `.admin-form` /
  `.admin-table__actions` overflow CSS. The table becomes read-only rows; **Edit** opens the dialog
  pre-filled, **New product** opens it empty.
- **Unchanged:** the `_saving` in-flight guard, error→toast (403 "need admin role"), and the new
  session-expiry handling all carry over.

## Testing

No new unit tests (Razor + DOM/interop). Verify by build + manual/Playwright, folded into the pending
#1 browser-verify pass: open via New product and via Edit; save both paths persist; Esc / X / Cancel
each close; focus returns to the trigger; narrow-viewport check.

## Out of scope

Generalized Dialog variants (sizes, confirm dialogs, stacking) — YAGNI until a second caller needs one.
Only the API Admin needs gets built now.
