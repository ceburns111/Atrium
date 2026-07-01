# Run log (append-only)

## 2026-07-01 — interactive session (Opus 4.8)

- `843b337` — Graceful 401 / session-expiry handling: `SessionExpiredException` +
  `ThrowIfSessionExpired`, all four typed clients map 401, `SessionErrorBoundary` in the shell. Unit 18/18.
- `5ad1f4b` — Co-located single-impl repository interfaces into their class files (Catalog/Order/Report).
- `4872c26` — Nested Storefront routes under one `/storefront` parent group; features as relative subgroups.
- `8483066` — Added `Dialog` primitive (native `<dialog>`/`showModal`) and modal-ised Admin create/edit;
  removed the create-card + inline-edit row + dead CSS.
- Also: committed the Dialog/modal design spec; a csharpier formatting nit in CatalogEndpoints.
- Set up this `docs/progress/` system + `atrium-overnight-run` memory for resumable work.
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
  New `docs/progress/SKILL-REVIEW.md` ledger tracks every auto-authored skill for morning keep/discard.
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
