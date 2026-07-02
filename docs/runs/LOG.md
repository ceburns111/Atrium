# Run log (append-only)

## 2026-07-01 — interactive session (Opus 4.8)

- `843b337` — Graceful 401 / session-expiry handling: `SessionExpiredException` +
  `ThrowIfSessionExpired`, all four typed clients map 401, `SessionErrorBoundary` in the shell. Unit 18/18.
- `5ad1f4b` — Co-located single-impl repository interfaces into their class files (Catalog/Order/Report).
- `4872c26` — Nested Storefront routes under one `/storefront` parent group; features as relative subgroups.
- `8483066` — Added `Dialog` primitive (native `<dialog>`/`showModal`) and modal-ised Admin create/edit;
  removed the create-card + inline-edit row + dead CSS.
- Also: committed the Dialog/modal design spec; a csharpier formatting nit in CatalogEndpoints.
- Set up this `docs/runs/` system + `atrium-overnight-run` memory for resumable work.
- **Not yet verified in a browser:** the session-expired panel and the Admin modal (stack was on the old
  build). Deferred to a supervised step-4 pass.

### Queue state at handoff
Nothing started in the queue. Next: create branch `overnight/2026-07-01`, green-baseline check, then item
10 (ADR sweep). Plan: run the whole queue unattended on that branch (docs 10,2,3,1 → code 4,6 → low/
tomorrow), one atomic commit per item, orchestrator+subagents, escalation/backoff per README. Deterministic
gate only; live verification deferred to a supervised morning pass. Docker up; aspire not required.

## 2026-07-01 — overnight run resumed (Opus 4.8)

- **Baseline (start commit `335b2a2`):** branch `overnight/2026-07-01` created off `main`. Tree clean;
  `csharpier` no-op; `dotnet build` 0W/0E; `dotnet test` 22/22 green; Docker up. Green baseline confirmed —
  cleared to run the queue. Starting item 10 (ADR sweep).
- `ee63214` — **Item 10 (ADR sweep)** done. New ADR-0008 (session-expiry handling), ADR-0009 (service-root
  route nesting), ADR-0010 (native `<dialog>` primitive); refreshed ADR-0007 (co-located repo interfaces),
  cross-linked ADR-0004 → 0008; README index synced. Implementer subagent (high confidence), Tier 0.
  Gate: build 0W/0E; every cited path/class/method/route grep-verified. Orchestrator caught one accuracy
  gap — ADR-0008 claimed 0004 linked back but 0004 had no explicit link; added the bidirectional link
  before commit. Docs only → no live verification needed.
- `14538c0` — **Item 2 (new-app guide)** done. New `docs/guides/wire-up-a-new-app.md` (407 lines): the
  end-to-end source-of-truth for adding a vertical, narrating the real Storefront+Catalog code, cross-linked
  to ADR-0001..0009. Implementer subagent (high confidence), Tier 0. Gate: build 0W/0E; 9 cited paths + 4
  symbols spot-checked, ARCHITECTURE.md link + csproj glob confirmed. Subagent accurately noted OpenAPI is
  NOT yet wired (only `.WithTags` exists) — that's item 4's job, not a doc error. Docs only → no live verify.
- **Policy change (user, live, 2026-07-01):** user authorized unattended skill authoring under
  `.claude/skills/**`. Grant added to `.claude/settings.local.json` (`autoMode.allow`, skills-scoped only;
  gitignored). Two classifier self-modification denials en route (item-3 dispatch, then the settings write)
  — correctly refused to let the agent grant *itself* permissions; the user authored/authorized the grant.
  New `docs/runs/SKILL-REVIEW.md` ledger tracks every auto-authored skill for morning keep/discard.
- `f30e9ae` — **Item 3 (AGENTS.md + authoring skills)** done. Root `AGENTS.md` orientation hub + three real
  skills `atrium-service`/`atrium-module`/`atrium-contracts` (mirror `atrium-ui` frontmatter; all load
  cleanly — harness re-listed them). Implementer subagent (high confidence), Tier 0. Gate: build 0W/0E;
  frontmatter valid, cross-links resolve, cited symbols (`IModule.BasePath`, `ProductDto`) verified. Skills
  logged in SKILL-REVIEW.md as `pending`. `.gitignore` updated to exclude the local settings grant.
- `a25c62f` — **Item 1 (README per project)** done. 13 READMEs (11 `src/*` + 2 `tests/*`), one consistent
  5-heading shape, all new (no overwrites). Implementer subagent (high confidence), Tier 0. Gate: build
  0W/0E; cited types/routes/sprocs/run-cmd grep-verified. Docs only → no live verify.
  - Note: a root file `ATRIUM-AI-EXTENSIBILITY-DESIGN.md` appeared mid-run — **the user's own** (they
    confirmed live); left untracked and untouched, NOT part of any queue item.
- **★ SAFE-REVERT-POINT = `a25c62f`** — end of the docs phase. All docs (10/2/3/1) are at/before this
  commit. `git reset --hard a25c62f` discards the whole code phase while keeping every doc. Everything
  after this line is CODE (items 4, 6, …), committed unit/integration-verified, live-verification deferred.

### CODE PHASE (starts here)

- `d51a902` — **Item 6 (OTel + Serilog)** done. New shared `Atrium.ServiceDefaults` (Serilog provider +
  OTel tracing, OTLP-guarded) wired into all four hosts. Implementer (high confidence) + **Tier-1 review:
  APPROVE WITH NOTES**. **One repair applied before commit:** Serilog replacing the MEL factory dropped the
  hosts' `appsettings` `Microsoft.AspNetCore→Warning` override (resurfaced framework Info logs + doubled
  request logging) → restored via `MinimumLevel.Override` in the shared wiring. Gate: csharpier no-op,
  build 0W/0E, `dotnet test` 22/22.
  - **Checklist correction (from review):** logs use a Console sink → Aspire dashboard **Console** tab (not
    **Structured**, which needs OTLP log export via `Serilog.Sinks.OpenTelemetry` — left as a supervised
    enhancement). Traces DO span Portal→Gateway→Service→SQL (Blazor interactive-circuit calls start their
    own root trace — expected).
  - **LIVE-VERIFICATION DEFERRED (morning):** `aspire run` → dashboard **Traces**: exercise a Storefront
    action, confirm one trace spans portal→gateway→storefront→catalog with SQL child spans; **Console** tab
    shows structured Serilog request lines.

- **Follow-on item queued (user, 2026-07-01, live): "application logging."** Audit found **~zero deliberate
  app logging** (1 `ILogger` injected in `SessionErrorBoundary.razor`, 0 `Log*()` call sites). Item 6 gave
  us the pipeline; this item adds purposeful structured `ILogger<T>` logging at real seams. Deterministic,
  no live stack needed → confidently doable unattended. Added to QUEUE as item **7**.
- **Low/tomorrow — csharpier fluent-chain one-per-line: NOT FEASIBLE (closed).** Verified empirically on
  csharpier 1.3.0: chain-breaking is width-driven and non-configurable (short chain stays inline; long
  chain breaks by heuristic, not uniform one-per-line; unknown config keys silently ignored — only
  `printWidth`/`tabWidth`/`useTabs`/`endOfLine` honored). No config forces one-call-per-line. No code
  change; item closed with evidence.
- **Low/tomorrow — `testuser`/customer-login: investigated (read-only), refined into QUEUE item 8.**
  Finding: realm already has roles `user`/`customer`/`admin`; `testuser`=`[user,customer]` (a customer,
  NOT internal), `admin`=`[user,admin]`. Both personas already exist → **no realm change/volume reset
  needed** (original assumption corrected). Real gap: Admin (`Products.razor`) + Reports (`Dashboard.razor`)
  use bare `[Authorize]` (auth-only) and `NavItem` is role-unaware, so a customer sees/opens Admin+Reports.
  Portal has `RoleClaimType="role"` (Program.cs:63) so role gating will work. Concrete fix planned as item 8
  (page `[Authorize(Roles="admin")]` + role-aware `NavItem` + shell filter). Held until item 7 commits to
  avoid concurrent edits to shared Portal/Modules files. Needs a live testuser-vs-admin login to confirm.
- `153b6bc` — **Item 7 (application logging)** done. Structured `ILogger<T>` at repos (write success/DB
  faults), endpoints (validation/pricing rejects), DbUp init (script counts/errors), all 5 HTTP clients
  (401/non-success seam), and `SessionErrorBoundary`. Implementer (high confidence) + **Tier-1 review:
  APPROVE WITH NOTES** — PII/secrets clean (order username omitted, only ids/counts; no tokens/headers),
  no control-flow change (rethrows intact, logging additive before Throw/EnsureSuccess), template arg-counts
  verified, exceptions passed as first arg (stack traces preserved), tests carry `NullLogger` (none
  weakened). **Applied the one actionable note before commit:** instrumented the previously-missed
  service-to-service `StorefrontCatalogClient`. Gate: csharpier no-op, build 0W/0E, `dotnet test` 22/22.
  Live log-emission = morning check (Aspire Console tab; expected lines noted in the item-7 subagent report).
- `a3a366d` — **Item 8 (role-gate Admin/Reports)** done. Page `[Authorize(Roles="admin")]` on Admin
  `Products.razor` + Reports `Dashboard.razor`; `NavItem` gained optional `RequiredRole`; shell `NavMenu`
  wraps role-gated items in `<AuthorizeView Roles>`. NO realm change (roles/users already exist).
  Implementer (high confidence) + **Tier-1 review: APPROVE** — critically verified the cascading
  `AuthenticationState` (`AddCascadingAuthenticationState`, Program.cs:83) reaches `NavMenu` (so links
  aren't hidden from admins too); page attribute + `AuthorizeView` agree on `IsInRole("admin")`; role
  strings match the realm exactly; Storefront/Home nav unaffected. Gate: csharpier no-op, build 0W/0E,
  test 22/22. **Live check (morning):** testuser(customer) → no Admin/Reports nav + `/admin`,`/reports`
  blocked; admin → both work. **Two follow-ups spun out of the review:** (a) item 9 — clean Forbidden view
  (authenticated wrong-role user currently hits RedirectToLogin bounce); (b) Reports read endpoint is NOT
  admin-gated server-side yet (Storefront service has no "admin" policy; needs live claim-mapping check).
- `7c38f53` — **Item 9 (Forbidden view)** done. Shell `NotAuthorized` branches on auth state:
  authenticated-wrong-role → clean `Forbidden` page (mirrors `NotFound`, non-routable, atrium-ui);
  unauthenticated → `RedirectToLogin` (unchanged). Fixes the login-bounce item 8 made reachable.
  **Self-reviewed, not a separate Tier-1 subagent** (proportionate to a 5-line branch + static page on
  already-reviewed auth architecture). Build 0W/0E, test 22/22. Live check (morning): testuser→`/admin`
  shows Forbidden (no loop).

- `86cbe00` — **UI ungraceful-scenarios audit** done (report, no code). `docs/audits/ui-ungraceful-scenarios.md`
  — 1 High / 4 Medium / 5 Low, each file:line + fix + severity. Orchestrator spot-checked the High
  (`CartPage.PlaceOrder` try/finally-no-catch → dup-order risk) and a Medium (`Shop.OnInitializedAsync` no
  try/catch) against the code — accurate. Findings intentionally NOT auto-fixed: they need user triage /
  judgment (the High one needs idempotency thinking, not a blind catch). Read-only; no code changed.
- **RUN WOUND DOWN HERE — out of confidently-doable unsupervised work (2026-07-01).** Everything remaining
  is supervised/live: all live checks (items 4/6/7/8/9), Dialog aesthetic polish, server-side Reports
  admin-gate (needs live claim-mapping), the audit findings' triage, skill keep/discard. Wrote the
  root-level wake-up summary (`GOOD-MORNING.md`) and stopped cleanly. See STATUS.md.

- `bc7afd7` — **Item 4 (OpenAPI + Redoc)** done. `Microsoft.AspNetCore.OpenApi` 10.0.9 (per-csproj) on
  Catalog + Storefront: `AddOpenApi()` (DI, unconditional) + Dev-only, anonymous `MapOpenApi()`
  (`/openapi/v1.json`) and a Redoc `/docs` page (CDN standalone), mapped outside the bearer-only groups.
  Implementer (high confidence) + **Tier-1 adversarial review: APPROVE WITH NOTES** — reviewer verified
  vs code + context7: default route `/openapi/v1.json` correct, `AllowAnonymous` valid on `MapOpenApi()`,
  no global fallback policy (so reachable), Dev-gate is prod-safe, `.WithTags` (Catalog/Orders/Reports)
  surface as tags. Orchestrator gate: csharpier no-op, build 0W/0E, `dotnet test` 22/22.
  - **LIVE-VERIFICATION DEFERRED (morning):** `cd src/Atrium.AppHost && aspire run`; from the Aspire
    dashboard get each service's own HTTP endpoint (NOT the gateway — it only proxies `/catalog`,
    `/storefront`). Check `http://<catalogPort>/openapi/v1.json` + `/docs` (Catalog tag) and
    `http://<storefrontPort>/openapi/v1.json` + `/docs` (Orders/Reports tags). Confirm each service resolves
    `ASPNETCORE_ENVIRONMENT=Development` or the doc routes won't be mapped.
  - **Non-blocking notes (reviewer, for later):** (1) Redoc CDN is pinned to `latest` — pin a concrete
    `redoc@2.x` for reproducibility. (2) `/docs` `MapGet` has no `.ExcludeFromDescription()`, so it appears
    as an untagged operation in the spec — add it for a clean doc. Neither is a blocker; left for the user.

---

## 2026-07-01 — Run 2 (storefront / checkout / diagrams) · Opus 4.8

New autonomous queue on branch `feat/storefront-checkout-diagrams`, created off the unmerged
`fix/modal-center-and-reports-gate` (which carries the reports-gate + centered-modal work). Run 1 above is
merged to `main`. Source of the new items: `TODO.md` "🆕 New work" + "## Last".

- **Baseline confirmed:** branch cut off `fix/modal-center-and-reports-gate`; `dotnet build` 0W/0E;
  `dotnet test` **23/23** (unit + integration, Docker up); csharpier 1.3.0 no-op. Green — cleared to run.
- **Pre-work commit `7f2f7dc`** — committed the pre-existing module drift-audit report
  (`docs/audits/module-drift-findings.md`, untracked in the working tree at run start). Read-only report,
  not one of the six queue items; 3 Low cosmetic findings left for user triage.
- **Planning done via one Explore mapper** (read-only) — mapped exact integration points for all six items:
  `Home.razor` (unfiltered `Catalog.Modules` cards) + `NavMenu.razor:19–33` (the `AuthorizeView`/`RequiredRole`
  pattern to copy) + `NavItem.RequiredRole` (exists); Catalog reads under `.MapGroup("/catalog")
  .RequireAuthorization()` with a pure pass-through gateway (so anon browse = AllowAnonymous the GETs +
  token-optional `CatalogClient` + drop page `[Authorize]`; `CartService` is `AddScoped`/per-circuit so anon
  cart already works; `POST orders` stays gated = the checkout gate); no payment step exists today;
  `tokens.css` is already dark-ready (all CSS vars); `docs/ARCHITECTURE.md` + 10 ADRs are ASCII-only (no
  mermaid yet). Wrote `QUEUE.md` (items A–F, ordered A→B→C→D→E→F with rationale) + refreshed `STATUS.md`.
- **Queue set up. Next: item A** (role-gate the home app cards). One atomic commit per item; deterministic
  gate only; live/login/visual checks deferred to a supervised pass. E/F are best-effort + flagged.
- `afb89eb` — **Item A (role-gate home cards)** done. **★ SAFE-REVERT-POINT** (last pre-auth-phase commit).
  Added a default `IModule.RequiredRole` (null default, mirroring the existing `Accent` optional member;
  Admin/Reports override → `"admin"`, Storefront inherits null) and wrapped role-gated cards in
  `Home.razor` in `<AuthorizeView Roles="@module.RequiredRole">`, mirroring `NavMenu`. Implementer (high
  confidence), reviewed by the orchestrator directly (proportionate: display-only filter over a pattern
  already Tier-1-verified in Run 1, and the real gate is server-side on the pages). Diff verified: card
  markup byte-for-byte identical, factored into a shared local `RenderFragment` (only refactor); no module
  names hard-coded. Gate: csharpier no-op, build 0W/0E, `dotnet test` 23/23. Anonymous + `testuser` now see
  only the Storefront card; `admin` sees all three. **Live login (testuser/admin/anon) = supervised.**
- `7ab96e5` — **Item B (anonymous storefront + checkout sign-in gate)** done. Two halves: (1) anon browse —
  Catalog GET `/products` + `/categories` now `.AllowAnonymous()` (writes keep `.RequireAuthorization("admin")`);
  `Shop.razor` + `CartPage.razor` drop `[Authorize]`; `CatalogClient` needed no change (the `Authorize`
  extension already guards on empty token). (2) checkout gate — `CartPage` wraps the place-order control in
  `<AuthorizeView>`: anon sees a `Notice` "Sign in to check out" → `/account/login?returnUrl=%2Fstorefront%2Fcart`;
  `POST /storefront/orders` stays `.RequireAuthorization()` (unchanged, the real boundary). New HTTP
  integration tests (`EndpointAuthorizationTests`, `WebApplicationFactory`): anon GET catalog → 200, anon
  POST orders → 401. Implementer (high confidence) + **Tier-1 adversarial review: APPROVE WITH NOTES** —
  reviewer verified vs code: AllowAnonymous overrides the group policy for only the two GETs, writes stay
  admin-only, server checkout gate intact (401 before handler, not a DB false-pass), token guard exists,
  `OrdersPage` still `[Authorize]` (order history not opened), `dotnet list --vulnerable` = none.
  - **Orchestrator caught a gate violation the implementer missed:** adding `Mvc.Testing` transitively
    pulled the **vulnerable `Microsoft.OpenApi` 2.0.0** (NU1903, high sev) → build went 0→2 warnings. The
    implementer wrongly called it "pre-existing"; my baseline was 0 warnings. Root cause: services get
    OpenApi from the ASP.NET Core shared framework (not audited); the test project pulls it as a real NuGet
    transitive (audited). **Fix: pinned `Microsoft.OpenApi` 2.9.0 in the test project** → build 0W/0E again.
  - **Known UX gap (not security) → folded into item C:** `CartService` is `AddScoped` (per-circuit), so the
    anon cart empties across the full-page OIDC sign-in. Item C now also persists the cart to localStorage.
  - **Non-blocking review notes (for the user):** the test harness sets a process-wide env var without
    cleanup (harmless — distinct conn names); `CartPage` hand-writes a `.btn` anchor for the sign-in link
    (renders fine; matches `Shop.razor`'s existing link-as-button pattern).
  - Gate: csharpier no-op, build 0W/0E, `dotnet test` **25/25**. **Live anon→signin→checkout = supervised.**
- `da4abba` — **Item C (payment/checkout + cart persistence)** done. Two parts: (1) **cart persistence** —
  `CartPersistence` bridges the per-circuit `CartService` to `localStorage` (persists only
  `{ProductId,Quantity}`), hydrating on the first interactive render (prerender-guarded, ADR-0010) so the
  cart survives the full-page OIDC sign-in; JS module `cart-storage.js`. (2) **simulated checkout** — new
  `/storefront/checkout` (`[Authorize]`) page: order summary + a `Field`-based card form, client-validated
  (Luhn/future-expiry/CVC); a **mock** `PaymentService` (approve by default; PAN ending `0002` declined —
  Stripe's test-decline number, passes Luhn so it exercises the post-validation decline path) with **no
  real gateway**; on approval it places the order via the unchanged `OrdersClient.CreateAsync` (idempotency
  key + re-entrancy guard preserved from CartPage) → clears cart → confirmation. `CartPage`'s signed-in
  button now routes to checkout instead of placing inline. Implementer (high confidence) + **Tier-1
  adversarial review: APPROVE WITH NOTES.**
  - **Honesty (verified by the reviewer AND the orchestrator via grep):** the PAN/CVC/expiry live only in
    `Checkout.razor` component state, are cleared after auth, and are **never** persisted, logged, or sent
    anywhere but the mock. localStorage holds only product-id+qty; the order carries only ids+qty. **No
    DB/sproc/contract change.**
  - **Reviewer confirmed both load-bearing claims:** (a) no card-data leak; (b) an approved order is placed
    exactly once and a decline places nothing (order placed strictly after a successful auth; key rotated
    only after success; failure keeps cart + same key so a retry can't double-place).
  - **Two repair notes applied before commit** (one repair attempt, per the ladder): (medium) `Checkout.razor`
    now injects `CartPersistence` + hydrates on first render, so a *direct/deep-link* anonymous visit to
    `/storefront/checkout` (→ login → fresh circuit) still restores the cart from localStorage — closes a gap
    in the "cart survives sign-in" guarantee; (low) a null order from `CreateAsync` is now treated as a
    failure (keep cart, same key) instead of confirming a phantom "Order #0".
  - Gate: csharpier no-op, build 0W/0E, `dotnet test` **56/56** (+30 payment unit tests). **Live checkout
    (approve/decline, cart-survives-signin, narrow viewport) = supervised.**
- `6c015d0` — **Item D (architecture + UI-flow diagrams)** done. Replaced the ASCII topology in
  `docs/ARCHITECTURE.md` with a Mermaid container diagram and added `docs/diagrams/` with three flow
  diagrams (auth/token-propagation `sequenceDiagram`; the real end-to-end checkout `flowchart` incl. the
  simulated-payment + `0002` decline path; module-discovery + role-gating `flowchart`) + a README index;
  cross-linked from ADRs 0001/0003/0004/0005/0009. Implementer (high confidence, thorough grep self-check).
  **Accuracy-reviewed by the orchestrator directly** (proportionate for a well-verified docs item — I have
  first-hand context from mapping the platform): confirmed all four blocks are valid GitHub-flavored Mermaid
  (balanced fences, correct diagram-type syntax) and spot-checked the load-bearing edges against code — the
  gateway is pass-through (no auth box), the Storefront→Catalog hop is **direct** (`https+http://catalog`,
  not via the gateway), catalog GET is anonymous, `/storefront/checkout` + `/storefront/orders` exist, and
  `OrderPricing.cs` exists as named. Docs gate: accuracy check passed; build 0W/0E (markdown-only).
- `cbf4fb2` — **Item E (dark mode)** done as **best-effort `[~]` — committed but NOT declared "done"**
  (subjective; needs the user's eye, same handling as Run 1's Dialog polish). Dark palette as **token
  overrides only** (`:root[data-theme="dark"]` in `tokens.css`) + a `prefers-color-scheme` fallback scoped
  to `:root:not([data-theme])` so an explicit choice wins; component CSS untouched (reads the vars). New
  `ThemeToggle` primitive (Ghost `Button`, sun/moon) persists to `localStorage`; wired into the shell
  top-bar. Anti-flash: an inline `<script>` in `App.razor`'s `<head>` applies the saved/system theme before
  first paint; runtime interop is prerender-guarded (`OnAfterRenderAsync`/click only, `JSDisconnected`/
  `InvalidOperation` caught — ADR-0010 pattern). Orchestrator verified the anti-flash script + the media-
  query scoping (both correct) and the gate. Implementer gave AA contrast reasoning. Gate: csharpier no-op,
  build 0W/0E, `dotnet test` 56/56. **Supervised review spots (flagged for the user):** module accent
  monogram chips (esp. Storefront amber `#b45309` on dark — value lives in the module `.cs`, outside
  `tokens.css`), status badges, shadows/elevation, accent-button hover direction. **Live look = supervised.**
- `3ab697a` — **Item F (store images)** done as **best-effort `[~]` — NOT "done"** (real curated/licensed
  photography is a user taste call). New `ProductThumb` primitive in `Atrium.Design`: a deterministic
  **FNV-1a** hash of the product name (not the per-process-randomized `GetHashCode`) picks a design-token
  tint (`--accent`/`--success`/`--warning`/`--danger` at 12–16%) and draws an inline-SVG placeholder
  (initials + hash-positioned motif) — mirrors the Home monogram language, adapts to dark mode (all tokens),
  no external/network assets. Wired into Shop product cards + cart line items. `role="img"` + label.
  **Real-image seam:** an optional `ImageUrl` param renders `<img object-fit:cover>` instead, so a future
  `ProductDto.ImageUrl` is a one-spot swap — the field was intentionally **not** added, and `Atrium.Design`
  gains no `Contracts` dependency (component takes plain `Name`). **No contract/service/DB/sproc change**
  (orchestrator-verified via git status). Implementer (medium-high). Gate: csharpier no-op, build 0W/0E
  (warning count explicitly re-checked), `dotnet test` 56/56. **Supervised:** in-browser look; a reviewer
  may prefer narrowing the decorative palette off `--warning`/`--danger`; swap in real photography.
- **User feedback (live, 2026-07-01, + screenshot):** dark-mode button colors are broken — the accent
  "Save" button is pale/washed with near-invisible text and the ghost "Cancel" label is too faint. Added as
  **item G** (follow-up to E). Held until F committed (both touch `Atrium.Design`). Next up.
- `b332388` — **Item G (dark-mode button/label contrast)** done — **fixed directly by the orchestrator**
  (small, well-scoped token bug; a subagent would add no value). Root cause: `.btn--primary` used
  `background: var(--ink)` with a **hard-coded `color: #fff`** — in dark `--ink` is near-white, so the Save
  button was a near-white block with white (invisible) text. A **scan for hard-coded `#fff`/`#000` in
  `atrium.css`** found the same latent bug in `.btn--accent` (white on the lighter dark accent, ~2:1),
  `.chip--on` (selected filter), and `.toast`. Fixed all four with **theme-aware tokens**: primary/chip/toast
  label → `var(--paper)` (flips with the theme; also keeps every status-toast variant legible since their
  fills invert dark↔bright); accent label → a **new `--on-accent` token** (`#fff` light / `#08211b` dark,
  where the luminous dark accent needs a dark label to clear AA); primary hover → `color-mix(... --ink 85%,
  --paper)` (was a hard `#000` that turned the near-white dark button black). No component CSS forked; **zero
  hard-coded color values remain in `atrium.css`** (verified by grep). Ghost/secondary buttons were checked
  and already correct in both themes. Gate: build 0W/0E, `dotnet test` 56/56. **Marked `[~]`** — the
  invisible-Save correctness bug is fixed by the contrast math, but the overall dark look still wants an
  in-browser eyeball (with E/F). **Live look = supervised.**

### ✅ RUN 2 COMPLETE (2026-07-01)
Queue A–G drained on `feat/storefront-checkout-diagrams`; `main` untouched; gate green throughout. Wound
down cleanly, wrote `GOOD-MORNING.md`. Remaining work is all supervised (live/visual) — see `QUEUE.md`.

---

## Run 3 — support chatbot slice (branch `feat/support-chatbot`, off `main`)

- **Run start (2026-07-01).** Discussion-led plan agreed (spec: `RUN3-SUPPORT-CHATBOT.md`). Azure deploy
  **deferred** (supervised, needs user's account). MTP/xUnit found **already done** (`global.json` runner =
  Microsoft.Testing.Platform; both test projects on `xunit.v3.mtp-v2`; no legacy `Microsoft.NET.Test.Sdk`)
  → ticked in `TODO.md`, dropped from the queue. Queue = **A** (NavMenu visible-vs-loaded count) → **C0–C5**
  (Storefront support-agent slice on MAF/AG-UI). Branch created off `main`.
- **Baseline:** csharpier no-op (71 files), `dotnet build Atrium.slnx` **0W/0E**, `dotnet test` **56/56**
  (unit + integration under MTP; Docker up). Green — cleared to run.
- **Working-tree note:** the user moved `ATRIUM-AI-EXTENSIBILITY-DESIGN.md` root→`docs/` (pure move) and
  added `docs/bugs/CARROTPAD.png` (a stray screenshot) out-of-band during planning. The doc-move is folded
  into the run-setup commit (spec links updated `../../`→`../`); the PNG is left untracked (the user's,
  unrelated to Run 3). No `git add -A` during this run.

### Item A — NavMenu visible-vs-loaded module count (code, Tier-1 auth-adjacent/display-only)
- **Done.** Footer (`nav__foot`) now shows `"{visible} of {loaded} modules visible"` when role-gating
  hides modules, collapsing to the original `"{N} module(s) loaded"` when the user sees all of them.
  `src/Atrium.Portal/Components/Layout/NavMenu.razor` only. Visible count =
  `Catalog.Modules.Count(m => m.RequiredRole is null || user.IsInRole(m.RequiredRole))`, using the
  existing `[CascadingParameter] Task<AuthenticationState>` idiom (same as `MainLayout`/`Home.razor`);
  no hard-coded module names, no new auth plumbing, no CSS.
- **Orchestrator review (light — display-only count, not an enforcement change):** verified the footer
  agrees with the links the nav actually renders for all three personas — anon → "1 of 3", customer
  (`[user,customer]`) → "1 of 3", admin → "3 modules loaded". Confirmed `IModule.RequiredRole` exists
  (default null). **Latent assumption (flagged):** the footer counts by `IModule.RequiredRole` while the
  nav links gate per `NavItem.RequiredRole`; they agree today because each module has one nav item at the
  same role. If a future module contributes multiple nav items at differing roles, revisit.
- **Gate (orchestrator re-ran authoritative):** csharpier check clean (71 files), build **0W/0E**,
  `dotnet test` **56/56**. Implementer confidence: high. **Live check (supervised):** anon/testuser/admin
  each see a correct, non-misleading count.

### Item C0 — MAF + AG-UI spike & pin — **GO** (code, Tier-1 framework go/no-go)
- **GO.** MAF + AG-UI restore, compile, and run an agent turn over a fake `IChatClient` on this .NET 10
  repo. Pinned on `Atrium.Services.Storefront`: `Microsoft.Agents.AI` **1.12.0** (stable) +
  `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` **1.12.0-preview.260629.1** (prerelease; `AddAGUI`/
  `MapAGUI`, wired in C3). `Microsoft.Agents.AI` **1.12.0** also on `Atrium.UnitTests` for the smoke.
  `Microsoft.Extensions.AI` resolves transitively at **10.6.0** (`IChatClient` lives there) — no explicit
  ref, no conflict.
- **Kept smoke (seed for C2):** `tests/Atrium.UnitTests/Support/FakeChatClient.cs` (deterministic
  `IChatClient`, reused by later items) + `tests/Atrium.UnitTests/MafAgentSmokeTests.cs` (builds a
  `ChatClientAgent` over the fake, runs a turn, asserts `.Text`).
- **★ Real 1.12.0 API (docs sketch was wrong — recorded for C1–C5):** `new ChatClientAgent(IChatClient,
  instructions:, name:, tools: IList<AITool>?)` → `AIAgent`; `RunAsync(string,...)` → **`AgentResponse`**
  (`.Text`/`.Messages`); conversation type `AgentSession`. **No** `CreateAIAgent` extension, **no**
  `AgentRunResponse`. Tools via `AIFunctionFactory.Create(...)`.
- **NU1903 (build-integrity) — folded into this item.** C0's fresh restore surfaced a newly-published
  high-severity advisory: `Microsoft.AspNetCore.OpenApi 10.0.9` pulls `Microsoft.OpenApi 2.0.0`
  transitively (GHSA-v5pm-xwqc-g5wc). A clean restore showed it in **3** projects (Catalog + Storefront
  services + UnitTests) — latent repo-wide, only masked at baseline by restore caching. Fixed the same
  way Run 2's item B did: pin `Microsoft.OpenApi` **2.9.0** on the two service projects (UnitTests clears
  transitively). Orchestrator verified: base build was 0W only due to cache; forced restore reproduced
  the 3-project blast radius; the two pins bring a forced-restore build back to **0W**.
- **Gate (orchestrator re-ran authoritative, forced restore):** csharpier clean (73 files), build
  **0W/0E**, `dotnet test` **57/57** (56 + the new MAF smoke). Implementer confidence: high (its
  "NU1903 pre-existing / independent" claim was partly wrong — it IS pre-existing transitively via
  AspNetCore.OpenApi, but the baseline was 0W, so the run must clear it; done).

### Item C1 — `AgentSurface` on `Atrium.Abstractions` (contract, Tier-0, MAF-free)
- **Done.** New `src/Atrium.Abstractions/AgentSurface.cs` — `public sealed record AgentSurface(string
  Name, string Endpoint, string[]? StarterPrompts = null, string? Icon = null)`, mirroring `NavItem`
  (own file, param-style XML docs, nullable optionals). Added default interface member to `IModule`:
  `IEnumerable<AgentSurface> AgentSurfaces => [];` (right after `NavItems`) — so every existing module
  compiles unchanged. **Abstractions stays MAF-free** (orchestrator-verified: csproj references only the
  two `Microsoft.Extensions.*.Abstractions` packages; no `Microsoft.Agents.AI`/`Microsoft.Extensions.AI`).
- **Gate (orchestrator re-ran):** csharpier clean (74 files), build **0W/0E**, `dotnet test` **57/57**.
  Confidence: high. Pure additive contract; nothing to verify live.

### Item C2a — user-scoped "look up one order" data layer (code, Tier-1 security-scoped)
- **Done.** New sproc `usp_Order_GetById.sql` (`@OrderId, @UserName`; same flat header×line projection
  as GetList, `WHERE o.Id=@OrderId AND o.UserName=@UserName`). New `IOrderRepository.GetByIdAsync(int
  orderId, string userName, ct)` → `OrderDto?`; Dapper sproc call, reuses `OrderRow` + an extracted
  `private static GroupRows(...)` helper (so GetOrders + GetById group identically), `.SingleOrDefault()`
  → **null** for both not-found and not-owned (no exists-but-forbidden leak). No status column exists →
  no invented lifecycle; returns the real order only.
- **Security:** the `@UserName` predicate is the boundary — a support agent under the user's bearer can
  only read that user's order. **Orchestrator review (security-scoped):** verified the sproc filters on
  both id+owner and the repo collapses zero-rows to null; the new integration test proves an "intruder"
  user gets null while the owner still reads the same order.
- **Tests (+3, real SQL via Testcontainers):** owner reads (total+lines), unknown id → null, other-user →
  null. **Gate (orchestrator re-ran):** csharpier clean (74), build **0W/0E**, `dotnet test` **60/60**.
  Confidence: high. (C2a is the data half of C2; the tool that wraps it is C2b.)

### Item C2b — SupportAgent + tools + config-driven IChatClient (code, Tier-1 service/runtime) — GO
- **Done.** New `Support/` feature folder in the Storefront service:
  - **Config-driven `IChatClient`** (`SupportAgentServiceCollectionExtensions.AddSupportAgent`): provider
    from `SupportAgent:Provider` — `Fake` (in-service `CannedChatClient`; **Development default** so the
    service boots + gate runs with no AI config), `FoundryLocal`/`AzureFoundry` (shared OpenAI-compatible
    client from `SupportAgent:Endpoint`/`ApiKey`/`Model`, api-key auth — DefaultAzureCredential deferred).
    Non-Development + missing/unknown provider → throws at startup. `IChatClient` singleton; agent+tools scoped.
  - **`SupportTools`** — `GetOrderStatus(int)` (resolves the signed-in user via `IHttpContextAccessor`
    preferred_username, calls C2a's user-scoped `GetByIdAsync`, returns an **honest** "Confirmed. Placed
    {date}, {N} item(s), total {total}" — no invented lifecycle; friendly not-found/not-signed-in msgs);
    `FindProduct(string)` (case-insensitive name filter over the Catalog client, top 5).
  - **`SupportAgent`** — a MAF `ChatClientAgent` over the configured `IChatClient` with both tools via
    `AIFunctionFactory.Create`; instructions explicitly forbid inventing order progress.
  - **`IStorefrontCatalogClient`** extracted (mirrors `IOrderRepository`); typed-client registration +
    `OrdersEndpoints`/`ReportsEndpoints` consumers updated to the interface (makes `FindProduct` testable).
- **Packages (build stayed 0W):** `Microsoft.Extensions.AI.OpenAI` 10.6.0 + `OpenAI` 2.10.0, both stable,
  pinned to match MEAI core 10.6.0 (no advisory; no `Microsoft.OpenApi`-style pin needed for these).
- **Tests (+6):** honest-status format; **user-scoping (recording fake asserts the repo got "alice")**;
  not-found message; FindProduct match + nothing-found; SupportAgent builds over `FakeChatClient` and
  `RunAsync` returns (tools register + agent runs). **Orchestrator review:** read the provider selector,
  tools, agent, Program.cs wiring, canned client, and the tests — all genuine assertions, honest wording,
  correct scoping.
- **Gate (orchestrator re-ran):** csharpier clean (81), build **0W/0E**, `dotnet test` **66/66**.
  Confidence: high. **C2 complete (C2a + C2b).** Live model run = supervised (Foundry Local).

### Item C3 — AG-UI endpoint + step-up MFA policy + tests (code, Tier-1 auth/runtime) — GO
- **Done.** SupportAgent exposed over **AG-UI SSE at `/storefront/agent`**, gated by a config-driven
  **StepUpMfa** policy. Build 0W/0E, **76 tests** (+10). No new packages (MAF/AG-UI already pinned in C0);
  **no gateway change** — the existing `/storefront/{**catch-all}` route already proxies it (YARP forwards SSE).
- **★ Scoped-agent-vs-singleton solution (the subtle bit):** `MapAGUI` resolves its agent ONCE at map
  time from the root provider (decompiled: all 3 overloads collapse to that; a Scoped registration threw
  "cannot resolve scoped from root"). Fix: register the `AIAgent` as a **keyed singleton** via MAF
  hosting's `AddAIAgent(name, factory, Singleton)` (factory depends only on singletons: `IChatClient`,
  `IHttpContextAccessor`); and build each tool with `AIFunctionFactory.Create(method, createInstanceFunc:
  _ => HttpContext.RequestServices.GetRequiredService<SupportTools>())` so **every tool call resolves a
  fresh request-scoped `SupportTools`** — the caller's identity + own `SqlConnection`, concurrency-safe.
  `MapAGUI(AgentName, "/agent")` binds by keyed name; agent `.Name` must match the key (factory asserts).
- **Step-up policy (`StepUpMfa.cs`, config `SupportAgent:StepUp`):** `Enabled` (default false → authenticated
  is enough, local browsing unblocked), `Simulate` (dev escape hatch → authenticated treated as stepped-up),
  else require a real claim — `amr` in `AcceptedAmrValues` (Entra) OR `acr` in `AcceptedAcrValues` (Keycloak),
  both overridable. Policy = `RequireAuthenticatedUser()` + requirement → **anonymous 401**, authenticated-
  but-not-stepped-up **403**. Same policy cloud + local, config only.
- **Tests:** 9 unit cases (`StepUpMfaHandlerTests`) cover the policy logic authoritatively (disabled/
  simulate/amr/acr/missing/override/unauthenticated); 1 integration (`Anonymous_support_agent_request_is_
  rejected` → 401, confirms the endpoint is mapped + non-anonymous). Authenticated-403 left to the unit
  tests (a test auth scheme would be needed for the HTTP path) — flagged for the supervised pass.
- **Orchestrator review:** read `SupportAgent`, `StepUpMfa`, the DI extension, `Program.cs` wiring, and the
  tests — the singleton/scoped split is correct + documented; 401-vs-403 is right; `amr` multivalued
  handling correct. **Fixed one misleading `Program.cs` comment** ("Scoped agent per request" → the agent
  is a keyed singleton; its tools resolve per request). Gate re-run green. Confidence: high.
- **Supervised (live) checks deferred:** real SSE stream through the gateway; step-up 403→200 with a real
  Keycloak ACR / dev-simulate; a live model turn (Foundry Local).
