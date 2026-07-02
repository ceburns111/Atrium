# Atrium — working guide for Claude

## ⚠️ Active work (remove this section when merged): AI chat enhancements

Branch: `feat/ai-chat-enhancements`.

**Before proposing or changing anything on this work, READ the design doc — it is the source of truth. Do not re-derive or re-litigate settled decisions from a blank slate:**

- Design (authoritative): `docs/superpowers/specs/2026-07-02-ai-chat-enhancements-design.md`
- Plan (execute this, task by task): `docs/superpowers/plans/2026-07-02-ai-chat-enhancements.md`

**Locked decisions — do NOT re-open unless the user explicitly asks:**

- **Ollama-only** local models (Langfuse was considered and deliberately dropped).
- **Right-sized multi-model:** Qwen 7–9B chat + tools · independent larger judge · ~3B guardrail classifier.
- **Observability:** OTel GenAI spans → the **Aspire dashboard**. Langfuse/App Insights are an honest *"would-export"* talking point (OTLP is vendor-neutral), **not built** — say "would," not "does."
- **Evals:** `Microsoft.Extensions.AI.Evaluation` in `tests/Atrium.Evals`, judge on Ollama. No hosted eval platform.
- **Feedback:** telemetry-only (OTel span + structured log), **no DB / no persistence**.

**Execute via `superpowers:subagent-driven-development`**, starting at the first unchecked `- [ ]` task in the plan. See the `ai-chat-enhancements-run` memory for the same pointers.
