namespace Atrium.Evals;

/// <summary>
/// Single source of truth for the local Ollama endpoint used by the evals. Override with the
/// <c>OLLAMA_ENDPOINT</c> environment variable (e.g. a remote box or a non-default port); defaults to
/// the standard local daemon.
/// </summary>
internal static class OllamaConnection
{
    public static string Root { get; } =
        (Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434").TrimEnd(
            '/'
        );

    /// <summary>Ollama's OpenAI-compatible surface, used by both the chat client and the judge.</summary>
    public static string OpenAIV1 => $"{Root}/v1";
}
