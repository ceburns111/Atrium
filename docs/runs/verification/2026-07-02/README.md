# Live verification — Run 3 (support chatbot), 2026-07-02

Supervised live pass of the support-chatbot slice on `feat/support-chatbot`, driven through the Playwright
MCP against `aspire run`, with a **real model** wired in (local **Ollama**, `qwen3:14b-q4_K_M`, via the
`FoundryLocal` provider path — Ollama exposes the same OpenAI-compatible endpoint, so `SupportAgent`
consumed it unchanged: `Provider=FoundryLocal`, `Endpoint=http://localhost:11434/v1`). That config was
injected as temp `WithEnvironment` on the storefront resource in `apphost.cs` **and reverted after** — the
repo is clean.

Playbook followed: [`../RUN3-support-chatbot.md`](../RUN3-support-chatbot.md).

## Result: all 6 playbook checks ✅ (incl. step-up MFA) — no confirmed defect

Minor UX polish (not a bug): a step-up **403** is surfaced via the generic "The agent is unavailable" Notice,
because the client maps only **401** → the "session expired" Notice and everything else → the generic error.
A dedicated "verify to continue / step up" Notice for 403 would be a nice future touch; behaviour today is
graceful (no crash), just not step-up-specific.

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| 1a | Signed out → no "Order Support" button | ✅ | `run3-01` — only theme toggle + Sign in (button is inside `<AuthorizeView>`). Bonus: item A "1 of 3 modules visible" renders. |
| 1b | Signed in (`testuser`) → button appears; Dialog opens with chat + 2 starter chips | ✅ | `run3-02` — "Where's my order?" / "Find me a desk lamp" chips present. |
| 2 | **#1 RISK — bearer reaches `/storefront/agent`** (Fake provider, no model) | ✅ | `run3-03` — canned reply *"Support is running in local (Fake) mode…"* streamed. Independently proven: anonymous POST to `/storefront/agent` (gateway **and** direct) → **401**; signed-in via circuit → **200 streamed**. So the per-circuit `BearerTokenHandler`/`AgentChatClientFactory` wiring (flagged `[~]` in C4) **works**. |
| 3a | Real model reply + `GetOrderStatus` tool + honest status | ✅ | `run3-04` — placed order **#9002** ($79), asked status → **`GetOrderStatus` tool card (done)** → *"Order #9002 is Confirmed. Placed 7/2/2026, 1 item(s), total $79.00."* No invented Shipped/Delivered. |
| 3b | `FindProduct` tool + name/price match | ✅ | "Do you have a desk lamp?" → `FindProduct` **done**, honestly *"no desk lamps"* (name-substring search; no product literally named "desk lamp"). "Do you have any lamps?" → *"We have a **Task Lamp** for $79.00."* |
| 3c | **User-scoping** — order owned by another user (#2 belongs to `admin`) → not found | ✅ | `run3-06` — as `testuser`: *"I don't see an order #2 associated with your account."* The sproc filters by `@UserName`; a foreign order id is not readable. |
| 4 | Step-up MFA gate | ✅ | With `StepUp:Enabled=true, Simulate=false`, `testuser`'s password-only token (no `amr`/`acr` step-up claim) is **forbidden**: storefront request log shows `POST /storefront/agent responded 403`, and `<AgentChat>` surfaces a graceful Notice (no crash/spinner). Anonymous/expired → **401** ("session expired" Notice). The pass path (`Enabled=false` / `Simulate=true` / valid claim) is the same `context.Succeed` branch already exercised ~15× live and by `StepUpMfaHandlerTests` (9 cases), so no separate `Simulate=true` restart was run. |

Verified robustness: **~15 tool-calling turns** across several chats — including one sent after a **~2.5-min
idle** — all streamed to completion (tool cards + reply text) and re-enabled the composer. Confirmed with
temporary boundary instrumentation (portal `BearerTokenHandler`, a storefront `/storefront/agent`
middleware, and the client streaming loop): every turn logged a matching `IN`/`OUT` on the storefront and
`loop-start`/`loop-exit`/`finally` on the client, with zero errors. All instrumentation was **reverted**.

## Note on the "agent wedge" reported mid-session (retracted)

An earlier draft of this report described the agent "wedging after ~3 turns." **That was a false alarm in
the test harness, not an app defect.** The Playwright poller used the **Send** button's disabled state as
its "busy" signal, but Send is *correctly* disabled whenever the draft is empty
(`Disabled="@(_busy || string.IsNullOrWhiteSpace(_draft))"`, `AgentChat.razor`). After a turn completes the
draft is empty, so Send is disabled — which the poller misread as "still streaming." The true busy signal
is the **input** field (`disabled="@_busy"`); with that, every turn completes.

Root-cause investigation (systematic-debugging) — instrumenting portal → gateway → storefront → model and
the client loop — showed the server always completes (`OUT 200`, both model calls fire) and the client loop
always exits and resets `_busy`. One earlier single occurrence (Ollama silent for one turn, seen via
snapshot) **could not be reproduced** across thorough instrumented testing, so **no fix was applied**
(no fix without a confirmed, reproducible root cause).

## Environment
- Stack: `aspire run` (dashboard `https://atrium.dev.localhost:17250`), portal
  `https://portal-atrium.dev.localhost:7001`. Logins from the realm export: `testuser`/`password`,
  `admin`/`password`.
- Model: Ollama `qwen3:14b-q4_K_M` (tool-calling capable) at `http://localhost:11434/v1`.
- Note: the dashboard/portal OIDC redirect first returned **HTTP 431** (oversized accumulated cookies for
  the shared `localhost` host); clearing cookies fixed it. Not an app bug — a dev-browser artifact.
</content>
