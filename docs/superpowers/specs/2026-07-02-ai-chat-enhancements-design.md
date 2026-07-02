# AI chat enhancements — design

Date: 2026-07-02 · Status: draft for review · Target demo: interview ~2026-07-07

## Context

Atrium has one MAF agent — the Storefront **Support** agent (`ChatClientAgent` over a config-selected
`IChatClient`), streaming over AG-UI/SSE, with two request-scoped tools (`GetOrderStatus`,
`FindProduct`) and a groundedness instruction ("only state facts the tools return"). Telemetry infra
exists (`Atrium.ServiceDefaults` → OTLP → Aspire dashboard) but **no GenAI/agent spans**. No eval,
guardrails, caching, or user feedback.

This design adds five capabilities framed as Chip Huyen's *AI Engineering* Chapter-10 architecture,
realized as composable `IChatClient` middleware. Goal: high-ROI, interview-flashy, honestly scoped —
each layer earns its place; nothing built that the demo doesn't show.

## Goals

- Live, visible **observability** of agent turns, model calls (tokens/latency), and tool calls.
- An **evaluation harness** (AI-as-judge) proving answer quality — `Microsoft.Extensions.AI.Evaluation`
  in the test suite, judge model on Ollama, published as an HTML scorecard.
- **Guardrails** (input screening) demonstrated live ("watch it refuse a jailbreak").
- **User feedback** (thumbs) wired to the trace — the data-flywheel story.
- **Caching** for cost/latency, visible in traces.
- Run entirely **locally on Ollama** with right-sized models.

## Non-goals (deliberately out of scope — knowing when to stop is the point)

RAG / vector store (tool-grounding already beats RAG here), model router by turn-complexity, semantic
caching, finetuning, persistent cross-session memory, multi-agent orchestration, Azure/Foundry-Local
providers, **hosted eval/observability platforms (Langfuse/Phoenix)** — Aspire + in-code eval cover the
demo with zero extra infra. Named as deliberate scope cuts, not gaps.

## Key decisions

1. **Runner: Ollama only.** Best Apple-Silicon performance (Metal/llama.cpp) and the widest
   tool-calling model catalog. Code is already OpenAI-compatible, so this is a provider/config change.
2. **Right-sized multi-model** (a model per job, not one big model):
   | Job | Model (target) | Why |
   |---|---|---|
   | Chat + tools | current Qwen instruct, **7–9B**, Q4_K_M | Qwen dominates local tool-calling 2026; Q4_K_M is the reliability floor. Snappy on M1 Max (~35–55 tok/s). |
   | Eval judge (#3) | independent **14–32B** (Qwen/Llama) | Separate judge avoids self-preference bias (Huyen Ch.3). Offline, so slowness is fine. |
   | Guardrail classifier (#4) | **~3B** (Llama Guard / Prompt Guard / Qwen-3B) | Cheap, fast input screen before the expensive model. |
   Exact tags confirmed at execution via `ollama pull` + a tool-calling smoke check.

   <!-- Confirmed Ollama model tags (Task 1.1, 2026-07-02) — use verbatim as config values:
        CHAT_MODEL      = qwen2.5:7b-instruct   (tool-calling smoke test passed: emitted GetOrderStatus{"orderId":1234})
        GUARDRAIL_MODEL = llama3.2:3b
        JUDGE_MODEL     = qwen2.5:14b-instruct -->

3. **Hardware envelope:** M1 Max / 32 GB — keep chat + guardrail resident; load the judge only during
   eval runs.
4. **Fully local observability + eval — no hosted platform.** OTel GenAI spans → the **Aspire
   dashboard** (already running); evals via **`Microsoft.Extensions.AI.Evaluation`** in the test suite
   with a **local Ollama judge**. No Langfuse/Phoenix: zero extra infra, nothing leaves the machine,
   and it's the strongest .NET-native story. The instrumentation is vendor-neutral OTel, so shipping
   the same spans to Langfuse/App Insights later is an exporter swap — a talking point, not built here.

## Architecture

### The IChatClient middleware pipeline (the spine)

`ChatClientAgent` auto-inserts `FunctionInvokingChatClient` (`WithDefaultAgentMiddleware`), so tools
work without us adding `.UseFunctionInvocation()`. We rebuild the registered `IChatClient` as a
pipeline (today it's a bare `.AsIChatClient()`):

```csharp
// BuildChatClient(...) in SupportAgentBuilderExtensions
IChatClient inner = ollamaOpenAiCompatibleClient;         // Ollama /v1
IChatClient pipeline = new ChatClientBuilder(inner)
    .UseDistributedCache(cache)                            // #5  innermost (caches real model calls)
    .Use((c, sp) => new GuardrailChatClient(c, classifier))// #4  outside cache: a block never hits model
    .UseOpenTelemetry(sourceName: AtriumGenAiSource,       // #1  outermost: measures everything
                      configure: o => o.EnableSensitiveData = true /* demo-only: logs prompts */)
    .Build();
```

Agent-level spans are separate: wrap the agent with `.WithOpenTelemetry()` (MAF source
`AgentOpenTelemetryConsts.DefaultSourceName`). `Atrium.ServiceDefaults` registers **both** sources on
the TracerProvider so the trace shows: *agent turn → model call (tokens) → tool exec → model call →
answer*.

- Ordering rationale confirmed from `dotnet/extensions`: last `.Use` = outermost.
- `EnableSensitiveData = true` is a **demo-only** choice (prompts/responses in traces) — itself a
  talking point about PII-in-telemetry; would be gated off in prod.

### Observability sink (Aspire)

Instrument once (OTel GenAI conventions), export to the **Aspire dashboard** via the existing
`UseOtlpExporter()` (`OTEL_EXPORTER_OTLP_ENDPOINT`, Aspire-injected; inert in tests — no change to the
current export path). Because the spans are vendor-neutral OTel, the *same* data could ship to
Langfuse/Phoenix/App Insights by adding an exporter — mentioned as a talking point, not built.

### Provider rework (Ollama)

Add an `Ollama` provider to `BuildChatClient`: OpenAI-compatible client at
`http://localhost:11434/v1` (dummy key), model from `SupportAgent:Model`. Existing Fake path stays as
the test/dev default. `SupportAgent:GuardrailModel` and eval judge model are separate config keys.

## Per-feature design

### #1 Observability (Ch.10 monitoring) — S effort, high flash
- Chat pipeline `.UseOpenTelemetry()` + agent `.WithOpenTelemetry()`; register both sources + a Meter
  for metrics in `ServiceDefaults` (currently tracing-only).
- Demo: "where's my order 1234?" → the Aspire trace waterfall with token counts, per-call latency, and
  the tool-call span (which tool, args, duration → SQL).
- Answers "MAF Langfuse equivalent?": OTel GenAI conventions are the equivalent — the *same* spans
  land in Aspire today (demonstrated) and **would** land in Langfuse/App Insights by adding an OTLP
  exporter (a few lines + that backend's endpoint/auth). Framed as architectural inference, **not**
  tested here — say "would," not "does"; landing ≠ rich LLM-specific rendering in every backend.

### #2 Feedback → OTel event (Ch.10 user feedback) — XS effort
- Thumbs up/down per assistant message in `AgentChat.razor`; a small authenticated endpoint records it
  as an **OTel event/tag on the conversation trace** (visible in Aspire) plus a structured log.
  Requires surfacing a correlation/trace id to the client (see risks).
- Talking point: the data flywheel — thumbs-down turns become candidate items for the #3 eval dataset,
  closing the loop from real usage back into the offline quality gate.

### #3 Eval harness / AI-as-judge (Ch.3–4) — M effort, high talking-point
- **`Microsoft.Extensions.AI.Evaluation`** (packages `.Quality`, `.Reporting`, `.Console`) in the
  test suite — evals-as-tests, fully offline, versioned in the repo, runnable as a CI/build step.
- A dataset of ~6–10 support scenarios scored by the **independent Ollama judge** on **Groundedness /
  Relevance / TaskAdherence**, plus **`ToolCallAccuracy` + `IntentResolution`** (the agent evaluators
  purpose-built for tool use: "where's my order" ⇒ resolves intent ⇒ calls `GetOrderStatus` with the
  right args). Per-scenario **reasoning**, not just pass/fail.
- Emits the **HTML report** (via `.Reporting` / the `.Console` tool) as the shareable scorecard;
  trends run-over-run. Gated so CI without Ollama skips.

### #4 Guardrails (Ch.5, Ch.10) — S–M effort, high flash
- One `GuardrailChatClient : DelegatingChatClient` in the pipeline. **Input**: the 3B classifier
  screens for prompt-injection / off-topic; on block, short-circuit with a canned refusal (never
  calls inner → no model/cache cost). **Output** (optional/stretch): stay-on-topic check.
- Demo: attempt a jailbreak / off-topic ask → visible refusal; the block shows in the trace.

### #5 Caching (Ch.9–10) — XS effort
- `.UseDistributedCache()` with an in-memory `IDistributedCache` (demo) — exact-match. Pairs with #1:
  show a repeated question returning at ~0ms with a cache-hit span; note cost avoided.

## Build sequence (phases → checkpoints)

1. **Provider + models** — Ollama provider, pull chat/guardrail/judge models, tool-calling smoke test.
   (Unblocks everything real.)
2. **#1 Observability** — pipeline + agent OTel + source registration (Aspire sink). (Establishes the
   pipeline seam.)
3. **#5 cache → #4 guardrails** — decorators on that pipeline.
4. **#2 feedback** — thumbs UI + endpoint recording an OTel event + trace-id surfacing.
5. **#3 eval harness** — `Microsoft.Extensions.AI.Evaluation` project/tests + HTML report. Independent
   of the pipeline, so it can run parallel to 3–4.

## Verification

- Per phase: build clean + tests green (existing 77 + new).
- #1/#4/#5: launch via Aspire, drive the chat with Playwright, **screenshot the Aspire trace** showing
  spans/tokens/tool-calls, a guardrail block, and a cache hit — evidence, not assertion.
- #2: screenshot a thumbs-down surfaced as an OTel event on its trace.
- #3: run the eval, **attach the HTML scorecard**; the run is green in the test suite.
- New bUnit tests for the thumbs UI; unit tests for `GuardrailChatClient` (block vs pass-through).

## Risks / open questions (resolve during planning research)

- Exact current Qwen tag + confirmed tool-calling on Ollama (pull + smoke).
- `Microsoft.Extensions.AI.Evaluation` exact evaluator/report API + package versions on net10; whether
  the agent evaluators (`ToolCallAccuracy`/`IntentResolution`/`TaskAdherence`) need the full agent
  message trace as input and how to feed a local Ollama judge as the `ChatConfiguration`.
- Whether guardrail short-circuit composes cleanly with streaming (`GetStreamingResponseAsync`) and
  MAF's function-invocation layer — verify block path returns a well-formed streamed refusal.
- Feedback→trace correlation: how to tie a thumbs event to the originating agent turn's trace/span id
  across the SSE boundary (may need to surface a correlation id to the client).
- Meter/metrics wiring in `ServiceDefaults` (currently tracing-only).
