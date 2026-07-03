using System.ClientModel;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Atrium.Evals;

// Mirrors the Support agent's brain — the Ollama chat model + function invocation + the two tools —
// so evaluators see the real tool calls. Tools return fixed fake data (the eval judges behaviour, not data).
internal static class SupportEvalHarness
{
    private const string ChatModel = "qwen2.5:7b-instruct"; // CHAT_MODEL from Task 1.1
    private const string Instructions =
        "You are Atrium's order-support assistant. Use GetOrderStatus to look up an order by id and "
        + "FindProduct to search the catalog. Only state facts the tools return. Be concise.";

    public static readonly List<AITool> Tools =
    [
        AIFunctionFactory.Create(GetOrderStatus),
        AIFunctionFactory.Create(FindProduct),
    ];

    public static (IChatClient Client, List<ChatMessage> System) Build()
    {
        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri("http://localhost:11434/v1") }
        );
        IChatClient chat = client
            .GetChatClient(ChatModel)
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        return (chat, [new(ChatRole.System, Instructions)]);
    }

    [Description("Look up the status of one of the signed-in customer's orders by its id.")]
    private static string GetOrderStatus(int orderId) =>
        orderId == 1234
            ? "Order 1234: Confirmed, placed 2026-06-30, 2 items, $58.00."
            : $"No order {orderId} found.";

    [Description("Find products in the catalog by name.")]
    private static string FindProduct(string query) =>
        query.Contains("lamp", StringComparison.OrdinalIgnoreCase)
            ? "Desk Lamp — $24.00"
            : "No matches.";
}
