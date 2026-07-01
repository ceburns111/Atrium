# TODO

Status of the 2026-07-01 overnight run + what's left. That run is **merged into `main`** (`14613f6`).
Full detail: `docs/progress/` (STATUS / QUEUE / LOG) and `GOOD-MORNING.md`.

## ✅ Done & merged (overnight 2026-07-01)

- README.md in every `src/*` and `tests/*` project
- "Wire up a new Atrium app" guide — `docs/guides/wire-up-a-new-app.md` (source of truth)
- Root `AGENTS.md` + 3 authoring skills (`atrium-service` / `atrium-module` / `atrium-contracts`) — kept
- API docs: built-in **OpenAPI + Redoc** per service (chose this over Scalar/Swashbuckle)
- Co-located single-impl repository interfaces with their class
- **OTel spans + Serilog** structured logging (`Atrium.ServiceDefaults`, all hosts)
- Actual application logging statements at real seams (repos, HTTP clients, endpoints, DbUp, error boundary)
- ADRs 0008 (session-expiry) / 0009 (route nesting) / 0010 (Dialog); refreshed 0004 & 0007
- Role-gate Admin + Reports to the `admin` role + Forbidden page (`testuser` = customer, `admin` = admin)
- csharpier one-call-per-line — **closed, not feasible** in csharpier 1.3.0 (width-driven, non-configurable)
- Verify live pass (2026-07-01): OpenAPI/Redoc render, traces span portal→gateway→storefront→catalog
  with SQL spans, role gating (`testuser` vs `admin`)
- UI audit Medium+Low (M1–M4, L1–L5) — inline error+retry, Notice card for Forbidden/NotFound,
  Admin validation + re-entrancy guard, Dialog JSDisconnectedException guard

## ✅ Done this session (2026-07-01, uncommitted → committing now)

- **Reports admin-gate** — `/storefront/reports` now `.RequireAuthorization("admin")`. Storefront's JWT
  config gained `MapInboundClaims = false` + `RoleClaimType = "role"` (both were missing; without them
  `RequireRole` silently 403s everyone) and an `"admin"` policy — mirrors Catalog's live-verified gate.
  Orders/cart stay auth-only.
- **Dialog centered** — root cause: the global `* { margin: 0 }` reset zeroes the UA `margin: auto` a
  native modal `<dialog>` uses to center, pinning it top-left. Fix: explicit `position: fixed;
  inset: 0; margin: auto`. (An `@starting-style` entrance animation was tried and **reverted** — it
  glitched against Blazor re-renders, squishing the modal.) Verified centered live.

##  Manual Test Required
- [ ] Session-expired panel (force a 401) + Admin modal open/save/Esc — **NOT driven** in headless
      Playwright (interactive circuit + self-signed cert). Needs a real browser or an E2E harness.

## Clean Up 
- [ ] Implement cleanup findings (dead code, overeingineering, ui module slop and inconsistency)
- [ ] Review for code drit
- [ ] Update project specific tools/skills

## 🆕 New work
- [ ] Payment form
- [ ] Architecture diagrams (explain the platform properly)
- [ ] Dark mode
- [ ] Find store images

## ❓ Open question
- [ ] Document my own agentic coding workflow (the overnight tool), or bring it up for discussion?
