using Atrium.Contracts;

namespace Atrium.Modules.Storefront.Cart;

public sealed class CartLine
{
    public required ProductDto Product { get; init; }
    public int Quantity { get; set; }
}

/// <summary>
/// The minimal, persistable shape of a cart line: just the product id and quantity. Product details
/// (name, price, blurb) are deliberately NOT stored — they are re-fetched from the catalog on hydrate
/// so a persisted cart always re-prices against current catalog data and never carries stale prices.
/// </summary>
public sealed record CartSnapshotItem(int ProductId, int Quantity);

/// <summary>
/// The shopping cart, scoped per circuit and registered by the Storefront module itself — the host
/// never knows it exists. Components subscribe to <see cref="Changed"/> to re-render on updates.
/// </summary>
public sealed class CartService
{
    private readonly List<CartLine> _lines = [];

    public IReadOnlyList<CartLine> Lines => _lines;
    public event Action? Changed;

    public int Count => _lines.Sum(l => l.Quantity);
    public decimal Total => _lines.Sum(l => l.Product.Price * l.Quantity);

    public void Add(ProductDto product)
    {
        var line = _lines.FirstOrDefault(l => l.Product.Id == product.Id);
        if (line is null)
        {
            _lines.Add(new CartLine { Product = product, Quantity = 1 });
        }
        else
        {
            line.Quantity++;
        }
        Changed?.Invoke();
    }

    public void SetQuantity(int productId, int quantity)
    {
        var line = _lines.FirstOrDefault(l => l.Product.Id == productId);
        if (line is null)
        {
            return;
        }

        if (quantity <= 0)
        {
            _lines.Remove(line);
        }
        else
        {
            line.Quantity = quantity;
        }
        Changed?.Invoke();
    }

    public void Remove(int productId)
    {
        _lines.RemoveAll(l => l.Product.Id == productId);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _lines.Clear();
        Changed?.Invoke();
    }

    /// <summary>The minimal id + quantity projection used to persist the cart.</summary>
    public IReadOnlyList<CartSnapshotItem> Snapshot() =>
        _lines.Select(l => new CartSnapshotItem(l.Product.Id, l.Quantity)).ToList();

    /// <summary>
    /// Replaces the cart's contents wholesale (used when rehydrating from persisted storage) and
    /// notifies subscribers once. Lines are rebuilt by the caller from current catalog products.
    /// </summary>
    public void Restore(IEnumerable<CartLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
        Changed?.Invoke();
    }
}
