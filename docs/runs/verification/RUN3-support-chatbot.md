# Supervised live-verification playbook — Run 3 (support chatbot)

Run 3 built the support-chatbot slice under the **deterministic gate only** (csharpier + build +
`dotnet test` 81/81). Everything below is what the gate **could not** check — it needs a running stack, a
model, and a browser. Work top-to-bottom; the first check proves the whole transport even without a model.

## 0. Bring up the stack
- `aspire run` from `src/Atrium.AppHost` (Docker must be up for SQL + Keycloak).
- Aspire dashboard: **https://atrium.dev.localhost:17250** → open the **`portal`** resource's endpoint.
- Sign in: `testuser` / `password` (customer) or `admin` / `password`.

## 1. Launcher visibility (no model needed)
- **Signed out:** the **"Order Support"** button is NOT in the top bar (it's inside `<AuthorizeView>`).
- **Signed in:** it appears next to the theme toggle. Click it → a `Dialog` opens with the chat + the two
  starter-prompt chips ("Where's my order?", "Find me a desk lamp").

## 2. ★ #1 RISK — token actually reaches the endpoint (no model needed)
The Fake provider is the Development default, so you can prove the **transport + auth** before wiring any
model. Type anything and send.
- **Expected:** an assistant reply `"Support is running in local (Fake) mode — no live model is
  configured."` streamed into the bubble.
- **What it proves:** the browser → circuit → `AGUIChatClient` → gateway `/storefront/agent` → AG-UI SSE
  path works AND the per-circuit bearer reached the endpoint (a 401 / "session expired" Notice instead
  means the `BearerTokenHandler`/`AgentChatClientFactory` scope wiring needs a fix — this is the item
  flagged `[~]` in C4). Confirm via the browser devtools/Network or the gateway/service logs that the
  `/storefront/agent` POST carried `Authorization: Bearer …` and returned 200.

## 3. Real replies + tools (needs a model)
Point the agent at a real model — Foundry Local is on-brand:
- Start Foundry Local; set on the **storefront** service (user-secrets or env):
  `SupportAgent:Provider=FoundryLocal`, `SupportAgent:Endpoint=<foundry-local OpenAI-compatible URL>`,
  `SupportAgent:ApiKey=<key>`, `SupportAgent:Model=<model id>`. (Azure cloud = same keys, `Provider=AzureFoundry`.)
- **Place an order first** as the signed-in user (Storefront → cart → checkout) to get a real order id.
- Ask **"what's the status of order #<that id>?"** → the agent calls `GetOrderStatus` and answers with the
  **honest** derived status: `Order #N — Confirmed. Placed <date>, <k> item(s), total <$>.` (no invented
  Shipped/Delivered). A tool card (running → done) should show.
- Ask **"do you have a desk lamp?"** → `FindProduct` returns name + price matches.
- **User-scoping:** ask for an order id you know belongs to a *different* user → "couldn't find an order
  #N on your account" (the sproc filters by `@UserName`).

## 4. Step-up MFA gate
Default is `SupportAgent:StepUp:Enabled=false` (authenticated is enough). To exercise the gate:
- `SupportAgent:StepUp:Enabled=true`, `Simulate=true` → authenticated user still gets in (dev escape hatch).
- `Enabled=true`, `Simulate=false`, and a token WITHOUT an `amr`/`acr` step-up claim → the endpoint returns
  **403** and `<AgentChat>` should surface a "verify to continue" style Notice rather than erroring.
  (Locally, a Keycloak step-up/OTP flow stamping `acr=mfa` — or Entra `amr=mfa` in the cloud — satisfies it.)
  The policy logic itself is already unit-tested (`StepUpMfaHandlerTests`, 9 cases); this confirms the
  HTTP wiring.

## 5. Look & feel
- Light + dark: the chat bubbles, tool cards, input, and starter chips use design tokens — check contrast
  and the streaming caret (disabled under `prefers-reduced-motion`). Capture a couple of screenshots into a
  dated folder here (mirror Run 2's `2026-07-01/`).

## If something's off
- Endpoint/route: service maps `/storefront/agent`; gateway forwards it via the existing
  `/storefront/{**catch-all}` route (no dedicated route was added).
- Wiring lives in: `src/Atrium.Services.Storefront/Support/` (agent/tools/policy/provider),
  `src/Atrium.Design/{AgentChat.razor, AgentChatClientFactory.cs, BearerTokenHandler.cs}`,
  `src/Atrium.Portal/Components/Layout/AssistantLauncher.razor`.
