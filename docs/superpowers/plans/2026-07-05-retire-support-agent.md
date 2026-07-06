# Retire the Support Agent Slice — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the AI Support agent slice from `main` entirely, preserve it on `feat/support-agent`, and record the decision + future modular-agent direction in ADR-0013.

**Architecture:** Pure subtraction along already-clean seams: the backend lives in one folder (`src/Atrium.Services.Storefront/Support/`) wired by three `Program.cs` calls; the client side is a handful of named files in `Atrium.Design`/`Atrium.Portal`; contracts/abstractions contribute one type each. Order of removal keeps every commit green: evals project → backend → client → AppHost → docs.

**Tech Stack:** .NET 10, Aspire 13.4, xUnit v3 on Microsoft.Testing.Platform, Playwright (live smoke).

**Spec:** `docs/superpowers/specs/2026-07-05-retire-support-agent-design.md` — read it first.

## Global Constraints

- **Run mechanics (archived runbook `docs/archive/runs/README.md`):** work on a dedicated run branch, never `main`; one atomic commit per task; the orchestrator re-runs the gate itself before every commit (never trust a subagent's word); max 2 attempts per task then revert-to-green + mark BLOCKED + skip; halt after 2 consecutive BLOCKED.
- **The gate (Lane A, before every commit):**
  ```bash
  dotnet csharpier format . && dotnet build Atrium.slnx -v q   # expect 0 warnings / 0 errors
  dotnet test Atrium.slnx                                       # Docker must be running
  ```
- **Test filters** use MTP syntax: `dotnet test tests/Atrium.UnitTests -- --filter-class "*ClassName"` (never VSTest `--filter`).
- **Do NOT touch:** `docs/archive/**`, `docs/audits/**`, `docs/superpowers/specs/2026-07-02-ai-chat-enhancements-design.md`, `docs/adr/0011-*.md` body (status line only). History stays.
- **Live URLs (Lane B):** Portal https://localhost:7001 (fallback http://localhost:5035), Gateway https://localhost:7271, Keycloak http://localhost:8080, Catalog health http://localhost:5260/health, Storefront health http://localhost:5109/health. Users: `testuser` / `password` (roles user+customer), `admin` / `password` (roles user+admin).
- **Ollama must NOT be running** during Lane B — proving nothing needs it is part of the test.
- Screenshots/logs go to a gitignored `artifacts/` directory at repo root (already in `.gitignore`).

---

### Task 0: Run setup + preservation branch

**Files:** none (git only)

**Interfaces:**
- Produces: run branch `run/retire-support-agent`; preservation branch `feat/support-agent` pushed to origin. All later tasks commit to the run branch.

- [ ] **Step 1: Verify preconditions**

```bash
docker info > /dev/null && echo DOCKER-OK
git -C /Users/ted/code/Atrium status --porcelain   # expect empty; if dirty, STOP and report
git fetch origin && git checkout main && git pull
```

- [ ] **Step 2: Create the preservation branch at pre-removal state and push it**

```bash
git branch feat/support-agent main
git push -u origin feat/support-agent
```

- [ ] **Step 3: Create the run branch**

```bash
git checkout -b run/retire-support-agent main
```

- [ ] **Step 4: Baseline gate** — run the Lane A gate (Global Constraints). Expected: 0 warnings / 0 errors, all tests pass. If the baseline is red, STOP: the run cannot start from red.

---

### Task 1: Remove the Evals project

Evals references the Storefront project (`InternalsVisibleTo`), so it must leave the solution **before** the `Support/` folder is deleted or nothing compiles.

**Files:**
- Modify: `Atrium.slnx` (remove one line)
- Modify: `src/Atrium.Services.Storefront/Atrium.Services.Storefront.csproj` (remove `InternalsVisibleTo`)
- Delete: `tests/Atrium.Evals/` (entire directory)

- [ ] **Step 1: Remove the project from the solution.** In `Atrium.slnx`, delete this line from the `/tests/` folder:

```xml
    <Project Path="tests/Atrium.Evals/Atrium.Evals.csproj" />
```

- [ ] **Step 2: Delete the project directory**

```bash
git rm -r tests/Atrium.Evals
```

- [ ] **Step 3: Remove the InternalsVisibleTo.** In `src/Atrium.Services.Storefront/Atrium.Services.Storefront.csproj`, delete these lines (keep the `Atrium.UnitTests` one):

```xml
    <!-- The eval harness composes the REAL agent brain (system prompt + pipeline seams) so scores
         certify what is deployed, not a hand-copied approximation. -->
    <InternalsVisibleTo Include="Atrium.Evals" />
```

- [ ] **Step 4: Gate** — run the Lane A gate. Expected: green (nothing else referenced Evals).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "chore(evals): remove the Atrium.Evals project (agent slice retiring, ADR-0013)"
```

---

### Task 2: Remove the backend agent slice (Storefront service)

**Files:**
- Delete: `src/Atrium.Services.Storefront/Support/` (all 10 files)
- Modify: `src/Atrium.Services.Storefront/Program.cs`
- Modify: `src/Atrium.Services.Storefront/Atrium.Services.Storefront.csproj`
- Delete: `tests/Atrium.UnitTests/Support/` (all 9 files) and `tests/Atrium.UnitTests/MafAgentSmokeTests.cs`

**Interfaces:**
- Produces: a Storefront service with orders + reports only; `builder.AddAtriumJwtAuth();` with no `.AddPolicy` chain.

- [ ] **Step 1: Delete the folders/files**

```bash
git rm -r src/Atrium.Services.Storefront/Support
git rm -r tests/Atrium.UnitTests/Support
git rm tests/Atrium.UnitTests/MafAgentSmokeTests.cs
```

- [ ] **Step 2: Edit `Program.cs`.** Remove all of the following (current line numbers as of this writing):

1. Line 5: `using Atrium.Services.Storefront.Support;`
2. Lines 6–7: `using OpenTelemetry.Metrics;` and `using OpenTelemetry.Trace;` (only used by the blocks removed next — verify with a build that they're now unused).
3. Lines 15–23 — the GenAI telemetry blocks, including the comment:

```csharp
// GenAI spans: the chat-client pipeline (tokens/model) + the MAF agent (turns/tools) + feedback (Phase 4).
builder.Services.ConfigureOpenTelemetryTracerProvider(t =>
    t.AddSource(SupportTelemetry.ChatSourceName)
        .AddSource(SupportTelemetry.FeedbackSourceName)
        .AddSource(SupportTelemetry.MafAgentSourceName)
);
builder.Services.ConfigureOpenTelemetryMeterProvider(m =>
    m.AddMeter(SupportTelemetry.ChatSourceName)
);
```

4. Lines 45–48 — the `AddSupportAgent` call and its comment:

```csharp
// MAF order-support agent + its config-driven IChatClient. SupportAgent:Provider selects
// Fake | Ollama | FoundryLocal | AzureFoundry (Fake is the Development default; Ollama is the real
// local provider). This registers the agent; the AG-UI endpoint is mapped below at /storefront/agent.
builder.AddSupportAgent();
```

5. Lines 54–58 — collapse the auth registration. Replace:

```csharp
builder
    .AddAtriumJwtAuth()
    // Step-up MFA for the support agent endpoint: always authenticated, and (when enabled via
    // SupportAgent:StepUp) a real or simulated step-up claim. See StepUpMfa.cs.
    .AddPolicy(StepUpMfaRequirement.PolicyName, StepUpMfaRequirement.Configure);
```

with:

```csharp
builder.AddAtriumJwtAuth();
```

(Trim the preceding comment block's step-up sentence if it mentions the agent.)

6. Lines 67–68 — the inert-gate warning and its comment:

```csharp
// Surface a misconfigured (inert) step-up gate outside Development, where it is opt-in by default.
app.WarnIfStepUpGateInert();
```

7. Lines 94–96 — the endpoint mappings and their comment:

```csharp
// The AG-UI support-agent endpoint at /storefront/agent (SSE), step-up-MFA gated (see SupportEndpoints).
storefront.MapSupportAgent();
storefront.MapSupportFeedback();
```

- [ ] **Step 3: Edit the csproj.** In `Atrium.Services.Storefront.csproj` remove these package references **and the MEAI comment above them**:

```xml
    <PackageReference Include="Microsoft.Agents.AI" Version="1.12.0" />
    <PackageReference
      Include="Microsoft.Agents.AI.Hosting.AGUI.AspNetCore"
      Version="1.12.0-preview.260629.1"
    />
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.6.0" />
    <PackageReference Include="OpenAI" Version="2.10.0" />
```

**KEEP** `<PackageReference Include="Microsoft.OpenApi" Version="2.9.0" />` — it pins a security advisory (NU1903) in `Microsoft.AspNetCore.OpenApi`'s transitive graph, which is unrelated to the agent. Edit its comment to drop the "surfaced ... on the fresh MAF restore" clause, keeping the advisory rationale.

- [ ] **Step 4: Gate** — run the Lane A gate. Expected: green. If the build reports unused-using warnings in `Program.cs`, remove exactly the usings it names (0 warnings is the bar).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "chore(storefront): remove the MAF support-agent backend (ADR-0013)"
```

---

### Task 3: Remove the client-side chat slice (Design, Portal, Abstractions, Modules, Contracts)

**Files:**
- Delete from `src/Atrium.Design/`: `Components/AgentChat.razor`, `Components/AgentChat.razor.css`, `wwwroot/js/agentchat.js`, `AgentChatClientFactory.cs`, `AgentChatServiceCollectionExtensions.cs`, `FeedbackClient.cs`, `BearerTokenHandler.cs`
- Modify: `src/Atrium.Design/Atrium.Design.csproj`
- Delete: `src/Atrium.Portal/Components/Layout/AssistantLauncher.razor`
- Modify: `src/Atrium.Portal/Components/Layout/MainLayout.razor`, `src/Atrium.Portal/Program.cs`
- Delete: `src/Atrium.Abstractions/AgentSurface.cs`
- Modify: `src/Atrium.Abstractions/IModule.cs`
- Modify: `src/Atrium.Modules.Storefront/StorefrontModule.cs`
- Delete: `src/Atrium.Contracts/FeedbackDto.cs`
- Delete: `tests/Atrium.UnitTests/FeedbackControlTests.cs`
- Modify: `tests/Atrium.UnitTests/StorefrontModuleTests.cs`

**Interfaces:**
- Produces: `IModule` without `AgentSurfaces`; `Atrium.Design` without any chat/bearer-handler types. `AccessTokenHolder`, `HttpClientExtensions`, `SessionExpiredException` remain and are unchanged.

- [ ] **Step 1: Delete files**

```bash
git rm src/Atrium.Design/Components/AgentChat.razor src/Atrium.Design/Components/AgentChat.razor.css \
       src/Atrium.Design/wwwroot/js/agentchat.js src/Atrium.Design/AgentChatClientFactory.cs \
       src/Atrium.Design/AgentChatServiceCollectionExtensions.cs src/Atrium.Design/FeedbackClient.cs \
       src/Atrium.Design/BearerTokenHandler.cs \
       src/Atrium.Portal/Components/Layout/AssistantLauncher.razor \
       src/Atrium.Abstractions/AgentSurface.cs \
       src/Atrium.Contracts/FeedbackDto.cs \
       tests/Atrium.UnitTests/FeedbackControlTests.cs
```

- [ ] **Step 2: Edit `Atrium.Design.csproj`.** Remove the AGUI package line:

```xml
    <PackageReference Include="Microsoft.Agents.AI.AGUI" Version="1.12.0-preview.260629.1" />
```

Then verify whether Design still uses `Atrium.Contracts` anywhere:

```bash
grep -rn "Atrium.Contracts\|FeedbackDto" src/Atrium.Design --include="*.cs" --include="*.razor"
```

Expected: no hits outside the csproj. If so, also remove from the csproj the `Atrium.Contracts` `ProjectReference` (and its ADR-0006 comment) **and** the whole global-using ItemGroup:

```xml
  <ItemGroup>
    <!-- Global using so Razor components (AgentChat) resolve the shared contracts without a per-file
         @using; global usings apply to Razor-generated code in this same compilation. -->
    <Using Include="Atrium.Contracts" />
  </ItemGroup>
```

- [ ] **Step 3: Edit `MainLayout.razor`.** Remove the launcher block (lines 19–23):

```razor
        <AuthorizeView>
            <Authorized>
                <AssistantLauncher />
            </Authorized>
        </AuthorizeView>
```

- [ ] **Step 4: Edit Portal `Program.cs`.** Remove line 26 (`builder.Services.AddAgentChat();`) plus any comment line directly above that refers to agent chat.

- [ ] **Step 5: Edit `IModule.cs`.** Remove:

```csharp
    /// <summary>Chat surfaces the module contributes to the shell's assistant launcher. Default: none.</summary>
    IEnumerable<AgentSurface> AgentSurfaces => [];
```

- [ ] **Step 6: Edit `StorefrontModule.cs`.** Remove the whole `AgentSurfaces` property (the block returning `new AgentSurface("Support", "storefront/agent", StarterPrompts: [...])`) and any comment attached to it.

- [ ] **Step 7: Edit `StorefrontModuleTests.cs`.** Delete the test method that asserts on `AgentSurfaces` (it contains `Assert.Single(new StorefrontModule().AgentSurfaces)`) including its `///` doc comment mentioning `<AgentChat>`. Keep all other tests in the file.

- [ ] **Step 8: Gate** — run the Lane A gate. Expected: green.

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "chore(portal,design): remove AG-UI chat client, launcher, and AgentSurfaces seam (ADR-0013)"
```

---

### Task 4: Clean the AppHost

**Files:**
- Modify: `src/Atrium.AppHost/apphost.cs`

- [ ] **Step 1: Remove the four env lines** from the storefront resource (lines 37–40):

```csharp
    .WithEnvironment("SupportAgent__Provider", "Ollama")
    .WithEnvironment("SupportAgent__Endpoint", "http://localhost:11434/v1")
    .WithEnvironment("SupportAgent__Model", "qwen2.5:7b-instruct")
    .WithEnvironment("SupportAgent__GuardrailModel", "llama3.2:3b")
```

- [ ] **Step 2: Gate** — run the Lane A gate (the AppHost is a single-file project; the solution build validates it).

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "chore(apphost): drop SupportAgent/Ollama environment wiring (ADR-0013)"
```

---

### Task 5: Documentation — ADR-0013, supersede ADR-0011, docs sweep

**Files:**
- Create: `docs/adr/0013-retire-the-support-agent-slice.md`
- Modify: `docs/adr/0011-circuit-scoped-bearer-handler.md` (status line only), `docs/adr/README.md`, `CLAUDE.md`, `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/interview/07-CLARIFICATIONS.md`
- Move: `docs/ATRIUM-AI-EXTENSIBILITY-DESIGN.md` → `docs/archive/ATRIUM-AI-EXTENSIBILITY-DESIGN.md`

- [ ] **Step 1: Write ADR-0013** at `docs/adr/0013-retire-the-support-agent-slice.md`:

```markdown
# ADR-0013 — Retire the Support agent slice

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** pre-demo hardening (2026-07)

## Context

The AI Support agent (Microsoft Agent Framework over Ollama; AG-UI SSE chat at
`/storefront/agent`; OTel → guardrail → cache decorator pipeline; step-up MFA gate; telemetry-only
feedback; LLM eval suite) shipped in PR #1 and was hardened by the 2026-07-02 audit (A1–A7, all
remediated). It worked. It was also the single largest piece of surface area in the repo relative
to how central it is to what Atrium demonstrates: modular Blazor architecture, auth, and
backend/data discipline.

For the demo, that ratio is the problem. The slice invites deep questioning on a stack
(MAF preview APIs, guardrail prompt design, eval methodology) that is peripheral to the system's
core story, and it carries five preview-version NuGet dependencies.

A feature flag was considered and rejected: flagged-off code keeps 100% of the in-repo scrutiny
surface — the packages, the pipeline, the ADR exception — while adding a toggle on top.

## Decision

Remove the slice from `main` entirely. The complete working implementation is preserved on the
**`feat/support-agent`** branch (pushed to origin), cut from the last pre-removal commit.

Removed: `Support/` in the Storefront service (agent, tools, guardrail, cache, step-up MFA,
feedback endpoint), the AG-UI client plumbing in `Atrium.Design` (`AgentChat`, factory,
`BearerTokenHandler`, feedback client), the Portal `AssistantLauncher`, the `IModule.AgentSurfaces`
seam, `FeedbackDto`, the AppHost Ollama wiring, the unit-test suite for the slice, and the
`Atrium.Evals` project.

[ADR-0011](0011-circuit-scoped-bearer-handler.md) is superseded by this ADR: its sanctioned
exception (a manually-composed, circuit-scoped `DelegatingHandler` for the AG-UI client) left with
the slice. The underlying rule it carved an exception from — no *factory-registered* bearer
handlers, per [ADR-0004](0004-token-propagation-and-option-b.md) — stands unchanged.

## Consequences

- The demo surface is exactly the system's core story; no preview packages remain.
- Feedback/eval history stays in `docs/archive/runs/`, `docs/audits/`, and the 2026-07-02 spec —
  the work is documented, reviewable, and honestly dated, without being deployed.
- Anyone can `git switch feat/support-agent` and run the full agent locally (Ollama required).

## Future direction

The agent returns — but shaped like the rest of the platform, not embedded in one vertical:

- A dedicated core service (working name `Atrium.Services.Agent`) owns the model pipeline
  (provider selection, guardrail, cache, telemetry) and exposes chat per registered surface.
- Its tools call the capability services **over HTTP with the relayed bearer** (the same
  composition grain as ADR-0005) instead of reaching into one service's repositories.
- Modules contribute chat surfaces declaratively — the `IModule.AgentSurfaces` +
  `AssistantLauncher` pattern preserved on `feat/support-agent` is the starting point; the branch
  is the reference implementation for the pipeline, the guardrail posture (screen all user
  messages, fail closed), and the eval harness.

Revive by porting from the branch, not by reverting the removal commits — the platform will have
moved (notably: the UI layer is migrating to MudBlazor, which replaces the chat styling substrate).
```

- [ ] **Step 2: Supersede ADR-0011.** In `docs/adr/0011-circuit-scoped-bearer-handler.md`, change the status line to:

```markdown
**Status:** Superseded by [ADR-0013](0013-retire-the-support-agent-slice.md) · **Deciders:** Atrium build · **Context phase:** AI chat enhancements (2026-07)
```

Do not edit anything else in the file.

- [ ] **Step 3: Update `docs/adr/README.md`.** Add a 0013 row/entry matching the existing index format; annotate the 0011 entry as superseded (match how the index formats titles — read it first).

- [ ] **Step 4: Update `CLAUDE.md`.** Precise edits:
  - Commands block: delete the `dotnet test tests/Atrium.Evals` line and the "Evals judge via Ollama…" paragraph.
  - Architecture paragraph: in the `Atrium.Design` description, delete "+ the AG-UI chat plumbing" (keep tokens + primitives + `AccessTokenHolder`).
  - ADR range: "0001–0012" → "0001–0013".
  - Token-flow bullet: delete the sentence "The AG-UI chat client's `BearerTokenHandler` is the one sanctioned exception, composed manually inside the circuit scope (ADR-0011)."
  - Auth matrix: delete "the agent endpoint has a step-up MFA policy".
  - Delete the entire "The AI **Support agent** lives in…" bullet.

- [ ] **Step 5: Update `AGENTS.md` and `docs/ARCHITECTURE.md`.** Search each for `agent`, `AG-UI`, `Support`, `Ollama`, `MAF`, `step-up`; remove or rewrite each hit so the docs describe the post-removal system. Where the story is worth keeping, one line pointing at ADR-0013 replaces the section. (Read the surrounding prose — these are narrative docs; make the result read naturally, not redacted.)

- [ ] **Step 6: Archive the extensibility design**

```bash
git mv docs/ATRIUM-AI-EXTENSIBILITY-DESIGN.md docs/archive/ATRIUM-AI-EXTENSIBILITY-DESIGN.md
```

Fix any links to it (grep `ATRIUM-AI-EXTENSIBILITY` across the repo).

- [ ] **Step 7: Tick the interview checklist.** In `docs/interview/07-CLARIFICATIONS.md`, change the pre-demo item to checked and append the outcome:

```markdown
- [x] **Remove / deactivate the MAF agent slice before the demo.** Done 2026-07 — removed from
  `main`, preserved on `feat/support-agent`; decision + future modular-agent direction in ADR-0013.
```

- [ ] **Step 8: Gate + commit**

```bash
git add -A && git commit -m "docs: ADR-0013 retire the support agent; supersede ADR-0011; sweep agent references"
```

---

### Task 6: Grep gate + SAFE-REVERT-POINT

- [ ] **Step 1: Run the grep gate.** Zero hits expected in `src/` and `tests/`:

```bash
grep -rnE "AgentChat|SupportAgent|AGUI|Microsoft\.Agents|agentchat|FeedbackDto|StepUpMfa|AgentSurface|AddAgentChat|BearerTokenHandler|IFeedbackClient|Ollama" \
  src/ tests/ --include="*.cs" --include="*.razor" --include="*.csproj" --include="*.json" --include="*.js" --include="*.css"
```

Expected: **no output**. Any hit is an incomplete removal — fix it and re-run. Then check the living docs (`CLAUDE.md`, `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/guides/`, `.claude/skills/`): agent mentions there must be gone too (archive/audit/spec/interview/ADR paths are sanctioned history).

- [ ] **Step 2: Full Lane A gate** (build + all tests). Expected: green, 0/0.

- [ ] **Step 3: SAFE-REVERT-POINT commit** (empty commit is fine if the tree is clean):

```bash
git commit --allow-empty -m "chore(run): SAFE-REVERT-POINT — agent slice fully removed, gates green"
```

---

### Task 7: Lane B — live smoke via Playwright (Ollama off)

**Files:** artifacts only (screenshots under `artifacts/retire-support-agent/`)

- [ ] **Step 1: Confirm Ollama is NOT running:** `curl -s --max-time 2 http://localhost:11434/api/tags` must **fail** (connection refused). If it responds, stop Ollama first.

- [ ] **Step 2: Boot the stack** — run `aspire run` from `src/Atrium.AppHost` in the background. Wait (up to ~180s; Keycloak is slow to start) until **both** health checks return `Healthy`:

```bash
curl -s http://localhost:5260/health   # Healthy
curl -s http://localhost:5109/health   # Healthy
```

- [ ] **Step 3: Drive the Portal with Playwright** at https://localhost:7001 (fallback http://localhost:5035). Screenshot every numbered state to `artifacts/retire-support-agent/NN-name.png`; after each page, read the browser console — **zero errors** is the assertion (warnings: record, don't fail).
  1. Landing page renders; click Sign in → Keycloak at http://localhost:8080 → log in `testuser` / `password` → redirected back signed in.
  2. **Topbar contains the theme toggle and the user avatar menu and NO chat/assistant icon button** (the launcher rendered an icon-only button with a chat-bubble SVG between the breadcrumb spacer and the theme toggle — assert absence).
  3. Storefront `/storefront`: products render; add 2 items to cart.
  4. `/storefront/cart`: both items present; change a quantity.
  5. Checkout: place the order; order confirmation renders; `/storefront/orders` lists it.
  6. Nav check: `testuser` sees no Admin or Reports links.
  7. Sign out; sign in as `admin` / `password`; `/admin` product table renders; `/reports` dashboard renders.
  8. Direct probe: `https://localhost:7271/storefront/agent` returns 401/404 (never 200/SSE).

- [ ] **Step 4: Record results** — pass/fail per numbered step in the run LOG; any failure follows the Global Constraints failure protocol (this task is functional — failures block).

- [ ] **Step 5: Shut the stack down** (kill the `aspire run` process).

---

### Task 8: Wrap-up

- [ ] **Step 1: Push the run branch**

```bash
git push -u origin run/retire-support-agent
```

Do **not** merge to `main` unattended — leave the branch for the user's morning review.

- [ ] **Step 2: Local (non-repo) cleanup.** In `/Users/ted/code/Atrium/.claude/settings.local.json`, remove the `SessionStart` hook entry whose command echoes "[Atrium] Read docs/superpowers/specs/2026-07-02-ai-chat-enhancements-design.md …" (the spec it mandates is now historical). Leave every other setting untouched. This file is gitignored — no commit.

- [ ] **Step 3: Write the run summary** (LOG): tasks completed, gate results, Lane B step results with artifact paths, anything marked BLOCKED or `[~]`, and the note that `feat/support-agent` + ADR-0013 carry the revival story.
