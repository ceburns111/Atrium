# Interview study — The AI slice (MAF + AG-UI support agent)

> This is the one part of Atrium the interviewing company's architecture does **not** have — so it's
> your differentiator, but also the part you must be able to defend as *engineering*, not a bolt-on demo.
> The whole design goal was: **add an AI agent the same way we add any other feature slice** — behind the
> gateway, authenticated with the same bearer, scoped to the signed-in user, config-driven, testable
> offline. Nothing about it breaks the existing architecture's rules.

## The 90-second explanation

"I added a customer **Support** agent to the Storefront vertical using the **Microsoft Agent
Framework (MAF)**, exposed over **AG-UI** (an SSE streaming protocol) at `/storefront/agent` behind the
same YARP gateway and the same Keycloak bearer as every other endpoint. The agent has two tools —
`GetOrderStatus` (scoped to the signed-in user's own orders) and `FindProduct` (catalog search). The model
is **config-driven**: a `Fake` in-process client is the Development default so the app boots and tests run
with no model or network; flipping `SupportAgent:Provider` to `FoundryLocal` or `AzureFoundry` points it at
any OpenAI-compatible endpoint — no code change. The UI is a reusable `<AgentChat>` Blazor primitive in the
design system that streams tokens and shows tool-call cards, and a shell **assistant launcher** that a
module lights up by declaring an `AgentSurface`. The agent endpoint is gated by a config-driven **step-up
MFA** policy. So: same ingress, same auth, same test discipline as the rest of the platform — the AI is
just another slice."

## How it actually works

**The agent (`src/Atrium.Services.Storefront/Support/SupportAgent.cs`).** A MAF `ChatClientAgent` built
over an injected `IChatClient`, with a system prompt that forbids inventing order state, and two tools
registered via `AIFunctionFactory.Create`. The tool schema comes from each method's `[Description]`
attribute — that's what the model reads to decide when to call it.

**The lifetime trick worth knowing cold.** AG-UI's `MapAGUI` captures **one** agent instance for the
endpoint's lifetime (effectively a singleton, resolved from the root provider). But the tools need the
**request-scoped** `SupportTools` — whose `OrderRepository` owns a per-request `SqlConnection` and whose
identity comes from the current `HttpContext`. So the agent must **not** capture `SupportTools` directly.
Instead each tool is built with `AIFunctionFactory.Create(method, _ => ResolveTools(httpContextAccessor))`
— the factory runs **per tool invocation**, resolving a fresh `SupportTools` from
`HttpContext.RequestServices`. Result: a singleton agent, but every tool call sees the *current* signed-in
caller and their correctly-scoped services. This is the single most important thing to be able to explain —
it's where concurrency correctness lives.

**Tools + user scoping (`Support/SupportTools.cs`).** `GetOrderStatus` reads the username from
`IHttpContextAccessor` (`preferred_username` claim) and calls `IOrderRepository.GetByIdAsync(orderId,
userName)`. The security boundary is **in the stored procedure's `WHERE o.Id=@OrderId AND
o.UserName=@UserName`** — an order that exists but belongs to someone else collapses to `null`, so the
agent literally cannot read another user's order. `GetOrderStatus` reports an honest "Confirmed" status
(the store has no shipping lifecycle column, so it never fabricates Shipped/Delivered). `FindProduct` goes
through the same `StorefrontCatalogClient` the rest of the vertical uses.

**Config-driven model (`Support/SupportAgentBuilderExtensions.cs`).** `SupportAgent:Provider` selects
`Fake` | `FoundryLocal` | `AzureFoundry`. `Fake` (the `CannedChatClient`) is the **Development default** so
a fresh checkout boots and the whole test gate runs with no AI config. `FoundryLocal` and `AzureFoundry`
share one construction path because both are OpenAI-compatible — they differ only in endpoint/key/model.
Missing/unknown provider **throws at startup** outside Development (fail-fast, not silent).

**Transport + UI.** The endpoint is mapped in `Support/SupportEndpoints.cs`
(`storefront.MapSupportAgent()`) onto the `/storefront` route group, gated by the step-up policy. On the
client side, `<AgentChat>` (in `Atrium.Design`) builds an `AGUIChatClient` via `AgentChatClientFactory`,
which wraps the pooled gateway handler chain with a `BearerTokenHandler` that attaches the signed-in user's
token **from the per-circuit `AccessTokenHolder`** — the exact same token mechanism the module typed
clients use, and a 401 becomes the same `SessionExpiredException` → "sign in again" notice. A module
declares an `AgentSurface` (name + gateway-relative endpoint + starter prompts); the shell's
`AssistantLauncher` renders it, role-gated to modules the user can actually see.

**Step-up MFA (`Support/StepUpMfa.cs`).** A config-driven authorization policy on the agent endpoint:
authenticated first (else 401), then a step-up claim (else 403). The claim seam is cloud/local-agnostic —
Entra stamps `amr` (mfa/otp/…), a Keycloak step-up flow stamps `acr`. `Simulate` is a **Development-only**
escape hatch (hardened in the Run 4 review so a stray prod `Simulate=true` can't weaken the gate), and a
startup **warning** fires if the opt-in gate is left inert outside Development.

## Why it's built this way

- **AG-UI/MAF over rolling my own chat loop:** MAF gives the agent+tool-calling loop and AG-UI gives a
  standard SSE contract the Blazor client streams — I didn't hand-roll SSE or tool dispatch. The trade-off:
  it's a **preview** stack (the API shape shifted between the design sketch and 1.12.0, which is exactly why
  C0 was a go/no-go package spike before any feature work).
- **Config-driven provider with a Fake default:** the model is a *deployment* concern, so it belongs in
  config, not code. The Fake default is what keeps the AI slice from making the build/test/boot depend on a
  model or network — offline-first, same as the rest of the suite.
- **Reuse the existing auth/token seam instead of a new one:** the agent request rides the same bearer,
  through the same gateway, validated by the same service — so "is the user allowed to talk to this agent,
  and can it see their data?" reuses answers the platform already had. The only new concept is step-up MFA,
  and that's a *policy*, not new plumbing.
- **User scoping in the sproc, not the prompt:** you never trust the model (or the prompt) for
  authorization. The `WHERE UserName=@UserName` is the boundary; the agent physically cannot fetch someone
  else's order regardless of what it's asked.

## What's impressive here / talking points

- **Singleton-agent / request-scoped-tools** reconciliation — a real concurrency-correctness design, not an
  accident. Steer here.
- **"The AI is just another slice"** — same ingress, same bearer, same test gate, same feature-folder
  layout. It didn't require special-casing the architecture.
- **Security posture:** authorization is enforced in the data layer (sproc `WHERE`), not trusted to the
  model; plus a config-driven step-up-MFA policy with an Entra/Keycloak-agnostic claim seam.
- **Offline-first testability:** a deterministic `Fake` `IChatClient` means the agent, tools, and step-up
  policy are all unit-tested with no model (`tests/Atrium.UnitTests/Support/*`).
- **Honest agent:** the system prompt + `GetOrderStatus` refuse to invent order progress the data doesn't
  support — a deliberate anti-hallucination choice you can speak to.

## Likely interview questions → strong answers

- **"How does the agent avoid leaking one user's orders to another?"** The tool resolves the caller from
  `HttpContext` and passes the username to a stored proc that filters on `Id AND UserName`; a
  non-owned order returns `null`. Authorization is in the data layer, not the prompt — the model can't be
  social-engineered past a SQL `WHERE`.
- **"A singleton agent with per-request data — how is that safe under concurrency?"** The agent instance is
  captured once, but the tools aren't captured; each tool invocation resolves a fresh request-scoped
  `SupportTools` from `HttpContext.RequestServices`, so it sees the current caller's identity and their own
  scoped `SqlConnection`. No shared mutable per-request state lives on the agent.
- **"How do you test an agent without a model?"** A deterministic `Fake`/`CannedChatClient` implementing
  `IChatClient` is the Development default and the test double; tool logic, user-scoping, and the step-up
  policy matrix are all unit-tested offline. Real-model behavior is verified in a supervised live pass.
- **"How do you switch models / go to Azure?"** Config only: `SupportAgent:Provider` +
  `Endpoint/ApiKey/Model`. FoundryLocal and AzureFoundry are both OpenAI-compatible, so they share one
  client path. No code change, and a missing/unknown provider fails fast at startup outside Dev.
- **"What is step-up MFA and why on the agent?"** The agent can surface account data, so it warrants a
  stronger assurance than plain login. The policy requires an MFA-grade claim (`amr` for Entra, `acr` for
  Keycloak). It's opt-in via config, Simulate is Development-only, and an inert gate logs a startup warning.
- **"Why AG-UI / MAF instead of just calling the model API?"** I wanted a standard streaming + tool-calling
  contract the Blazor client consumes uniformly, rather than hand-rolling SSE and a tool dispatch loop. The
  cost is a preview dependency, which I de-risked with a go/no-go spike before building features on it.
- **"How does the token reach the agent from a Blazor circuit?"** Same seam as every module client: the
  per-circuit `AccessTokenHolder` holds the access token; a `BearerTokenHandler` (a `DelegatingHandler`
  composed *inside* the circuit scope, because the AG-UI client owns its `HttpClient`) attaches it and maps
  a 401 to `SessionExpiredException`.

## Gotchas & things that could trip you up

- **Don't say the agent is request-scoped** — the *agent* is a singleton; the *tools* are request-scoped.
  Mixing this up is the fastest way to look like you didn't build it.
- **`AgentSurface.Endpoint` is gateway-relative with NO leading slash** (`"storefront/agent"`) — `<AgentChat>`
  resolves it against the gateway base. A leading slash would break resolution.
- **AG-UI threads are ephemeral** — no `AgentSessionStore` is registered, so there's no cross-user
  ThreadId-resume risk, but also no server-side conversation memory (the client sends the transcript each
  turn).
- **The Fake reply is a feature, not a bug** — "Support is running in local (Fake) mode" means no model is
  configured; it also *proves* the bearer reached the endpoint.

## Running a real model locally (keep this handy)

The Development default is `Fake`, so out of the box you get the canned reply. To get real answers with no
code change:

1. Run a **tool-calling** model locally via Ollama (OpenAI-compatible endpoint):
   ```bash
   ollama pull qwen3:14b-q4_K_M   # tool-calling capable — required for GetOrderStatus/FindProduct
   ollama serve                    # exposes http://localhost:11434/v1
   ```
2. Add to `src/Atrium.Services.Storefront/appsettings.Development.json`, then restart `aspire run`:
   ```json
   "SupportAgent": {
     "Provider": "FoundryLocal",
     "Endpoint": "http://localhost:11434/v1",
     "ApiKey": "ollama",
     "Model": "qwen3:14b-q4_K_M"
   }
   ```
   `ApiKey` must be non-empty (the config validator requires it; Ollama ignores the value). Real Foundry
   Local or Azure AI Foundry just change these four values. A non-tool-calling model will chat but never
   fire the tools.

## If they push deeper / how I'd evolve it

- **Server-side conversation memory / durable threads:** register an `AgentSessionStore` keyed by user +
  surface, so a conversation survives a reload — with per-user isolation as the top correctness concern.
- **Cloud credentials:** today the real providers use an API key from config; production would use
  `DefaultAzureCredential`/managed identity for Azure AI Foundry rather than a parked key (deferred with the
  rest of the Azure work).
- **A general authorization-aware query layer** *(design direction, not built)*: the agent scopes at the
  sproc today; the natural next step for a richer agent is a query-composition/evaluator layer that refuses
  to even *build* an unauthorized query — authorization pushed up in front of data access so the model's
  requested filters are validated against the caller's grants before any SQL is generated. Frame this as
  where I'd take it, not as something already in the repo.
- **Guardrails/eval:** add prompt-injection defenses on tool inputs and an offline eval harness over the
  Fake client to catch hallucination regressions.
