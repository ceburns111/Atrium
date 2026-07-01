// Persists the anonymous/interactive cart to localStorage so it survives the full-page OIDC sign-in
// round-trip (which spins up a fresh Blazor circuit and a fresh scoped CartService). We store only a
// minimal { productId, quantity } list — never product details, prices, or anything sensitive. Every
// call is wrapped so a storage exception (private mode, quota, disabled) degrades to in-memory rather
// than throwing back across the interop boundary.
const KEY = "atrium.cart.v1";

export function load() {
    try {
        return localStorage.getItem(KEY);
    } catch {
        return null;
    }
}

export function save(json) {
    try {
        localStorage.setItem(KEY, json);
    } catch {
        // Storage unavailable — the cart stays in-memory for this session.
    }
}

export function clear() {
    try {
        localStorage.removeItem(KEY);
    } catch {
        // Nothing to do; a stale entry (if any) is harmless and re-priced on next hydrate.
    }
}
