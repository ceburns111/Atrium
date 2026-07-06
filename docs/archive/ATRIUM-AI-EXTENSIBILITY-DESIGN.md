# Atrium AI Extensibility — Design

> How teams that already ship an Atrium **Module (RCL)** + **Service (vertical)** add
> **chat and event-driven AI** the same way they add everything else: a project reference,
> a few registration lines, and **zero host edits**. Built on the **Microsoft Agent Framework
> (MAF)** as a thin convention layer.

Status: **Design (v1), partially shipped** · Date: 2026-07-01

> **2026-07-03:** the chat seam described here is real — `AgentSurface` on `IModule`, the shell's
> `AssistantLauncher`, the `AgentChat` primitive, and the Storefront Support agent all shipped (see
> `superpowers/specs/2026-07-02-ai-chat-enhancements-design.md` for the hardening that followed). The
> event-driven/workflow parts remain forward-looking design.

---

## 1. Goal & guiding principle

Atrium already has two extensibility seams:

| Seam | Mechanism | Drop-in cost |
|---|---|---|
| **UI Module** | `IModule` in an RCL, reflection-discovered by `ModuleLoader.Discover()`, self-registers via `RegisterServices(...)`, contributes `NavItems` | project reference + one type |
| **Service vertical** | ASP.NET project added in `apphost.cs` (`AddProject<>` + `WithReference`), own DB, behind the YARP gateway, trusts Keycloak JWTs | project + apphost line |

AI is a **third capability layered onto those two seams, not a new platform.** A team's AI
work splits along the seam it already owns:

- the **agent/workflow runtime + tools** live in the team's **Service** (where the domain data and authz already are), and
- the **chat surface** is contributed by the team's **Module** and rendered by the shell.

**Guiding principle: convention, not abstraction.** Teams write idiomatic MAF
(`AIAgent`, `WorkflowBuilder`, `[Description]` tool methods). Atrium contributes the
plumbing — identity, transport, telemetry, discovery — and nothing more. This is the same
philosophy that makes an `IModule` "wired exactly like first-party code."

### Locked decisions

| # | Decision | Choice | Why |
|---|---|---|---|
| 1 | Where the agent runtime lives | **In the owning Service vertical** | Co-located with domain data, tools, and authz; mirrors "each UI paired with its own app API." Keeps AI compute out of the shared Blazor host. |
| 2 | How much Atrium abstracts MAF | **Thin convention layer** | Teams use MAF docs/samples directly; Atrium wires plumbing, doesn't own a wrapper over a fast-moving framework. AG-UI already provides engine-swappability at the *protocol* layer. |
| 3 | Developer-facing shape | **Registration-builder (per-capability verbs)** | Matches how services already wire themselves imperatively; each agent/workflow/automation is a small, independently testable unit; "chat or event-based" is a one-line choice. |
| 4 | Chat surface discovery/presentation | **Module declares `AgentSurfaces`; shell renders** | Exact parallel to `NavItems`; gives a consistent context-aware launcher *and* inline embedding; all rendering in one `Atrium.Design` primitive. |
| 5 | Event-automation trigger | **v1: in-process, own-service; upgrade to v3: outbox + bus** | YAGNI now, no new infra; the transport-free contract lets the mechanism change later without touching team code. |

---

## 2. Topology

```
browser ──SignalR──▶ Atrium.Portal (Blazor Server)
                       ├─ shell renders assistant launcher from IModule.AgentSurfaces
                       └─ <AgentChat> (Atrium.Design)
                              │ HTTP + user bearer (same path as module HttpClients)
                              ▼
                          Atrium.Gateway (YARP)
                              │ route: /{basePath}/agent  (auto-registered by convention)
                              ▼
   Atrium.Services.<Team>   ◀── agent runtime lives HERE (decision A)
     ├─ builder.AddAtriumAI().AddChatAgent<>() / .AddWorkflow<>() / .AddEventAutomation<>()
     ├─ MAF: AIAgent + WorkflowBuilder graphs + [Description] tool methods
     ├─ MapAgui → AG-UI SSE endpoint (auto-wired by AddChatAgent)
     └─ in-process event dispatcher → automations (v1)
```

### New / changed projects

| Project | Change | References MAF? |
|---|---|---|
| **`Atrium.AI`** (new) | Service-side convention layer: `AddAtriumAI()` builder, capability base types, AG-UI hosting, in-process event dispatcher | Yes |
| **`Atrium.Abstractions`** | Add `AgentSurface` record next to `NavItem`; add `IModule.AgentSurfaces` (default empty) | No (UI-facing, MAF-free) |
| **`Atrium.Design`** | Add `<AgentChat>` primitive wrapping the .NET AG-UI client | No |

`Atrium.Abstractions` stays MAF-free so UI modules can declare a chat surface without taking
a dependency on the agent framework.

---

## 3. The convention layer — `Atrium.AI`

`builder.AddAtriumAI()` installs the defaults so teams never wire plumbing:

- **Model client** resolved from central config (Azure OpenAI / Microsoft Foundry endpoint + model), with a per-service override. Exposed as `IChatClient` (MEAI), so provider swaps are a config change.
- **Identity bridge** — the signed-in user's Keycloak bearer flows into the agent's tool-execution context.
- **OpenTelemetry** spans on every model call, tool invocation, and workflow hop — feeds the existing Serilog + OTel work.
- **AG-UI hosting** — `MapAgui` wiring for any registered chat agent.

It returns a builder exposing three verbs. **Chat vs event-based is simply which verb you call:**

```csharp
// In Atrium.Services.<Team>/Program.cs
builder.AddAtriumAI()
       .AddChatAgent<OrderHelpAgent>()         // interactive, auto-exposed over AG-UI
       .AddWorkflow<OrderTriageWorkflow>()     // a MAF graph, reusable by chat/automation/direct
       .AddEventAutomation<LowStockReorder>(); // headless, triggered by a domain event
```

---

## 4. Service side — the three capabilities

### 4.1 Chat agent
A class the team authors as a normal MAF `AIAgent` / `ChatClientAgent`, with tools as plain
C# methods:

```csharp
public sealed class OrderHelpAgent(IChatClient chat, IOrderRepository orders)
{
    [Description("Look up an order's current status by id.")]
    public Task<OrderStatus> GetOrderStatusAsync(string orderId) => orders.GetStatusAsync(orderId);

    // agent instructions + tool registration provided via the base/convention
}
```

`AddChatAgent<T>()` auto-exposes it at **`/{basePath}/agent`** over AG-UI (`MapAgui`) and
registers the gateway route by convention. **The team writes no endpoint code.**

### 4.2 Workflow
A MAF `WorkflowBuilder` graph — typed executors, conditional edges, superstep (Pregel/BSP)
execution, checkpointing, and human-in-the-loop. Callable from a chat agent, an automation,
or directly. **This is the deterministic, regulated-friendly path.**

```csharp
var builder = new WorkflowBuilder(classify);
builder.AddEdge(classify, triageHuman, condition: m => m.RiskScore >= 0.8);
builder.AddEdge(classify, autoApprove,  condition: m => m.RiskScore <  0.8);
var workflow = builder.Build();
```

Executors may be custom C# logic **or an `AIAgent` dropped in as a node**, mixing
deterministic code and LLM steps in one graph.

### 4.3 Event automation
Declares *"run this workflow/agent when domain event X occurs."* **The contract hides the
transport** — the team names the *event*, never a queue:

```csharp
public sealed class LowStockReorder : IEventAutomation<InventoryLowEvent>
{
    public Task RunAsync(InventoryLowEvent e, IWorkflowRunner runner, CancellationToken ct)
        => runner.RunAsync<ReorderDraftWorkflow>(e, ct);
}
```

v1 dispatches **in-process within the owning service**. Runs headless under a service
identity (no user present).

---

## 5. UI side

`IModule` gains one optional member, parallel to `NavItems`:

```csharp
// Atrium.Abstractions
public sealed record AgentSurface(
    string Name,               // "Order Assistant"
    string Endpoint,           // "/storefront/agent"  (through the gateway)
    string[]? StarterPrompts = null,
    string? Icon = null);

public interface IModule
{
    // ...existing members...
    IEnumerable<AgentSurface> AgentSurfaces => [];   // default: none
}
```

- The **shell** reads `AgentSurfaces` from every discovered module and renders a
  **context-aware assistant launcher** (app-bar entry that targets the active module's agent).
- The shared **`<AgentChat>`** primitive (Atrium.Design) wraps the .NET AG-UI client
  (`AGUIChatClient : IChatClient`), reusing the module's gateway + bearer pattern and the
  existing `ThrowIfSessionExpired()` convention.
- Modules may also **embed `<AgentChat>` inline** on any of their own pages (e.g. an
  "explain this order" panel), reusing the same primitive.

No team writes chat UI or transport wiring.

---

## 6. Identity & security

- **Interactive (chat):** user bearer flows Portal → gateway → service; agent tools run
  under that user's authorization. MAF **tool middleware** is the guardrail seam
  (authorization + anti-context-poisoning) — optional, per team.
- **Headless (automation):** runs under a dedicated Keycloak **service account** with scoped
  permissions.
- **Regulated:** workflow **checkpoints** become HITL approval gates, surfaced back to the
  user through `<AgentChat>`.

---

## 7. AppHost wiring

An AI-enabled service needs only its normal registration plus a reference to the
model-provider resource:

```csharp
var storefront = builder
    .AddProject<Projects.Atrium_Services_Storefront>("storefront")
    .WithReference(storefrontDb)
    .WithReference(catalog)
    .WithReference(keycloak)
    .WithReference(foundry);   // ← the only AI-specific line
```

**No special host code. Event automations (v1) need no infrastructure.**

---

## 8. Error handling

| Path | Behavior |
|---|---|
| Chat | AG-UI error events rendered in `<AgentChat>`; session expiry reuses the existing pattern (`ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`). |
| Automation | Failures logged + bounded retry; dead-lettering arrives with v3 (bus). |
| Workflow | Checkpoint enables resume after failure. |

---

## 9. Testing

- **Tools** — plain methods; unit test directly.
- **Workflow** — `InProcessExecution.RunAsync(...)` with fake executors, asserting the event stream.
- **Automation** — raise the domain event, assert the workflow ran.
- **AG-UI endpoint** — integration test mirroring the existing `Atrium.Services.*.Tests` shape.

---

## 10. Out of scope for v1 (YAGNI)

Message bus / transactional outbox, cross-service automations, multi-model routing,
agent registry/marketplace, generative-UI components beyond text + tool cards.

---

## 11. The v1 → v3 upgrade seam

Because `AddEventAutomation<T>()` **hides the transport**, replacing the in-process
dispatcher with **transactional outbox + bus** later is an internal change inside
`Atrium.AI` — **no team's automation code changes.** That transport-free contract is the
entire reason v1 can be small without foreclosing the regulated-grade path.
