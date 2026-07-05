# Full codebase & documentation audit — 2026-07-02

## Remediation status (2026-07-03)

All **FIX**-disposition findings below landed via the 2026-07-03 remediation pass, plus the three
user-reported TODO bugs (chat empty-answer path, feedback-thumb visuals, dark-mode chip hover
contrast). Gate at close: `dotnet build` 0 warnings / 0 errors · unit tests 97/97 · integration tests
13/13. Exceptions:

- **A4 (feedback→trace correlation): deferred, documented** — no clean seam (client-only turn GUID;
  the preview AG-UI client exposes no traceparent hook); limitation noted at the feedback span and in
  the design doc's shipped-state notes.
- **ACCEPT items unchanged:** U16 (sign-out GET, now commented), U17 (module structural drift),
  B11's unbounded list sprocs (demo scale).

The **DOCS**-disposition items were handled by the same-day documentation overhaul (ADR-0011/0012,
amendments to 0001/0003/0004/0007, ARCHITECTURE/AGENTS/CLAUDE/HANDOFF/guide/README refreshes, and the
docs-tree reorganization into `docs/archive/`).

Four parallel audit passes (backend/SQL, AI support slice, UI/portal/modules, architecture coherence)
over `main` at `d281e08`. Baselines at audit time: build clean (0 warnings / 0 errors), unit tests 87/87.

**Verdict up front:** no critical findings. The runtime architecture held up under adversarial checking
(module discovery, gateway routes, ADR-0009 nesting, realm/audience wiring, contracts discipline, chat
decorator order, order user-scoping, step-up MFA matrix were all verified correct). The real weaknesses
cluster in four places: the order-idempotency mechanism (~2/3 implemented), the guardrail's depth,
UI edge paths the happy path never exercises, and — biggest of all — **documentation lag behind the AI
slice**: the repo's self-description is its main liability.

Disposition legend: **FIX** = fixed in the remediation pass following this audit ·
**DOCS** = handled in the documentation overhaul · **ACCEPT** = deliberate non-fix, reason given.

---

## Critical

None found.

## High

| ID | Finding | Confidence | Disposition |
|----|---------|-----------|-------------|
| B1 | **Order idempotency-key dedupe is not user-scoped** — `usp_Order_Create.sql` looks up `IdempotencyKey` without `UserName`; user B replaying user A's key gets A's order id back and B's order is silently never created. `usp_Order_GetById` scopes correctly; `usp_Order_Create` diverged. | High | FIX |
| B2 | **Order create returns a fabricated `OrderDto`, not the stored order** — endpoint re-prices from the current request/catalog and stamps `DateTime.UtcNow`; on the exact replay path idempotency exists for, the response total/lines/timestamp can disagree with what was committed. | High | FIX |
| A1 | **Guardrail classifies only the last user message of a client-controlled transcript** — AG-UI threads are ephemeral and the client resends full history; an injection placed in an earlier message (or fabricated assistant turn) reaches the 7B model unscreened. Ambiguous classifier output also fails open. | High (mechanism) / Medium (exploit value in demo) | FIX |
| U1 | **AgentChat dialog grows per message until the whole dialog scrolls** (TODO.md known bug) — `.agent-chat { height: 100% }` against an auto-height `.dialog__panel/__body` chain resolves to auto; the log never overflows so `scrollToEnd` pins nothing. | High | FIX |
| U2 | **Dialog opens with a stray focus ring on the ✕ close button** (the `docs/bugs/CARROTPAD.png` screenshot, since removed) — `showModal()` focuses the first focusable (the ✕), programmatic focus triggers `:focus-visible`, and the dialog's ring is `--ink` (white in dark mode) instead of the system-wide `--accent`. No `autofocus` target exists. | High | FIX |
| U3 | **Admin `Save()` has no try/catch/finally** — any non-400/403/404 failure (e.g. transient 502) escapes to `SessionErrorBoundary`, replacing the page and destroying the open dialog + typed edits, contradicting the page's own "inline toasts" comment. | High | FIX |
| U4 | **Currency rendered `"N0"` at 12 sites silently rounds cents** — schema is `DECIMAL(10,2)`, Admin form accepts `19.99`, Shop/Cart/Checkout/Pay button show `$20` while the order stores 19.99. `$` hard-coded next to culture-sensitive formatting. | High (mechanism) / Medium (visible today — seed data is whole dollars) | FIX |
| D1 | **ADR-0004/ARCHITECTURE.md/CLAUDE.md/HANDOFF state "no DelegatingHandler for the bearer token" categorically — `BearerTokenHandler` is one.** The code is *correct* (manually composed inside circuit scope, not factory-resolved; tested; explained in AgentChatClientFactory) — the categorical doc claim is false and would cause a reviewer/agent to "fix" working code. | High | DOCS |
| D2 | **ARCHITECTURE.md (the designated reference model) omits the entire AI support slice and `Atrium.ServiceDefaults`** — no `/storefront/agent` route, step-up policy, AgentSurface, feedback endpoint, Evals project, or telemetry project in the topology/solution tables. | High | DOCS |

## Medium

| ID | Finding | Confidence | Disposition |
|----|---------|-----------|-------------|
| B3 | Concurrent double-submit of the same idempotency key 500s (SELECT-then-INSERT race; unique index violation uncaught) despite the sproc comment calling the index "the integrity backstop". | High | FIX |
| B4 | `Identity?.Name ?? "unknown"` creates a shared cross-user order bucket for any token without `preferred_username`. | High (path) / Medium (reachability) | FIX |
| B5 | `DatabaseInitializer` duplicated line-for-line in both services (classic copy-drift; ADR-0007's "byte-identical" claim already stale). | High | FIX |
| B6 | Keycloak JWT config + `admin` policy + 25-line Redoc HTML triplicated across service hosts; the claim-mapping settings are load-bearing and can drift per host. | High | FIX |
| B7 | Order total computed independently in endpoint and repository (`lines.Sum(...)` twice) — stored vs returned totals equal only by coincidence. | High | FIX |
| A2 | "Support" rendered twice stacked — Dialog `Title` and AgentChat's own header both show the surface name (TODO.md known bug). | High | FIX |
| A3 | Guardrail silently becomes a no-op when `SupportAgent:GuardrailModel` is unset in any environment — no startup warning, unlike the step-up gate built for exactly this failure mode. | High | FIX |
| A4 | Feedback span is an uncorrelated root activity keyed by a client-only GUID — the design's "tie thumbs to the originating trace" join is impossible from recorded data. | High (mechanics) / Medium (severity — plan shipped this shape) | FIX (correlate if a clean seam exists; else DOCS-record the limitation) |
| A5 | Eval harness drifts from the production agent: different system prompt (drops the anti-hallucination clause), hand-duplicated tool descriptions, hardcoded model/endpoint — scores certify a prompt that isn't deployed. | High | FIX |
| A6 | All three evaluators run on every scenario but greeting/off-topic scenarios pass empty context — scorecard will show error/degraded Groundedness/ToolCallAccuracy entries contradicting the code comments. | Medium (depends on M.E.AI.Evaluation missing-context behavior) | FIX |
| A7 | Guardrail re-classifies the same user message on every tool-loop iteration and is itself completely uninstrumented (no OTel wrap) — hidden latency, invisible in the Aspire trace. | Medium-high | FIX |
| U5 | Shop category filter has a last-write-wins race (no sequence guard/cancellation) — stale products under the wrong chip. | High (mechanism) | FIX |
| U6 | Cart hydration wholesale-replaces the cart (`Restore` clears) — items added during the hydrate window are dropped and the loss is persisted; JS module import not concurrency-safe; fire-and-forget saves unordered. | Medium | FIX |
| U7 | Two overlapping role-gating mechanisms (`IModule.RequiredRole` vs `NavItem.RequiredRole`) must be manually kept in sync; `IsVisible` helper copy-pasted in NavMenu and AssistantLauncher. | High | FIX |
| U8 | `Error.razor` is untouched template boilerplate — dead Bootstrap classes, dev instructions shown to production users, only page not using design primitives. | High | FIX |
| U9 | Typed-client boilerplate re-implemented four ways (two byte-identical `GetAsync<T>` copies; divergent error styles incl. a message-less `InvalidOperationException` and a null-return that forces a "phantom order" guard in Checkout). | High | FIX |
| U10 | `OrdersPage` renders `PlacedAtUtc.ToLocalTime()` — server timezone, not the browser's; a lie in prod, a no-op in containers. | High | FIX |
| U11 | AgentChat pins client + transcript to the first `Endpoint`; the launcher is built to swap surfaces per section — the day a second surface ships, chats go to the wrong agent with the wrong history. | High (mechanism) / Low (impact today) | FIX |
| D3 | Feedback wire DTO defined twice (`FeedbackDto` in Design, `FeedbackRequest` in the service) — the exact producer/consumer drift pattern ADR-0006 rejected. | High | FIX (move to Contracts) |
| D4 | ADR-0003 claims the portal secret "never lives in the repo"; it is committed twice (`apphost.cs` literal + realm export). Unused `UserSecretsId` suggests the intended mechanism was abandoned midway. | High | DOCS (soften claim; dev-only secret is fine, the *claim* is wrong) |
| D5 | CLAUDE.md "Active work" section and HANDOFF.md describe a state that no longer exists — the branch is merged, yet every plan checkbox is unchecked and CLAUDE.md routes agents to restart Task 1. | High | DOCS |
| D6 | `IModule` and `Atrium.Design` have both outgrown their docs: ADR-0001's interface snippet is stale (`RequiredRole`, `AgentSurfaces` missing); three docs list a deleted `Card` primitive; Design now ships HTTP/agent plumbing + a preview AGUI package into every module. | High | DOCS (+ record Design's charter decision) |
| D7 | ARCHITECTURE.md auth model ("all other reads open to any authenticated user") is wrong twice: Reports reads are admin-only; the agent endpoint has a step-up policy. | High | DOCS |
| D8 | wire-up-a-new-app.md ("every path below exists in the repo") drifted: says no OpenAPI endpoint exists (both services have `/openapi/v1.json` + Redoc), template omits `AddAtriumTelemetry` + ServiceDefaults reference, wrong `DatabaseInitializer` signature, "two test suites" (there are three). | High | DOCS |
| D9 | Test-project organization cuts across every deployment seam (TODO.md:22 concern assessed as valid but proportionate at demo scale). | High (facts) / Medium (judgment) | DOCS (record the position: tests follow the deployable, split when a vertical splits; normalize flat-vs-`Support/` folder inconsistency) |

## Low

| ID | Finding | Confidence | Disposition |
|----|---------|-----------|-------------|
| B8 | Serilog fluent defaults clobber config-supplied levels, contradicting the adjacent comment; dead MEL `Logging` sections in three hosts. | High | FIX |
| B9 | Catalog category validation is ordinal case-sensitive vs collation-insensitive SQL; sproc `THROW 50001` escapes as 500 instead of 400. | High (mismatch) / Medium (impact) | FIX |
| B10 | Non-nullable DTO members unvalidated at the boundary: missing `Blurb` → SQL 500; `Items: null` → NRE 500, where all other bad input gets 400. | Medium | FIX |
| B11 | `Quantity = int.MaxValue` overflows `DECIMAL(10,2)` → 500 instead of 400; list sprocs unbounded. | High (code) / Low (urgency) | FIX quantity bound; ACCEPT unbounded lists (demo scale, recorded here) |
| B12 | Gateway is the only host without `/health`, yet the Portal `WaitFor`s it. | High | FIX |
| B13 | Mapping drift: `CategoryDto` Dapper-bound directly while every other read uses Row+Mapperly; `ReportRepository` alone has no logger. | High (facts) | FIX (logger + CategoryRow for consistency) |
| B14 | Storefront Program.cs/csproj comments still describe the pre-Ollama Foundry provider story and call the mapped endpoint "a later item". | High | FIX (comments) |
| B15 | Integration test gaps: admin gates never asserted, no cross-user/concurrent idempotency cases; process-global env-var config trick. | High (facts) / Low (urgency) | FIX (add cross-user idempotency test alongside B1; document the env-var constraint) |
| A8 | Three near-identical canned chat clients (prod `CannedChatClient` + `FakeChatClient` + private `StubClient`). | High | FIX |
| A9 | Ollama endpoint literal duplicated four times across service + evals. | High | FIX |
| A10 | Tool-result matching by "last not-done" instead of `CallId` — wrong card flips with parallel tools. | High (code) / Low (impact) | FIX |
| A11 | Chat cache has no TTL and its cross-user safety is accidental (keyed only on message content). | Medium | FIX (TTL + comment on the user-partitioning assumption) |
| A12 | Eval "Ollama up" gate checks the daemon, not the required models — missing models fail instead of skip; guardrail transport errors surface as raw exceptions (accidental fail-closed) while ambiguous verdicts fail open — inconsistent policy. | High / Medium | FIX |
| U12 | Admin BadRequest body (ProblemDetails JSON) shown verbatim in a toast. | High | FIX |
| U13 | Admin dialog fields have unassociated labels (`Field.For` unused; Checkout does it right). | High | FIX |
| U14 | `Toasts` relies on implicit sync-context capture; a background-thread `Show` would race render enumeration. | Medium | FIX (snapshot + documented assumption) |
| U15 | MainLayout calls `Recover()` outside `InvokeAsync` while dispatching the adjacent `StateHasChanged`. | Medium | FIX |
| U16 | Sign-out is a plain GET link (cross-site triggerable, prefetchable). | High (fact) / Low (risk here) | ACCEPT with comment (demo; convention noted) |
| U17 | Structural drift: Reports/Admin flat vs Storefront feature folders; dead `Icon` on NavItem/AgentSurface; module page CSS centralized in atrium.css rather than scoped files; dark palette duplicated in tokens.css. | High (facts) | ACCEPT (recorded; churn outweighs value pre-interview) — dark-palette duplication gets a comment |
| U18 | AgentChat feedback: optimistic state never rolls back on failure; repeated clicks re-POST unbounded. | High | FIX |
| D10 | ADR-0007's anti-shared-library rationale contradicted by ServiceDefaults (which both services share). | High | DOCS (sharpen: domain/data never shared; deployment infra may be) |
| D11 | Portal carries an unused `Atrium.Contracts` reference. | High | FIX |
| D12 | realm-export.json defines an `atrium-catalog` client nothing references. | Medium | FIX (remove; realm re-import note) |
| D13 | Per-project README staleness uneven: Design/Storefront-service/UnitTests READMEs predate the AI slice; Abstractions/Gateway are current. | High | DOCS |

## Verified non-findings (checked and found correct)

- Chat pipeline decorator order (OTel → guardrail → cache) is right on both streaming and
  non-streaming paths; the "block never warms cache" regression test genuinely pins it.
- `GetOrderStatus` tool is user-scoped down to the sproc (`Id` **and** `UserName`).
- Step-up MFA Enabled/Simulate/claim matrix correct, including the production Simulate-bypass guard.
- `BearerTokenHandler` does **not** violate ADR-0004's real constraint — it is composed inside the
  circuit scope, not factory-resolved (the docs are wrong, not the code → D1).
- Portal names no module types; gateway route table has no dead/mismatched routes; ADR-0009
  implemented as written; Aspire composition matches the realm export; contracts are DTO-only
  (except D3).
- C#/SQL contracts line up everywhere cross-checked (Dapper params vs sproc signatures, row
  records vs SELECT lists, DECIMAL(10,2) vs decimal).
