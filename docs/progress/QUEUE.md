# Work queue

Execution order for tonight/hi: **10 → 2 → 3 → 1 → 6**, then low/tomorrow. `[x]` = done (commit).
`[~]` = supervised / paused at the autonomy boundary. See `README.md` for the gate + boundary.

## Auto-run tonight (docs — commit unattended)

- [ ] **10 · ADR sweep** (doc). Update/add ADRs for decisions made this session:
      - New ADR: **graceful session-expiry handling** (401 → `SessionExpiredException` → `SessionErrorBoundary`).
      - New ADR: **service-root route nesting** (one `/storefront` parent group; features as relative
        subgroups) — or fold into ADR-0007.
      - New ADR: **native-`<dialog>` Dialog primitive** (why `showModal()` over a hand-rolled overlay).
      - Refresh ADR-0007 note about repository interfaces now co-located with their class.
      - Keep the `docs/adr/README.md` index in sync.
- [ ] **2 · "Wire up a new Atrium app" guide** (doc). End-to-end: how to add a new vertical
      (service + module + contracts + gateway route + Aspire wiring + auth) on the platform. This is the
      **source of truth**; item 3 derives from it. Walk the real Storefront/Catalog code as the worked example.
- [ ] **3 · AGENTS.md + authoring skills** (doc). Derive from item 2: an `AGENTS.md` at repo root plus
      focused skills/guardrails for building Modules, Services, Contracts — style rules, the design-system
      reuse rules (see `.claude/skills/atrium-ui`), the data recipe, the auth model, the test gate. Goal:
      bootstrap fast, high-quality new work. Must not contradict item 2 — cross-link, don't duplicate.
- [ ] **1 · README.md per project** (doc). One README per `src/*` project: what it is, its role in the
      topology, key types, how it's run/tested. Short and accurate; link to `docs/ARCHITECTURE.md`.

## Code phase — after ALL docs commit (run unattended; record SAFE-REVERT-POINT first)

Before starting this phase, write the last docs commit hash into `LOG.md` + `STATUS.md` as
`SAFE-REVERT-POINT`. One atomic commit per item. Gate = build-clean + unit/integration green + Playwright
if the stack is up (else commit with a "browser-unverified" note — see `README.md`).

- [ ] **4 · API docs — built-in OpenAPI + Redoc** (code; **decided 2026-07-01**). Enable
      `Microsoft.AspNetCore.OpenApi` per service: `AddOpenApi()` + `MapOpenApi()` → each serves
      `/openapi/v1.json` (makes the existing `.WithTags` meaningful). Then a light **Redoc** page per
      service (Redoc standalone from CDN, `spec-url` → that service's `/openapi/v1.json`). Per-service
      docs, **no Swashbuckle**, no Scalar. Verify the JSON + Redoc render for Catalog and Storefront.
- [ ] **6 · OpenTelemetry spans + structured logging (Serilog)** (code). Add Serilog + OTel tracing
      across services/gateway/portal. Gate = build + unit/integration + confirm traces/logs emit on the
      running stack (or note browser/stack-unverified). Atomic commit.

## Supervised (best-effort unattended, but flag for the user's eye)

- [~] **Browser-verify #1 + Admin modal** (needs stack). Restart stack on the new build; verify the
      session-expired panel on a forced 401, and the modal (open via New/Edit, save both paths,
      Esc/X/Cancel, focus return, narrow viewport). Screenshot per atrium-ui. Log findings; don't
      "fix-and-forget" anything subjective.
- [~] **Dialog frontend-design polish** (subjective). User wants the modal to read as a small, centered,
      "cute" dialog with better spacing. A reversible `frontend-design` CSS pass is OK, but **flag it in
      LOG for the user to eyeball** — don't mark it done.

## Low / tomorrow

- [~] Audit UI for ungraceful scenarios (the #1 session-expiry work is the first instance). (code)
- [~] csharpier: break fluent call chains one-per-line. **Verify feasibility first** — csharpier is
      deliberately low-config; this may not be a supported option. (config)
- [~] `testuser` vs `admin`: determine whether `testuser` mimics an internal user; if so, add a
      **regular-customer** login that cannot see Admin/Reports (Keycloak realm change → volume reset). (code)
- [ ] Update + add ADRs — rolled into item 10 above; keep this as the catch-all for later decisions. (doc)

## Done this session (for context)

- [x] Graceful 401 / session-expiry handling — `843b337`
- [x] Co-locate single-impl repository interfaces — `5ad1f4b`
- [x] Nest Storefront routes under one `/storefront` group — `4872c26`
- [x] Dialog primitive + modal-ise Admin create/edit — `8483066`
- [x] Design spec for the Dialog/modal work — committed under `docs/superpowers/specs/`
