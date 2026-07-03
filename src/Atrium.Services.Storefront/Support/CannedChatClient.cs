using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// A deterministic in-service <see cref="IChatClient"/> that ignores the prompt and returns a fixed
/// assistant message. It is the <c>Fake</c> provider: the Development default (see
/// <see cref="SupportAgentBuilderExtensions"/>) so the service boots and the test gate runs
/// with no AI endpoint, key, or network. Swapping to a real model is config-only.
/// </summary>
/// <remarks>
/// The optional <paramref name="reply"/> lets tests inject a specific canned response — most usefully
/// <c>"ALLOW"</c> to create a permissive guardrail classifier stub without an additional test double.
/// The parameterless default reproduces the original behaviour exactly.
/// </remarks>
internal sealed class CannedChatClient(
    string reply = "Support is running in local (Fake) mode — no live model is configured."
) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
