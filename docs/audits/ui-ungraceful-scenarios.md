# UI audit: ungraceful scenarios

**Date:** 2026-07-01 · **Branch:** `overnight/2026-07-01` · **Scope:** `Atrium.Portal`, `Atrium.Modules.{Storefront,Admin,Reports}`, `Atrium.Design` (static read-only audit)

## What "ungraceful" means here

A screen is *ungraceful* when the **unhappy path** (a downstream 500/503/timeout/network drop, an empty result, a wrong-role 403, or a null field) produces a jarring dead-end instead of an inline, contextual message the user can act on.

The known "first instance" — an expired access token (no refresh in this demo) — is **already fixed**: the typed clients call `ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`, raising `SessionExpiredException`, which `SessionErrorBoundary` (`src/Atrium.Portal/Components/Layout/SessionErrorBoundary.razor:13`) turns into a friendly "sign in again" notice card. This audit hunts for the *other* places the UX degrades badly.

**The dominant pattern:** every read page awaits its typed client directly in `OnInitializedAsync` with **no try/catch**. For a *non-401* failure (a 500, a 503, a dropped connection), `EnsureSuccessStatusCode()` throws `HttpRequestException`, which bubbles to `SessionErrorBoundary`'s **generic** branch — a full-page "Something went wrong" card (`SessionErrorBoundary.razor:24-29`) that replaces the entire page body. It is caught (no circuit crash) and offers a Reload, so it is recoverable, but it is not contextual: the user loses the page and can't tell a transient blip from a real failure or retry just the failed call.

**Counts:** 1 High · 4 Medium · 5 Low.

> **Resolution (2026-07-01):** All **Medium (M1–M4)** and **Low (L1–L5)** findings are **fixed** — a
> shared `Atrium.Design/Notice` primitive backs inline error+retry on the four unguarded reads and the
> Forbidden/NotFound/session-expired cards; Admin gained client-side validation, a Save re-entrancy
> guard, and the Dialog interop is now guarded against `JSDisconnectedException`. The **High (H1)** —
> `CartPage.PlaceOrder` duplicate-order risk — is **left open by design**: the right fix needs an
> idempotency decision (server-side order key), not a blind try/catch.

---

## High

| # | Location | Bad UX | Fix | 
|---|----------|--------|-----|
| H1 | `src/Atrium.Modules.Storefront/Pages/CartPage.razor:65-87` (`PlaceOrder`) | `try/finally` resets `_placing` but there is **no `catch`**. A 500/timeout on `Orders.CreateAsync` bubbles to the generic boundary card, bouncing the user off the cart. The cart *is* preserved (`Cart.Clear()` runs only on success), but the user cannot tell whether the order was created — after Reload they may **place a duplicate order**. Order creation is the one non-idempotent action in the app, so the ambiguity is a real data-integrity risk. | Wrap the call in `try/catch`; on a non-session failure keep the user on the cart and show a `ToastVariant.Danger` "Couldn't place your order — please try again"; do not clear the cart. |

---

## Medium

All four are the same "unguarded read → generic full-page card" pattern. Reads are idempotent, so retry via Reload is safe (hence Medium, not High), but the UX drops the whole page instead of an inline "couldn't load, retry."

| # | Location | Bad UX | Fix |
|---|----------|--------|-----|
| M1 | `src/Atrium.Modules.Storefront/Pages/Shop.razor:65-83` | `GetCategoriesAsync()` + `LoadProductsAsync()` unguarded. A catalog 500 (or a throw inside `Filter`) replaces the browse page with the generic card; a failed `Filter` also leaves `_products` null → skeleton, not an error. | try/catch around the loads; render an inline error panel with a Retry button in place of the grid; keep the page chrome. |
| M2 | `src/Atrium.Modules.Storefront/Pages/OrdersPage.razor:58` | `_orders = await Orders.GetOrdersAsync()` unguarded → generic card on any Storefront-service failure. | Same: inline error + retry region instead of the boundary card. |
| M3 | `src/Atrium.Modules.Reports/Pages/Dashboard.razor:64-67` | `Reports.GetSalesAsync()` unguarded → generic card if the analytics compose fails. | Same. |
| M4 | `src/Atrium.Modules.Admin/Pages/Products.razor:117-123` | `GetCategoriesAsync()` + `LoadAsync()` unguarded → generic card on a Catalog **read** failure. (Note: the admin **write** path is handled well — see below.) | Same inline error + retry for the initial load. |

---

## Low (polish)

| # | Location | Bad UX | Fix |
|---|----------|--------|-----|
| L1 | `src/Atrium.Portal/Components/Pages/Forbidden.razor:1-5` | Bare `<h3>Access denied</h3>` with no `PageHeader`/design tokens — a stark, off-brand dead-end compared with the polished session-expired notice card. | Render inside a `notice card` / `PageHeader` for visual consistency. |
| L2 | `src/Atrium.Portal/Components/Pages/NotFound.razor:1-5` | Same bare markup, **and no link at all** back home — a true dead-end for a mistyped URL. | Use design chrome and add a "Return to home" link. |
| L3 | `src/Atrium.Modules.Admin/Pages/Products.razor:82-98` | No client-side field validation: empty Name/Blurb or `Price = 0` submit fine and rely on a server 400 round-trip. (The 400 body *is* surfaced via toast — `AdminCatalogClient.cs:96` — so it's recoverable, just chatty.) | Add `required`/min validation and disable Save until the form is valid. |
| L4 | `src/Atrium.Modules.Admin/Pages/Products.razor:144-169` (`Save`) | Unlike `CartPage.PlaceOrder`, `Save` has no early `if (_saving) return;` re-entrancy guard — it leans solely on the disabled button. Low risk but inconsistent with the cart's belt-and-suspenders. | Add the early-return guard for parity. |
| L5 | `src/Atrium.Design/Components/Dialog.razor:39-56` (`OnAfterRenderAsync`) | JS interop (`import`, `showModal`, `close`) is not guarded against `JSDisconnectedException`; only `DisposeAsync` is. A disconnect mid-render could throw an uncaught interop exception. Edge case. | Wrap the interop in a `try/catch (JSDisconnectedException)` like `DisposeAsync` already does. |

---

## Already handled well (balance)

- **Session expiry** — the known fix: `ThrowIfSessionExpired()` in every client → `SessionExpiredException` → friendly re-login notice card (`SessionErrorBoundary.razor:13-22`); `MainLayout` calls `Recover()` on navigation (`MainLayout.razor:77`).
- **Loading states** — all four data pages show skeletons while null: Shop (`Shop.razor:29-37`), Orders (`OrdersPage.razor:13-24`), Reports (`Dashboard.razor:11-19`), Admin (`Products.razor:16-43`).
- **Empty states** — comprehensive: Shop "No products in this category" (`Shop.razor:38-41`), Cart "Your cart is empty" (`CartPage.razor:13-16`), Orders "No orders yet" (`OrdersPage.razor:25-28`), Reports "No orders yet" (`Dashboard.razor:39-42`), Admin "No products yet" (`Products.razor:44-47`), Home "No modules discovered" (`Home.razor:12-15`).
- **Admin writes** — a model for the fix: 403/400/404 are translated to inline toasts and the dialog stays open to retry (`AdminCatalogClient.cs:93-101`, `Products.razor:157-161`); only 401 escalates to the boundary.
- **Double-submit during flight** — `CartPage.PlaceOrder` guards with `_placing` + disabled "Placing…" button (`CartPage.razor:58,68-72`); `Admin.Save` disables the button via `_saving` (`Products.razor:101-102`).
- **Wrong-role paths** — Admin/Reports pages are role-gated (`[Authorize(Roles="admin")]`), NavMenu hides links the user can't use (`NavMenu.razor:26-32`), and `Routes.razor:7-18` renders a clean `Forbidden` page instead of a login bounce. Consequently the read endpoints **can't 403 for a reachable user**, so there's no in-page wrong-role read gap.
- **Dialog** — backdrop clicks intentionally do not dismiss (no accidental edit loss), Esc/X handled, `JSDisconnectedException` swallowed on dispose (`Dialog.razor:8,71-84`).
- **Null data** — all DTOs are non-nullable positional records (`ProductDto`, `OrderDto`, `SalesReportDto` in `src/Atrium.Contracts`), so the `@item.Field` renders are contract-safe; no null-guard gap found.
