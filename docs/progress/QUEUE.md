# Work queue — Run 2 (storefront / checkout / diagrams)

Execution order: **A → B → C → D → E → F**. `[x]` = done (commit). `[~]` = supervised / best-effort,
flagged for the user. `BLOCKED:` = parked with reason. See `README.md` for the gate + autonomy boundary.

> **Run 1 (2026-07-01 overnight — ADRs, guides, OpenAPI/Redoc, OTel/Serilog, role-gating, UI audit) is
> DONE and merged to `main`.** Its detail is preserved in `LOG.md` (append-only) and `GOOD-MORNING.md`.
> This is a fresh queue on branch `feat/storefront-checkout-diagrams` (off the unmerged
> `fix/modal-center-and-reports-gate`, which carries the reports-gate + centered-modal work).

Ordering rationale: **A** is a small, low-risk quick win that reuses the proven `RequiredRole` pattern.
**B** (anonymous storefront) is the auth-touching dependency for **C** (payment/checkout). **D**
(diagrams) runs *after* B+C so the diagrams reflect the real, finished flow. **E** (dark mode) and **F**
(store images) are the user's "## Last" bucket — more subjective/asset-driven, so they run last as
**best-effort + flag**, mirroring how Run 1 handled the Dialog aesthetic polish.

---

## Auto-run (code + docs — commit unattended, on the run branch)

- [x] **A · Hide app cards on home the user can't access** (code) — `afb89eb`. **SAFE-REVERT-POINT.**
      Default `IModule.RequiredRole` (null; Admin/Reports → `"admin"`) + `<AuthorizeView Roles>` around
      role-gated cards in `Home.razor`, mirroring `NavMenu`. Diff reviewed (card markup byte-identical,
      extracted to a shared `RenderFragment`). Gate green (0W/0E, 23/23). Live login = supervised.
      reuses the shipped pattern). **Gap:** `Home.razor` renders a card per `Catalog.Modules` with **no**
      role filter, so a customer (`testuser` = `[user,customer]`) sees Admin + Reports cards even though
      `/admin` and `/reports` are already role-gated (they'd hit the Forbidden page). `NavMenu.razor`
      (lines 19–33) already solves the identical problem for nav links via `<AuthorizeView Roles>` +
      `NavItem.RequiredRole`. **Plan:** expose the module's required role (add a `RequiredRole` to
      `IModule`, defaulting null; Admin + Reports return `"admin"`, Storefront null — mirror the values
      the modules already put on their single `NavItem`), then in `Home.razor` wrap role-gated cards in
      `<AuthorizeView Roles="@module.RequiredRole">` exactly like `NavMenu`. Anonymous + customer see
      only Storefront; admin sees all three. Do **not** hard-code module names in the shell. Gate = build
      + test green. Live login (testuser vs admin vs anon) = supervised.

- [ ] **B · Storefront visible to anonymous; checkout prompts sign-in** (code, **auth** — Tier-1
      mandatory). Depends on nothing; blocks C. **Two halves:**
      1. **Anonymous browsing.** Today the Catalog reads sit under
         `.MapGroup("/catalog").RequireAuthorization()` (`CatalogEndpoints.cs:16`); the gateway is a pure
         pass-through (no auth of its own). And `Shop.razor` / product pages carry `@attribute
         [Authorize]`, and `CatalogClient` always calls `request.Authorize(tokens)`. **Plan:** let the
         **GET** catalog routes (`/catalog/products`, `/catalog/categories`, product-by-id) serve
         anonymously — `.AllowAnonymous()` on the GETs (keep the `admin` POST/PUT writes gated); make the
         Storefront `CatalogClient` attach the bearer **only when a token is present** (skip `Authorize`
         when anonymous) so reads work signed-out and still carry the token signed-in; drop `[Authorize]`
         from the storefront browse pages (`Shop.razor`, product detail) and the `CartPage`. `CartService`
         is `AddScoped` (per-circuit) so an anonymous cart already works — no change.
      2. **Checkout gate.** The order write (`POST /storefront/orders`) **stays** `.RequireAuthorization()`
         — that's the real gate; never trust the client. On the cart/checkout, when the user is anonymous,
         render a `Notice` ("Sign in or create an account to check out") with a sign-in link
         (`/account/login`, returnUrl back to the cart) **instead of** the place-order/pay button — use
         `<AuthorizeView>` so a signed-in `testuser` sees the real checkout. After sign-in they land back
         on the cart with items intact and can place the order.
      Gate = build + test green (add/adjust a unit or integration test proving anon GET catalog works and
      anon POST orders is rejected). Live pass (anon browse → sign in → checkout) = supervised.

- [ ] **C · Basic payment form / checkout process** (code — Tier-1). Depends on **B**. "Basic, but enough
      to fluff out the diagrams." **Plan (kept honest + contained):** insert a **payment step** between
      cart review and order placement, gated behind sign-in (from B). A `Payment.razor` (or a Dialog)
      using the `Field` primitives collects cardholder name, card number, expiry, CVC + a billing summary;
      validate **client-side only** (Luhn-ish length, expiry in the future, CVC digits) — this is a
      **mock**, it does **not** hit a real processor and **must not** store PAN/CVC anywhere. On "Pay",
      simulate authorization (a `PaymentService` returning an approved/declined result with a fake auth
      reference), then on approval call the existing `OrdersClient.CreateAsync(...)` to place the order and
      route to the confirmation. **Prefer no DB/sproc/contract change**; if recording a payment reference
      on the order adds real value, it must be an **additive-only** migration (new nullable column + new
      sproc param) — otherwise keep payment client-side. Reuse `Notice`/`Button`/`Field`/`Dialog`; no new
      UI libs; no hard-coded colors (atrium-ui). Gate = build + test green. Live checkout = supervised.
      **Flag in LOG:** call out clearly that payment is simulated (no real gateway, no card storage).

- [ ] **D · Architecture + UI-flow diagrams** (docs, mermaid — Tier-1 accuracy). Depends on B+C so the
      checkout/payment flow is real when drawn. **Plan:** add **Mermaid** diagrams that actually explain
      the platform (ARCHITECTURE.md is ASCII-only today). At minimum: (1) a **C4-ish container/topology**
      graph (browser → Portal shell + reflection-discovered modules → YARP gateway → Catalog / Storefront
      services → SQL; Keycloak OIDC/JWT) — fold into `docs/ARCHITECTURE.md`; (2) an **auth sequence**
      (OIDC login, token-in-cookie-claim propagation → typed client → gateway → service JWT — ties to
      ADR-0003/0004); (3) the **checkout UI flow** end-to-end (anon browse → cart → sign-in gate → payment
      form → place order → confirmation — ties to B+C); (4) a **module-discovery** view (how `IModule`s are
      found by reflection and surfaced as cards/nav, role-gated — ties to A). Put the flow diagrams under
      `docs/diagrams/` and link them from ARCHITECTURE.md + relevant ADRs. Docs gate = build-clean
      (n/a) + **accuracy**: every node/edge/arrow grep-verified against real files/types/routes. Tier-1
      because it's the showcase "explain the platform" deliverable.

## "## Last" bucket — best-effort + flag (subjective / asset-driven)

- [ ] **E · Dark mode** (code/CSS — best-effort, supervised look). `tokens.css` is already dark-ready
      (every color is a CSS var; zero hard-coded values). **Plan:** add a `:root[data-theme="dark"]` (and
      an `@media (prefers-color-scheme: dark)` fallback) block overriding the color tokens (`--paper`,
      `--surface`, `--ink`, `--muted`, `--accent`, status colors) with a considered dark palette — do
      **not** touch component CSS (they read the vars). Add a small theme-toggle primitive in
      `Atrium.Design` (sun/moon) wired into the shell top-bar, persisting the choice to `localStorage` via
      a tiny JS-interop helper and applying `data-theme` on load (guard `IJSRuntime` for prerender per
      ADR-0010's interop lessons). Gate = build + test green. **Subjective → flag for the user's eye;
      don't declare "done," mark `[~]`.**

- [ ] **F · Find store images** (assets — best-effort, likely needs the user). Storefront products render
      without imagery. **Honest constraint:** I can't unattended-browse and download real product photos
      (licensing + taste). **Plan (best-effort):** add tasteful, self-contained **generated SVG
      placeholders** keyed deterministically by product/category (e.g. an initial + accent-tinted panel
      from the design tokens) so the storefront isn't image-less, wired through the existing product data
      + `Atrium.Design`. Then **flag** that curated, licensed photography is a user/taste call. If it can't
      be done cleanly without real assets, mark `BLOCKED: needs curated licensed images` rather than
      shipping something off-brand. Gate = build + test green. Mark `[~]`.

---

## Supervised pass (live / browser / subjective — NOT the auto-loop)

Bring the stack up (`cd src/Atrium.AppHost && aspire run`) with the user, then:

- [~] **A live** — anon + `testuser` see only the Storefront card on home; `admin` sees all three.
- [~] **B live** — anon can browse Shop + product + cart; the checkout shows the sign-in Notice; after
      signing in as `testuser` the cart survives and checkout proceeds; anon `POST orders` is rejected.
- [~] **C live** — the payment form validates, "declined" and "approved" paths both behave, an approved
      payment places the order and lands on confirmation. Confirm no PAN/CVC is persisted or logged.
- [~] **D** — diagrams render (GitHub/mermaid) and read correctly.
- [~] **E** — dark mode looks right across storefront/admin/reports; toggle persists across reloads.
- [~] **F** — image placeholders look on-brand, or the user supplies real images.
