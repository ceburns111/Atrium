using System.Text.Json;
using Atrium.Modules.Storefront.Catalog;
using Microsoft.JSInterop;

namespace Atrium.Modules.Storefront.Cart;

/// <summary>
/// Bridges the circuit-scoped <see cref="CartService"/> to browser <c>localStorage</c> so the cart
/// survives the full-page OIDC sign-in round-trip (a new circuit means a brand-new scoped
/// <see cref="CartService"/>). It persists a minimal id + quantity snapshot on every cart mutation
/// (saves are chained FIFO so an older snapshot can never overwrite a newer one) and rehydrates once
/// per circuit, merging persisted lines into the live cart and re-pricing from the current catalog.
///
/// Interop safety (ADR-0010): JS is NEVER called during prerender/SSR. Hydration must be triggered from
/// the first interactive render (<c>OnAfterRenderAsync(firstRender)</c>); saves are triggered by cart
/// mutations, which only happen from interactive user actions. Every interop call is guarded so that a
/// missing/disconnected circuit degrades the cart to in-memory rather than throwing.
/// </summary>
public sealed class CartPersistence : IAsyncDisposable
{
    private const string ModulePath = "./_content/Atrium.Modules.Storefront/js/cart-storage.js";

    private readonly CartService _cart;
    private readonly CatalogClient _catalog;
    private readonly IJSRuntime _js;

    private Task<IJSObjectReference>? _moduleTask;
    private Task _saveChain = Task.CompletedTask;
    private bool _hydrated;

    public CartPersistence(CartService cart, CatalogClient catalog, IJSRuntime js)
    {
        _cart = cart;
        _catalog = catalog;
        _js = js;
        _cart.Changed += OnCartChanged;
    }

    /// <summary>
    /// Loads any persisted cart from localStorage and rebuilds the in-memory cart from current catalog
    /// products. Idempotent: only the first call per circuit does the work. Safe to call from
    /// <c>OnAfterRenderAsync(firstRender)</c>; never call during prerender.
    /// </summary>
    public async Task HydrateAsync()
    {
        if (_hydrated)
        {
            return;
        }
        _hydrated = true; // set before awaiting so a concurrent/second call no-ops

        try
        {
            var module = await GetModuleAsync();
            var json = await module.InvokeAsync<string?>("load");
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var items = JsonSerializer.Deserialize<List<CartSnapshotItem>>(json);
            if (items is null || items.Count == 0)
            {
                return;
            }

            // Re-price from the live catalog: persisted lines carry only id + quantity.
            var products = await _catalog.GetProductsAsync();
            var byId = products.ToDictionary(p => p.Id);
            var lines = items
                .Where(i => i.Quantity > 0 && byId.ContainsKey(i.ProductId))
                .Select(i => new CartLine { Product = byId[i.ProductId], Quantity = i.Quantity })
                .ToList();

            if (lines.Count > 0)
            {
                // Merge rather than replace: anything the user added while this hydrate was in
                // flight survives, and the merged result is re-persisted by the Changed handler.
                _cart.MergeRestored(lines);
            }
        }
        catch (Exception ex)
            when (ex is JSDisconnectedException or InvalidOperationException or JSException)
        {
            // JS unavailable (prerender/disconnect) — degrade to an empty in-memory cart.
        }
        catch (JsonException)
        {
            // Corrupt/legacy payload — ignore it and start clean.
        }
    }

    // Persist on every mutation. The snapshot is serialized synchronously HERE — at mutation time —
    // and the writes are chained FIFO onto _saveChain, so overlapping saves stay ordered and a slow
    // older snapshot can never land after (and overwrite) a newer one. Cart mutations originate from
    // interactive user actions, so JS is available; failures are swallowed per link to keep the cart
    // usable in-memory (which also means awaiting the previous link never throws).
    private void OnCartChanged()
    {
        var json = JsonSerializer.Serialize(_cart.Snapshot());
        _saveChain = SaveAsync(_saveChain, json);
    }

    private async Task SaveAsync(Task previous, string json)
    {
        await previous; // never faults — every link swallows its own failures below
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("save", json);
        }
        catch (Exception ex)
            when (ex is JSDisconnectedException or InvalidOperationException or JSException)
        {
            // Circuit gone or JS unavailable — the cart remains valid in-memory for this session.
        }
    }

    // Single-flight: cache the import Task itself (not the resolved reference) so a hydrate and a
    // save racing on first use share one import instead of each starting their own. A failed import
    // is evicted so the next call can retry rather than being stuck on a faulted task forever.
    private Task<IJSObjectReference> GetModuleAsync()
    {
        if (
            _moduleTask is null
            || (_moduleTask.IsCompleted && !_moduleTask.IsCompletedSuccessfully)
        )
        {
            _moduleTask = _js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
        }
        return _moduleTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cart.Changed -= OnCartChanged;
        if (_moduleTask is { IsCompletedSuccessfully: true } moduleTask)
        {
            try
            {
                await (await moduleTask).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already gone — nothing to dispose.
            }
        }
    }
}
