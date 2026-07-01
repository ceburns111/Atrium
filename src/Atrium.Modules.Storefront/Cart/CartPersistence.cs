using System.Text.Json;
using Atrium.Modules.Storefront.Catalog;
using Microsoft.JSInterop;

namespace Atrium.Modules.Storefront.Cart;

/// <summary>
/// Bridges the circuit-scoped <see cref="CartService"/> to browser <c>localStorage</c> so the cart
/// survives the full-page OIDC sign-in round-trip (a new circuit means a brand-new scoped
/// <see cref="CartService"/>). It persists a minimal id + quantity snapshot on every cart mutation and
/// rehydrates once per circuit, re-pricing lines from the current catalog.
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

    private IJSObjectReference? _module;
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
                _cart.Restore(lines);
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

    // Fire-and-forget persist on every mutation. Cart mutations originate from interactive user actions,
    // so JS is available; failures are swallowed to keep the cart usable in-memory.
    private void OnCartChanged() => _ = SaveAsync();

    private async Task SaveAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            var json = JsonSerializer.Serialize(_cart.Snapshot());
            await module.InvokeVoidAsync("save", json);
        }
        catch (Exception ex)
            when (ex is JSDisconnectedException or InvalidOperationException or JSException)
        {
            // Circuit gone or JS unavailable — the cart remains valid in-memory for this session.
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync() =>
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    public async ValueTask DisposeAsync()
    {
        _cart.Changed -= OnCartChanged;
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already gone — nothing to dispose.
            }
        }
    }
}
