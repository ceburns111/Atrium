# Work queue

Execution order for tonight/hi: **10 → 2 → 3 → 1 → 6**, then low/tomorrow. `[x]` = done (commit).
`[~]` = supervised / paused at the autonomy boundary. See `README.md` for the gate + boundary.

## Auto-run tonight (docs — commit unattended)

- [x] **10 · ADR sweep** (doc) — `ee63214`. Update/add ADRs for decisions made this session:
      - New ADR: **graceful session-expiry handling** (401 → `SessionExpiredException` → `SessionErrorBoundary`).
      - New ADR: **service-root route nesting** (one `/storefront` parent group; features as relative
        subgroups) — or fold into ADR-0007.
      - New ADR: **native-`<dialog>` Dialog primitive** (why `showModal()` over a hand-rolled overlay).
      - Refresh ADR-0007 note about repository interfaces now co-located with their class.
      - Keep the `docs/adr/README.md` index in sync.
- [x] **2 · "Wire up a new Atrium app" guide** (doc) — `14538c0` (`docs/guides/wire-up-a-new-app.md`). End-to-end: how to add a new vertical
      (service + module + contracts + gateway route + Aspire wiring + auth) on the platform. This is the
      **source of truth**; item 3 derives from it. Walk the real Storefront/Catalog code as the worked example.
- [x] **3 · AGENTS.md + authoring skills** (doc) — `f30e9ae`. Root `AGENTS.md` + 3 real skills (`atrium-service`/`atrium-module`/`atrium-contracts`), logged in `SKILL-REVIEW.md` for morning keep/discard. Derive from item 2: an `AGENTS.md` at repo root plus
      focused skills/guardrails for building Modules, Services, Contracts — style rules, the design-system
      reuse rules (see `.claude/skills/atrium-ui`), the data recipe, the auth model, the test gate. Goal:
      bootstrap fast, high-quality new work. Must not contradict item 2 — cross-link, don't duplicate.
- [x] **1 · README.md per project** (doc) — `a25c62f`. 13 READMEs (11 src/ + 2 tests/). **SAFE-REVERT-POINT.** One README per `src/*` project: what it is, its role in the
      topology, key types, how it's run/tested. Short and accurate; link to `docs/ARCHITECTURE.md`.

## Code phase — after ALL docs commit (run unattended; record SAFE-REVERT-POINT first)

Before starting this phase, write the last docs commit hash into `LOG.md` + `STATUS.md` as
`SAFE-REVERT-POINT`. One atomic commit per item. Gate = build-clean + unit/integration green + Playwright
if the stack is up (else commit with a "browser-unverified" note — see `README.md`).

- [x] **4 · API docs — built-in OpenAPI + Redoc** (code) — `bc7afd7`. Tier-1 APPROVE WITH NOTES; unit/integration green; live-check deferred to morning. Enable
      `Microsoft.AspNetCore.OpenApi` per service: `AddOpenApi()` + `MapOpenApi()` → each serves
      `/openapi/v1.json` (makes the existing `.WithTags` meaningful). Then a light **Redoc** page per
      service (Redoc standalone from CDN, `spec-url` → that service's `/openapi/v1.json`). Per-service
      docs, **no Swashbuckle**, no Scalar. Verify the JSON + Redoc render for Catalog and Storefront.
- [x] **7 · Application logging** (code) — `153b6bc`. Tier-1 APPROVE WITH NOTES (added the flagged
      `StorefrontCatalogClient`); build+test green; live emission = morning check.
      **added 2026-07-01 by user.** Repo had ~zero deliberate logging
      (1 injected `ILogger`, 0 `Log*()` calls); item 6 added the Serilog/OTel pipeline but nothing writes to
      it. Add purposeful **structured** `ILogger<T>` logging at real seams: repository ops (esp. failures /
      sproc `THROW`), the typed HTTP clients (401/session-expiry + non-success), endpoint business events
      (order placed, product created/updated), DbUp migration runs, the error boundary. Correct levels,
      message templates with named properties, **no tokens/PII, no per-item noise**. Gate = build +
      unit/integration green; live emission is a morning check. Deterministic → runs unattended.
- [x] **6 · OpenTelemetry spans + structured logging (Serilog)** (code) — `d51a902`. Shared `Atrium.ServiceDefaults` project; Tier-1 APPROVE WITH NOTES (one repair applied: restore `Microsoft.AspNetCore→Warning`); unit/integration green; live-check deferred. Add Serilog + OTel tracing
      across services/gateway/portal. Gate = build + unit/integration + confirm traces/logs emit on the
      running stack (or note browser/stack-unverified). Atomic commit.

## Supervised morning pass (one live pass, with the user — NOT the overnight loop)

All live/browser/trace verification is consolidated here so the overnight loop never drives the stack.
Bring the stack up on the new build (`cd src/Atrium.AppHost && aspire run`), then:

- [~] **#1 session-expired panel** — force a 401 (drop Keycloak accessTokenLifespan or wait out expiry);
      confirm the panel renders instead of the crash overlay.
- [~] **Admin modal** — open via New product and Edit; save both paths persist; Esc / X / Cancel each
      close; focus returns to the trigger; narrow viewport. Screenshot per atrium-ui.
- [~] **Dialog "cute"/spacing polish** — subjective; `frontend-design` pass, user eyeballs it.
- [~] **Item 4 live** — `/openapi/v1.json` + Redoc render for Catalog and Storefront.
- [~] **Item 6 live** — traces/logs actually emit across services/gateway/portal.

## Low / tomorrow

- [~] Audit UI for ungraceful scenarios (the #1 session-expiry work is the first instance). (code)
- [x] csharpier: break fluent call chains one-per-line — **NOT FEASIBLE (verified 2026-07-01, csharpier
      1.3.0).** CSharpier's chain breaking is width-driven and non-configurable: a short chain stays inline,
      a long one breaks by its own heuristics (not uniformly one-per-line), and unknown config keys are
      silently ignored — only `printWidth`/`tabWidth`/`useTabs`/`endOfLine` are honored. No option forces
      one-call-per-line. Closing the item; the only lever (`printWidth`) would affect all formatting, not
      just chains. No code change.
- [x] **9 · Clean Forbidden view for authenticated wrong-role users** (code) — `7c38f53`. Branched shell
      `NotAuthorized` (authenticated → `Forbidden` page mirroring `NotFound`; unauthenticated →
      `RedirectToLogin`). Self-reviewed (low-risk); build+test green; live-confirm the bounce is gone
      (morning). Spun out of item-8 review, 2026-07-01.
- [x] **8 · Role-gate Admin/Reports from customers** (code) — `a3a366d`. Page `[Authorize(Roles="admin")]`
      + role-aware `NavItem` + shell `AuthorizeView` filter; Tier-1 APPROVE (cascading auth state verified);
      build+test green; needs live testuser-vs-admin login (morning). Two follow-ups spun out: item 9
      (Forbidden view) + server-side Reports gating (noted). Refined from the `testuser`-vs-`admin` item,
      2026-07-01). **Investigation done (read-only):** the realm ALREADY defines roles `user`/`customer`/
      `admin`; **`testuser` is a customer** (`["user","customer"]`), `admin` is `["user","admin"]` — both
      personas exist, so **NO realm change / volume reset is needed** (corrects the original assumption).
      **The real gap:** `Products.razor` (Admin) and `Dashboard.razor` (Reports) use bare
      `@attribute [Authorize]` (auth only, not role), and `NavItem` is role-unaware — so a customer sees
      the Admin/Reports nav + can open those pages. Portal wiring supports role checks
      (`RoleClaimType = "role"`, Program.cs:63), so `[Authorize(Roles="admin")]` will work. **Plan:**
      (1) `@attribute [Authorize(Roles = "admin")]` on the Admin + Reports pages; (2) make nav role-aware —
      add optional `RequiredRole` to `NavItem` (`Atrium.Abstractions`) + filter in the shell via
      `<AuthorizeView>`; (3) confirm the Reports read endpoint is admin-gated server-side too. Build+test
      verifiable; **needs a live login (testuser vs admin) in the morning to confirm the gating behaves.**
      Tier-1 (auth). Hold until item 7 commits (shared files).
- [ ] Update + add ADRs — rolled into item 10 above; keep this as the catch-all for later decisions. (doc)

## Done this session (for context)

- [x] Graceful 401 / session-expiry handling — `843b337`
- [x] Co-locate single-impl repository interfaces — `5ad1f4b`
- [x] Nest Storefront routes under one `/storefront` group — `4872c26`
- [x] Dialog primitive + modal-ise Admin create/edit — `8483066`
- [x] Design spec for the Dialog/modal work — committed under `docs/superpowers/specs/`
