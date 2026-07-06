# Clean-up audit — findings for approval

Date: 2026-07-01. Three read-only audits (overengineering, dead code, UI-module slop) run over the
Atrium codebase. **Nothing here is fixed yet.** Tick approve/deny per item; I'll implement only what
you approve.

Each finding was cross-checked by hand before landing here — corrections to the raw audit output are
called out in _italics_. Two raw findings were **rejected on verification** (see the end of each
section) so you don't act on a bad call.

**Cross-cutting theme:** the highest-value cluster is **HTTP-client duplication** — OE-2, SL-1, and
SL-2 are three views of the same thing (five typed clients each hand-rolling token attachment +
401-vs-other logging, with drift). Fixing them together as one small refactor resolves all three.

Legend — Severity: High (clear win) / Med / Low (subjective). Effort: S/M/L.

---

## 1 · Overengineering

### OE-1 · `UseAtriumRequestLogging()` is a pass-through wrapper
- **Location:** `src/Atrium.ServiceDefaults/TelemetryExtensions.cs:78-82`
- **Severity:** Low · **Effort:** S
- Wraps `UseSerilogRequestLogging()` and returns `app`, adding no logic. Used in 4 hosts.
- **Action:** Either inline `UseSerilogRequestLogging()` at the call sites, or _keep it_ as a named
  seam if you value the consistent `AddAtrium*`/`UseAtrium*` vocabulary across hosts. Genuinely
  borderline — a named convention has some value even when the body is thin.
- [ ] Approve · [ x] Deny

### OE-2 · Duplicated `LogIfUnsuccessful` across 5 HTTP clients
- **Locations:** `Atrium.Modules.Storefront/Catalog/CatalogClient.cs:57-84`,
  `Atrium.Modules.Storefront/Orders/OrdersClient.cs:62-89`,
  `Atrium.Modules.Admin/AdminCatalogClient.cs:106-125`,
  `Atrium.Modules.Reports/ReportsClient.cs:41-68`,
  `Atrium.Services.Storefront/Catalog/StorefrontCatalogClient.cs:41-68`
- **Severity:** Med · **Effort:** S
- The 401-vs-other-failure structured-logging block is copy-pasted 5×. See also SL-1 (one copy drifted
  to an instance method) and SL-2 (auth attachment has the same problem).
- **Action:** Extract one shared helper (e.g. `HttpResponseLoggingExtensions.LogIfUnsuccessful(logger,
  request, response)` in `Atrium.Design`), and ideally a shared `Authorize` extension, then delete the
  per-client copies. Resolves OE-2 + SL-1 + SL-2 in one pass.
- [ x] Approve · [ ] Deny

_The rest of the codebase read as lean — the co-located single-impl repository interfaces (ADR-0007),
Dapper/sprocs/DbUp/Mapperly, and feature folders are deliberate and were not flagged._

---

## 2 · Dead code / cruft

### DC-1 · Unused `Card` component
- **Location:** `src/Atrium.Design/Components/Card.razor`
- **Severity:** Low · **Effort:** S
- Verified: **zero `<Card>` instantiations** anywhere in `src/` or `tests/`. The component is dead.
- _Correction to the raw audit:_ its **CSS is not fully dead.** The base `.card` class **is used** by
  `Notice.razor` (`class="notice card"`) and `OrdersPage.razor` (`class="card order-card"`), so it must
  **stay**. Only the `a.card` / `a.card:hover` rules (`atrium.css:331-342`) become dead once the
  component is gone, because no markup uses `<a class="card">` outside the component.
- **Action:** Delete `Card.razor`; optionally trim only the `a.card` / `a.card:hover` rules. **Keep the
  base `.card` rule.**
- [ x] Approve (delete component + trim `a.card` rules) · [ ] Approve (delete component only) · [ ] Deny

### DC-3 · Debug screenshot committed-adjacent in repo root
- **Location:** `wrong_position_modal.png` (repo root, ~518 KB, untracked)
- **Severity:** Med · **Effort:** S
- The modal-positioning bug it captured is now fixed. It's a throwaway debug artifact.
- **Action:** Delete. (It's untracked, so this is just `rm`.)
- [ x] Approve · [ ] Deny

**Rejected on verification:**
- ~~DC-2 (delete `.card` CSS at atrium.css:325-342)~~ — **rejected.** `.card` is live (Notice,
  OrdersPage). Folded the true remainder into DC-1.

**Left alone (untracked, user-authored working docs — no action):** `ATRIUM-AI-EXTENSIBILITY-DESIGN.md`,
`docs/JS-STACK-OPTIONS-A-VS-B.md`. Flag if you want them tracked or removed.

_Otherwise clean: no commented-out blocks, no TODO/FIXME cruft, no unused usings/DI/packages (build is
0-warning), no empty catches._

---

## 3 · UI-module slop / inconsistency

### SL-1 · `AdminCatalogClient` logging method drifted to an instance method
- **Location:** `src/Atrium.Modules.Admin/AdminCatalogClient.cs:106`
- **Severity:** Med _(raw audit said High; downgraded — it's a consistency nit, not a defect)_ · **Effort:** S
- `AdminCatalogClient` has `private void LogUnsuccessful(...)` while the other three modules have
  `private static void LogIfUnsuccessful(ILogger, ...)`. Same logic, drifted shape/name.
- **Action:** Fold into the OE-2 shared helper (preferred), or at least make it match the others.
- [ x] Approve · [ ] Deny

### SL-2 · Bearer-token attachment: inline in 2 clients, extracted in 2
- **Location:** inline in `CatalogClient.cs:40-46` & `ReportsClient.cs:24-30`; extracted to
  `Authorize()` in `OrdersClient.cs:49-58` & `AdminCatalogClient.cs:127-136`
- **Severity:** Med · **Effort:** M
- Same responsibility, two patterns, no reason for the split.
- **Action:** Standardize on the extracted `Authorize()` form (cleaner); fold into OE-2's refactor.
- [ x] Approve · [ ] Deny

### SL-3 · `CartPage` uses a raw `<button>` where the rest uses `<Button>`
- **Location:** `src/Atrium.Modules.Storefront/Pages/CartPage.razor:45` (Remove button)
- **Severity:** Med · **Effort:** S
- Line 45 is a raw `<button class="btn btn--ghost btn--sm">`; line 58 (Place order) uses the `<Button>`
  primitive. Breaks the design-system reuse contract.
- **Action:** Replace with `<Button Variant="ButtonVariant.Ghost" ...>` (confirm the small-size param
  name against the primitive).
- [ x] Approve · [ ] Deny

### SL-4 · Catch-idiom inconsistency in `CartPage` _(reframed)_
- **Location:** `CartPage.razor:94` vs `Shop.razor:95`, `OrdersPage.razor:78`, `Dashboard.razor:85`,
  `Products.razor:152`
- **Severity:** Low · **Effort:** S
- _Correction to the raw audit:_ its "unused exception variable" claim is **wrong** — `ex` is used in
  the `when (ex is not SessionExpiredException)` filter, so there's no smell and no compiler warning.
- The real (minor) point: the four read pages use `catch (Exception ex) when (ex is not
  SessionExpiredException)`, while `CartPage` (the idempotency work I just added) uses `catch
  (SessionExpiredException) { throw; } catch (Exception)`. Both are correct; they just read differently.
- **Action:** Optional — align `CartPage` to the single `when`-filter idiom for consistency.
- [ x] Approve · [ ] Deny

_Otherwise the modules read consistently: design tokens, Notice/PageHeader/Button adoption, and error
boundaries are used uniformly._

---

## Suggested batching (if you approve)

1. **HTTP-client consolidation** (OE-2 + SL-1 + SL-2) — one small refactor, shared helper in
   `Atrium.Design`, delete 5 copies. Highest signal.
2. **Design-system reuse** (SL-3, optionally SL-4) — small, self-contained.
3. **Deletions** (DC-1, DC-3) — trivial.
4. **OE-1** — only if you want the wrapper gone; otherwise deny.
