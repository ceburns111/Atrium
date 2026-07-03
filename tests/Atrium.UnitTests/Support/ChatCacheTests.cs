using Atrium.Services.Storefront.Support;
using Atrium.UnitTests.Support; // FakeChatClient
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Atrium.UnitTests.Support;

public class ChatCacheTests
{
    [Fact]
    public async Task Identical_requests_hit_the_cache_and_call_the_model_once()
    {
        var counting = new CountingChatClient(new FakeChatClient());
        IDistributedCache cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions())
        );
        IChatClient client = new ChatClientBuilder(counting).UseDistributedCache(cache).Build();

        var messages = new List<ChatMessage> { new(ChatRole.User, "hello") };
        await client.GetResponseAsync(
            messages,
            cancellationToken: TestContext.Current.CancellationToken
        );
        await client.GetResponseAsync(
            messages,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(1, counting.Calls);
    }

    [Fact]
    public async Task Production_pipeline_caches_identical_requests_through_the_real_factory_seam()
    {
        var counting = new CountingChatClient(new FakeChatClient());
        IDistributedCache cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions())
        );
        IServiceProvider services = new ServiceCollection().BuildServiceProvider();

        IChatClient client = SupportAgentBuilderExtensions.BuildSupportPipeline(
            counting,
            cache,
            services
        );

        var messages = new List<ChatMessage> { new(ChatRole.User, "hello from the seam test") };
        await client.GetResponseAsync(
            messages,
            cancellationToken: TestContext.Current.CancellationToken
        );
        await client.GetResponseAsync(
            messages,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(1, counting.Calls);
    }

    private sealed class CountingChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public int Calls { get; private set; }

        public override Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Calls++;
            return base.GetResponseAsync(messages, options, cancellationToken);
        }
    }
}
