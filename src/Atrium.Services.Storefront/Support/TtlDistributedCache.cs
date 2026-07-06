using Microsoft.Extensions.Caching.Distributed;

namespace Atrium.Services.Storefront.Support;

/// <summary>
/// Applies a default absolute TTL to every cache write that doesn't set its own expiration.
/// <c>DistributedCachingChatClient</c> writes entries with default (never-expiring) options, so without
/// this wrapper the chat cache grows unbounded and a stale model answer is served forever.
/// </summary>
internal sealed class TtlDistributedCache(IDistributedCache inner, TimeSpan ttl) : IDistributedCache
{
    public byte[]? Get(string key) => inner.Get(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        inner.GetAsync(key, token);

    public void Refresh(string key) => inner.Refresh(key);

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        inner.RefreshAsync(key, token);

    public void Remove(string key) => inner.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        inner.RemoveAsync(key, token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        inner.Set(key, value, WithTtl(options));

    public Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default
    ) => inner.SetAsync(key, value, WithTtl(options), token);

    // The incoming options may be a frozen shared instance (DistributedCachingChatClient reuses one),
    // so never mutate it — substitute a fresh TTL-carrying instance when no expiration is set.
    private DistributedCacheEntryOptions WithTtl(DistributedCacheEntryOptions options) =>
        options.AbsoluteExpiration is null
        && options.AbsoluteExpirationRelativeToNow is null
        && options.SlidingExpiration is null
            ? new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }
            : options;
}
