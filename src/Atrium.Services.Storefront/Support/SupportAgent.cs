using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// The Storefront order-support agent: a Microsoft Agent Framework <see cref="ChatClientAgent"/> over
/// the configured <see cref="IChatClient"/> (Fake / FoundryLocal / AzureFoundry — see
/// <see cref="SupportAgentServiceCollectionExtensions"/>), with the two <see cref="SupportTools"/>
/// registered so the model can look up orders and products. Scoped, because its tools read the
/// signed-in caller from the current request.
/// </summary>
public sealed class SupportAgent
{
    private const string Instructions =
        "You are Atrium's order-support assistant for signed-in customers. Help with questions "
        + "about their orders and the product catalog. Use the GetOrderStatus tool to look up an "
        + "order by id, and the FindProduct tool to search the catalog by name. Only state facts the "
        + "tools return — never invent order progress (such as shipped or delivered) that the tools "
        + "did not report. Be concise and friendly.";

    private readonly AIAgent _agent;

    public SupportAgent(IChatClient chatClient, SupportTools tools)
    {
        _agent = new ChatClientAgent(
            chatClient,
            instructions: Instructions,
            name: "Order Support",
            tools:
            [
                AIFunctionFactory.Create(tools.GetOrderStatus),
                AIFunctionFactory.Create(tools.FindProduct),
            ]
        );
    }

    /// <summary>Drives one support turn and returns the agent's reply.</summary>
    public Task<AgentResponse> RunAsync(
        string message,
        CancellationToken cancellationToken = default
    ) => _agent.RunAsync(message, cancellationToken: cancellationToken);
}
