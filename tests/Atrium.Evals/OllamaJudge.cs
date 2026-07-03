using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using OpenAI;

namespace Atrium.Evals;

internal static class OllamaJudge
{
    private const string Endpoint = "http://localhost:11434/v1";
    private const string JudgeModel = "qwen2.5:14b-instruct"; // JUDGE_MODEL from Task 1.1

    public static ChatConfiguration Configuration()
    {
        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri(Endpoint) }
        );
        IChatClient judge = client.GetChatClient(JudgeModel).AsIChatClient();
        return new ChatConfiguration(judge);
    }

    public static async Task<bool> UpAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var r = await http.GetAsync("http://localhost:11434/api/tags");
            return r.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
