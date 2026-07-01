using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Atrium.UnitTests.Support;

/// <summary>
/// A deterministic <see cref="IChatClient"/> that ignores the prompt and always returns a fixed
/// assistant message. This lets Microsoft Agent Framework agents run in unit tests with no real
/// model, API key, or network — keeping the test gate fully deterministic.
/// </summary>
/// <remarks>
/// Later chatbot items reuse this fake (e.g. to assert tool-calling behaviour), so it lives in a
/// shared <c>Support</c> namespace rather than inside a single test class.
/// </remarks>
public sealed class FakeChatClient : IChatClient
{
    /// <summary>The canned assistant text this client returns for every request.</summary>
    public const string CannedResponse = "PONG from the fake model.";

    private readonly string _reply;

    public FakeChatClient(string reply = CannedResponse) => _reply = reply;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _reply)));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, _reply);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
