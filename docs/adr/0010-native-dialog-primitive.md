# ADR-0010 — Build the modal Dialog on the native `<dialog>` element, not a hand-rolled overlay

**Status:** Superseded by [ADR-0014](0014-adopt-mudblazor.md) · **Deciders:** Atrium build · **Context phase:** 7 (post-polish)

## Context

Admin's product management showed one four-field form in **two** layouts: a create card plus an
inline-edit `<tr>` whose Save/Cancel actions overflowed the table's right edge. Collapsing both into a
single modal removed the duplication and the overflow bug together — but "add a modal" invites a pile of
hand-rolled accessibility work: a focus trap, Esc-to-close, top-layer stacking above every other
element, a backdrop, and returning focus to the trigger on close. Getting all of that right by hand is
easy to get subtly wrong.

## Decision

Add a reusable `Dialog` primitive to `Atrium.Design` built on the **native `<dialog>` element**, opened
via `showModal()`.

- **Component:** `src/Atrium.Design/Components/Dialog.razor` (`@namespace Atrium.Design`), scoped CSS in
  `Dialog.razor.css`, tokens only — no UI library.
- **Platform does the hard parts.** Calling `dialog.showModal()` (rather than the `open` attribute)
  gives us the focus trap, Esc-to-close, top-layer stacking, `::backdrop`, and return-focus **for free**
  from the browser, instead of a hand-rolled trap we'd have to maintain.
- **Tiny JS interop.** `src/Atrium.Design/wwwroot/js/dialog.js` is a ~10-line ES module exporting just
  `showModal(dialog)` and `close(dialog)`, each guarded (`if (dialog && !dialog.open)` / `dialog.open`)
  so Blazor re-renders can't double-open or double-close. Imported per-component as
  `./_content/Atrium.Design/js/dialog.js`.
- **Declarative surface.** Two-way `Open` / `OpenChanged`, plus `Title`, `ChildContent` (body), a
  `Footer` slot, and a header ✕ button. `OnAfterRenderAsync` syncs the imperative `showModal`/`close`
  calls to the `Open` parameter; the native `@onclose` relays Esc / programmatic closes back through
  `OpenChanged`, so declarative and imperative state stay in sync. `IAsyncDisposable` cleans up the JS
  module (swallowing `JSDisconnectedException` when the circuit is already gone).
- **Backdrop clicks intentionally do NOT dismiss** — that would silently discard in-progress edits.

Admin's `Pages/Products.razor` now uses **one** `Dialog` + one shared `ProductForm`, with `_editingId`
(`null` = create) selecting title, label, and create-vs-update — the create card and inline-edit row
(and their dead CSS) are gone. This **supersedes** the earlier action-overflow CSS patch.

## Consequences

- **Accessibility comes from the platform.** Focus trap, Esc, top-layer, backdrop and return-focus are
  the browser's, not ours — less code to own and fewer ways to get a11y subtly wrong.
- **One reusable primitive.** `Dialog` lives in `Atrium.Design`, so future modals across modules reuse
  it instead of re-inventing an overlay per screen (consistent with the design-system direction).
- **One form, two modes.** Admin create and edit share a single form + modal; the table no longer
  overflows and the duplicate layouts are gone.
- **Small JS surface accepted.** A ~10-line interop module is the cost of driving the imperative
  `showModal()`/`close()` API from Blazor — far less than a hand-rolled trap would be.

## Alternatives rejected

- **Hand-rolled overlay `<div>` + custom focus trap.** Re-implements what `<dialog>` already does
  correctly (trap, Esc, top-layer, backdrop, return-focus); more code and more a11y risk.
- **A third-party modal/UI library.** Pulls in a dependency for one primitive and cuts against the
  tokens-and-`Atrium.Design`-only approach.
- **Keep the inline-edit row.** Leaves the create/edit layout duplication and the action-overflow bug
  in place.
