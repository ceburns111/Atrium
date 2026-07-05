using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using OpenAI;

namespace Atrium.Evals;

internal static class OllamaJudge
{
    /// <summary>The judge model (independent of and larger than the chat model). Override with <c>EVAL_JUDGE_MODEL</c>.</summary>
    public static string JudgeModel { get; } =
        Environment.GetEnvironmentVariable("EVAL_JUDGE_MODEL") ?? "qwen2.5:14b-instruct"; // JUDGE_MODEL from Task 1.1

    public static ChatConfiguration Configuration()
    {
        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri(OllamaConnection.OpenAIV1) }
        );
        IChatClient judge = client.GetChatClient(JudgeModel).AsIChatClient();
        return new ChatConfiguration(judge);
    }

    /// <summary>
    /// The skip gate: true only when the Ollama daemon is reachable AND both required models (chat +
    /// judge) are actually pulled. Checking <c>/api/tags</c> for the model names — not just daemon
    /// liveness — makes a missing model a SKIP (like Ollama being down) instead of a mid-test failure.
    /// </summary>
    public static async Task<bool> UpAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var json = await http.GetStringAsync($"{OllamaConnection.Root}/api/tags");
            using var doc = JsonDocument.Parse(json);
            var available = doc
                .RootElement.GetProperty("models")
                .EnumerateArray()
                .Select(m => m.GetProperty("name").GetString())
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return HasModel(available, SupportEvalHarness.ChatModel)
                && HasModel(available, JudgeModel);
        }
        catch
        {
            return false;
        }
    }

    // /api/tags reports fully-tagged names ("qwen2.5:14b-instruct", "llama3.2:latest"); accept an
    // untagged required model as its ":latest" tag.
    private static bool HasModel(HashSet<string> available, string model) =>
        available.Contains(model)
        || (!model.Contains(':') && available.Contains($"{model}:latest"));
}
