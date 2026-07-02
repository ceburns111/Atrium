using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// A deterministic in-service <see cref="IChatClient"/> that ignores the prompt and returns a fixed
/// assistant message. It is the <c>Fake</c> provider: the Development default (see
/// <see cref="SupportAgentServiceCollectionExtensions"/>) so the service boots and the test gate runs
/// with no AI endpoint, key, or network. Swapping to a real model is config-only.
/// </summary>
internal sealed class CannedChatClient : IChatClient
{
    private const string Reply =
        "Support is running in local (Fake) mode — no live model is configured.";

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Reply)));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, Reply);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
