using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// The Storefront order-support agent: a Microsoft Agent Framework <see cref="ChatClientAgent"/> over
/// the configured <see cref="IChatClient"/> (Fake / Ollama / FoundryLocal / AzureFoundry — see
/// <see cref="SupportAgentBuilderExtensions"/>), with the two <see cref="SupportTools"/>
/// functions registered so the model can look up orders and products.
/// </summary>
/// <remarks>
/// AG-UI's <c>MapAGUI</c> captures a single agent instance for the endpoint's lifetime, so this agent is
/// effectively a singleton and must not capture the request-scoped <see cref="SupportTools"/> (whose
/// order repository owns a per-request <c>SqlConnection</c>). Instead each tool invocation resolves a
/// fresh <see cref="SupportTools"/> from the <em>current</em> request scope
/// (<see cref="HttpContext.RequestServices"/>), so the tools see the signed-in caller and that caller's
/// own scoped services — correct under concurrency. The <c>[Description]</c> on each tool method (read by
/// <see cref="AIFunctionFactory"/>) still drives tool selection.
/// </remarks>
public sealed class SupportAgent
{
    /// <summary>
    /// The agent's name. It is both the AG-UI keyed registration name (see
    /// <see cref="SupportAgentBuilderExtensions"/>) and the <see cref="ChatClientAgent.Name"/>;
    /// the MAF hosting factory asserts these match, so they are pinned to this one constant.
    /// </summary>
    public const string AgentName = "Support";

    // Internal (not private) so the eval harness (InternalsVisibleTo Atrium.Evals) scores the REAL
    // deployed system prompt — any edit here is automatically what the evals certify.
    internal const string Instructions =
        "You are Atrium's order-support assistant for signed-in customers. Help with questions "
        + "about their orders and the product catalog. Use the GetOrderStatus tool to look up an "
        + "order by id, and the FindProduct tool to search the catalog by name. Only state facts the "
        + "tools return — never invent order progress (such as shipped or delivered) that the tools "
        + "did not report. Be concise and friendly.";

    private readonly AIAgent _agent;

    public SupportAgent(IChatClient chatClient, IHttpContextAccessor httpContextAccessor)
    {
        _agent = new ChatClientAgent(
            chatClient,
            instructions: Instructions,
            name: AgentName,
            tools:
            [
                ToolFor(nameof(SupportTools.GetOrderStatus), httpContextAccessor),
                ToolFor(nameof(SupportTools.FindProduct), httpContextAccessor),
            ]
        )
            .AsBuilder()
            .UseOpenTelemetry() // emits agent-turn + tool-orchestration spans under OpenTelemetryConsts.DefaultSourceName
            // SupportAgent has no IServiceProvider here; MAF's AIAgentBuilder.Build(null) falls back to
            // an empty provider (OpenTelemetry agent middleware needs no DI services), so null! is intentional.
            .Build(null!);
    }

    /// <summary>
    /// The underlying MAF agent, exposed so the AG-UI hosting factory can register it as the keyed
    /// <see cref="AIAgent"/> the endpoint captures.
    /// </summary>
    public AIAgent Agent => _agent;

    /// <summary>Drives one support turn and returns the agent's reply.</summary>
    public Task<AgentResponse> RunAsync(
        string message,
        CancellationToken cancellationToken = default
    ) => _agent.RunAsync(message, cancellationToken: cancellationToken);

    // Build a tool from a SupportTools method whose target is resolved per invocation from the request
    // scope. Create(MethodInfo, createInstanceFunc) reads the method's [Description] for the tool schema
    // and calls the factory each time the tool runs, giving a fresh, correctly-scoped SupportTools.
    private static AIFunction ToolFor(string methodName, IHttpContextAccessor httpContextAccessor)
    {
        var method =
            typeof(SupportTools).GetMethod(methodName)
            ?? throw new InvalidOperationException($"SupportTools.{methodName} was not found.");

        return AIFunctionFactory.Create(method, _ => ResolveTools(httpContextAccessor));
    }

    private static SupportTools ResolveTools(IHttpContextAccessor httpContextAccessor)
    {
        var httpContext =
            httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "The support agent's tools require an active HTTP request."
            );

        return httpContext.RequestServices.GetRequiredService<SupportTools>();
    }
}
