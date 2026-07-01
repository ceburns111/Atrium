# Run 3 spec — support chatbot slice (+ NavMenu count, MTP close-out)

**Planned:** 2026-07-01 (discussion-led, per `STATUS.md`). **Not started.**
**Branch:** off `main` (Run 2 is merged; `main` is the clean base). Suggested: `feat/support-chatbot`.
**Gate + escalation + execution model:** `README.md` (thin orchestrator, one implementer subagent per
item, deterministic gate, Failure protocol). This file is the **queue + locked design**; `README.md` is
the **how**. `STATUS.md` stays the live source of truth once the run starts.

This run came out of a design discussion (not paste-and-run). The Azure-deploy TODO item was **explicitly
deferred** (see below) — it is **not** in this run.

---

## Locked decisions (from the 2026-07-01 discussion)

**Chatbot — build shape**
- **Slice-first, on MAF/AG-UI (mandatory).** Build ONE working **Storefront support agent** on
  **Microsoft Agent Framework + AG-UI**, wired directly into the Storefront service/module. Do **not**
  build the generalized `Atrium.AI` convention layer (`AddChatAgent<>()`, zero-host-edits) yet — but
  shape the slice so that layer can be extracted later. The full design is
  `../ATRIUM-AI-EXTENSIBILITY-DESIGN.md`; this run implements a **v1 slice** of it.
- **Agent runtime lives in `Atrium.Services.Storefront`** (co-located with order data + the existing
  Catalog client), exposed over **AG-UI SSE** through the gateway. Mirrors the design doc's decision A.
- **Tools over real data:** `GetOrderStatus` (Storefront's own order DB) and **product lookup** (reuse
  the Storefront→Catalog client that already exists). Plain `[Description]` C# methods.
- **Chat surface** = the design doc's UI seam: `IModule.AgentSurfaces` (new, on `Atrium.Abstractions`,
  MAF-free) + a shared `<AgentChat>` primitive in `Atrium.Design` wrapping the AG-UI .NET client; the
  Portal shell renders a context-aware launcher from discovered surfaces (parallel to `NavItems`).

**Chatbot — model client (config-driven `IChatClient`, provider swap = config only)**
- `tests → fake IChatClient` (deterministic; drives the whole gate).
- `dev → Foundry Local` (on-brand with the cloud target; supervised live pass only).
- `cloud → Azure AI Foundry` (deferred with the Azure work).

**Chatbot — MFA = step-up (not just "app has login")**
- A **feature-flagged `RequireStepUpMfa` authorization policy** guards the agent endpoint AND gates the
  launcher. It checks an `amr`/`acr` **step-up claim**.
  - **Cloud** → Entra `amr` (arrives with the deferred Azure/Entra work).
  - **Local now** → Keycloak ACR step-up, with a **dev-simulate flag** so it is testable without a live
    step-up ceremony. Same policy, environment config only.
- If the session lacks step-up, `<AgentChat>` shows a "verify to continue" prompt — it does not error.

**Identity (context for the MFA design; the migration itself is deferred)**
- Agreed target: **Entra External ID in the cloud, Keycloak local, provider config-driven per
  environment**, plus a small **claim-normalization shim** (Keycloak `role` realm-roles ↔ Entra
  `roles`/app-roles) so the `admin`/`customer` policies keep working unchanged. **None of this is built
  in Run 3** — the chatbot's step-up policy is written config-driven so it drops onto Entra later with no
  code change.

---

## Deferred — NOT in this run

- **Azure deploy (whole TODO item).** User's call, 2026-07-01: skip for this unattended run. When it
  runs it will be a **supervised** effort (needs the user's Azure account/creds/cost setup — not
  unattended-safe). Agreed direction for when it happens, captured so it isn't re-litigated:
  - **Compute:** Azure Container Apps via **`azd`** (generates Bicep from the Aspire model) — scale-to-zero
    + **Azure SQL serverless** (auto-pause) → near-$0 idle; one resource-group teardown (`azd down` /
    `az group delete`). ACA chosen over AKS (always-on node pool = most expensive/most ops) and App
    Service (no true scale-to-zero).
  - **Identity:** the Entra/Keycloak split above.
  - **Local IaC testing (open question answered):** no full local ACA emulator; **Aspire itself is the
    local integration environment**; pre-apply validation via `bicep build` + `az deployment group
    what-if` + `azd provision --preview`.
  - **CI/CD (the "MAYBE"):** `azd pipeline config` → GitHub Actions with OIDC federated auth (no stored
    secrets); the "deploy a feature slice" demo maps onto a PR touching one module + one service.
  - The Keycloak→Entra swap removes the always-on Keycloak+Postgres cost floor and makes the chatbot's
    MFA step-up free (Entra Conditional Access).

---

## Queue — execution order: **A → C0 … C5**

`[x]` = done (commit) · `[~]` = supervised/best-effort, flagged · `BLOCKED:` = parked with reason.

> **Note:** the TODO's "Integrate Microsoft Test Platform w/ xUnit" is **already done** and is NOT a queue
> item — verified 2026-07-01 (`global.json` runner = MTP, both test projects on `xunit.v3.mtp-v2`, no
> legacy `Microsoft.NET.Test.Sdk`). Tick it in `TODO.md`; no work.

### A · NavMenu "loaded vs visible" module count — Tier-1 (small, deterministic)
- [ ] **Problem.** `NavMenu.razor`'s `nav__foot` shows `@Catalog.Modules.Count module(s) loaded`
  (e.g. "3 modules loaded") even when a customer/anon only *sees* 1 module in the left-nav — misleading.
- **Plan.** Compute the count the current user can actually **see** (the same role filter already applied
  to the nav links: `NavItem.RequiredRole` / `IModule.RequiredRole` via `<AuthorizeView Roles>`, per the
  Run-2 role-gating pattern), and show visible-vs-loaded when they differ — e.g. `1 of 3 modules
  visible` (exact wording at implementer discretion; must not mislead). When all loaded modules are
  visible (admin), the "of N" clause may collapse to the existing phrasing. **Do not hard-code module
  names**; derive from the same gating the nav already uses. Evaluate role state via the existing
  `AuthenticationStateProvider`/`AuthorizeView` mechanism the shell already uses — no new auth plumbing.
- **Gate.** csharpier + build + `dotnet test` green. **Live check (supervised):** anon / testuser
  (`[user,customer]`) / admin each see a correct, non-misleading count.

### C · Storefront support agent (MAF/AG-UI slice) — Tier-1, phased C0 → C5

> **★ Autonomy guardrail (MAF/AG-UI are PREVIEW).** **C0 is the go/no-go.** If the exact MAF + AG-UI
> .NET packages can't be resolved/restored or their API has drifted such that a clean wiring isn't
> achievable, **escalate `BLOCKED` for supervised review — do NOT silently substitute plain MEAI.** The
> user made MAF/AG-UI mandatory; a fallback framework is a supervised decision, not an unattended one.
> Prefer landing C0–C2 (which are the most framework-sensitive) as separate commits so a later blocker
> doesn't strand a giant uncommitted change.

- [ ] **C0 · Spike & pin the stack — go/no-go.** Resolve the **exact** MAF (Microsoft Agent Framework)
  and **AG-UI .NET** package IDs + versions (both are preview/fast-moving — do not assume names). Add a
  throwaway smoke that constructs an `AIAgent`/`ChatClientAgent` over a **fake `IChatClient`** and, if
  feasible, hosts a trivial AG-UI SSE endpoint. **Deliverable:** the pinned package refs + a one-paragraph
  `LOG.md` note recording the resolved IDs/versions and any API-shape surprises. **If not feasible →
  `BLOCKED:` with the specific failure.** Gate: build green (test optional at this step).
- [ ] **C1 · `AgentSurface` on `Atrium.Abstractions`.** Add the `AgentSurface` record next to `NavItem`
  and `IEnumerable<AgentSurface> IModule.AgentSurfaces => []` (default empty). **MAF-free** — UI modules
  must declare a surface without depending on the agent framework. Fields per the design doc:
  `Name`, `Endpoint` (gateway-relative, e.g. `/storefront/agent`), `StarterPrompts?`, `Icon?`.
  Gate: build + test green (pure additive contract; no behavior).
- [ ] **C2 · `SupportAgent` + tools + config-driven `IChatClient` (Storefront service).** Author the MAF
  agent as idiomatic MAF with two `[Description]` tool methods:
  `GetOrderStatus(orderId)` (Storefront order DB — add a get-status/get-by-id sproc + repo method if one
  doesn't exist, following the existing DbUp two-lane + Dapper + repository-interface-with-integration-test
  convention) and a **product lookup** (reuse the existing Storefront→Catalog client). Register
  `IChatClient` from config with three providers: **fake** (tests/default-when-unconfigured), **Foundry
  Local** (dev), **Azure AI Foundry** (cloud) — selection is config only. **Unit-test the tools directly**
  (plain methods) + the config resolution. Gate: build + `dotnet test` green.
- [ ] **C3 · AG-UI endpoint + gateway route + step-up MFA policy + tests.** Map the agent over AG-UI SSE
  at the service's `/agent`; add the gateway route `/storefront/agent/{**catch-all}` → storefront
  (mirror the existing `/storefront/*` cluster in `Atrium.Gateway/appsettings.json`). Add the
  feature-flagged `RequireStepUpMfa` policy (checks `amr`/`acr`; Keycloak-ACR + dev-simulate locally,
  Entra-ready) and apply it to the endpoint. **Integration test** (fake `IChatClient`, mirroring the
  `Atrium.Services.*.Tests` shape): asserts the SSE event stream for a simple prompt AND that the step-up
  policy blocks a non-stepped-up caller / permits a stepped-up one. Gate: build + `dotnet test` green
  (**integration needs Docker**). **Live model/MFA run deferred to supervised pass.**
- [ ] **C4 · `<AgentChat>` primitive (`Atrium.Design`).** A reusable component wrapping the **AG-UI .NET
  client**, reusing the module gateway + bearer pattern and the existing `ThrowIfSessionExpired()`
  convention (surface AG-UI error events + session expiry the standard way). Renders text + tool cards
  (design-doc v1 scope — no generative UI beyond that). **Pull from Atrium.Design tokens/primitives — no
  ad-hoc CSS** (per the `atrium-ui` skill). Also usable inline on a module page. Gate: build + test green.
- [ ] **C5 · Storefront module surface + shell launcher.** `Atrium.Modules.Storefront` declares an
  `AgentSurface("Order Support", "/storefront/agent", StarterPrompts: […])`. The **Portal shell** reads
  `AgentSurfaces` from every discovered module and renders a context-aware assistant launcher (app-bar
  entry targeting the active module's agent), gated by the same step-up state — mirroring how the shell
  already renders `NavItems`. Gate: build + test green. **Live click-through (launcher → chat → tool
  answer) = supervised pass.**

---

## Gate (authoritative; orchestrator runs before every commit)

Per `README.md`: `dotnet csharpier format .` → `dotnet build Atrium.slnx -v q` (**0W/0E**) → for code
items `dotnet test Atrium.slnx` (all green; **integration needs Docker**; Aspire stack NOT required).
**Deterministic only — no unattended browser/Aspire/live-model.** Commit atomically, one item per commit,
`Co-Authored-By` trailer, with a "live-verification deferred to supervised pass" body line naming what to
check.

## Escalation specific to this run

- **MAF/AG-UI unavailable / drifted** → `BLOCKED:` (see C0 guardrail). No MEAI substitution unattended.
- Everything else → the standard Failure protocol / escalation ladder in `README.md`.

## Deliverables

- The working slice under the green deterministic gate (A, B tick, C0–C5), atomic commits on the run
  branch; `main` untouched.
- A **`verification/` playbook** for the supervised live pass: bring up Foundry Local, run the stack,
  drive Portal → launcher → Order Support → ask "status of order #NNNN" and a product question, and
  exercise the step-up gate (blocked before step-up, permitted after / dev-simulate). Mirrors Run 2's
  Playwright verification approach.

## Baseline to establish at run start

Create the branch off `main`; confirm `dotnet build` 0W/0E, `dotnet test` green (record the count),
Docker up, csharpier no-op. Record the start commit in `LOG.md`. Green = cleared to run A → C5.
