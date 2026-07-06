using System.ClientModel;
using System.ComponentModel;
using System.Reflection;
using Atrium.Services.Storefront.Support;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Atrium.Evals;

// Composes the REAL Support agent brain: the production system prompt (SupportAgent.Instructions,
// internal + InternalsVisibleTo) and the production tool contracts (name + [Description] read straight
// off SupportTools), over the Ollama chat model with function invocation — so the scores certify the
// prompt and tool schema that are actually deployed, and neither can silently drift from production.
// Only the tool BODIES are fakes returning fixed data: the eval judges behaviour, not data access.
internal static class SupportEvalHarness
{
    /// <summary>The chat model under evaluation (CHAT_MODEL from Task 1.1). Override with <c>EVAL_CHAT_MODEL</c>.</summary>
    public static string ChatModel { get; } =
        Environment.GetEnvironmentVariable("EVAL_CHAT_MODEL") ?? "qwen2.5:7b-instruct";

    public static readonly List<AITool> Tools =
    [
        AIFunctionFactory.Create(
            GetOrderStatus,
            nameof(SupportTools.GetOrderStatus),
            ProductionToolDescription(nameof(SupportTools.GetOrderStatus))
        ),
        AIFunctionFactory.Create(
            FindProduct,
            nameof(SupportTools.FindProduct),
            ProductionToolDescription(nameof(SupportTools.FindProduct))
        ),
    ];

    public static (IChatClient Client, List<ChatMessage> System) Build()
    {
        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri(OllamaConnection.OpenAIV1) }
        );
        IChatClient chat = client
            .GetChatClient(ChatModel)
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        return (chat, [new(ChatRole.System, SupportAgent.Instructions)]);
    }

    // The [Description] the production model sees is the [Description] the eval model sees.
    private static string ProductionToolDescription(string methodName) =>
        typeof(SupportTools)
            .GetMethod(methodName)
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description
        ?? throw new InvalidOperationException(
            $"SupportTools.{methodName} was not found or has no [Description]."
        );

    // Fake tool bodies with the production signatures. Parameter names are part of the tool schema the
    // model sees, so they must match SupportTools exactly (orderId / query).
    private static string GetOrderStatus(int orderId) =>
        orderId == 1234
            ? "Order 1234: Confirmed, placed 2026-06-30, 2 items, $58.00."
            : $"No order {orderId} found.";

    private static string FindProduct(string query) =>
        query.Contains("lamp", StringComparison.OrdinalIgnoreCase)
            ? "Desk Lamp — $24.00"
            : "No matches.";
}
