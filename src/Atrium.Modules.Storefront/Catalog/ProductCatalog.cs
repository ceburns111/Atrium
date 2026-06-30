namespace Atrium.Modules.Storefront.Catalog;

/// <summary>
/// Mock product data for the Storefront module. In Phase 4 this is replaced by the Catalog core
/// service, reached over the gateway — the pages and cart stay the same.
/// </summary>
public static class ProductCatalog
{
    public static IReadOnlyList<Product> Products { get; } =
    [
        new(
            1,
            "Walnut Monitor Shelf",
            "Desk",
            89,
            "Solid walnut riser that lifts your screen to eye level."
        ),
        new(2, "Linen Desk Mat", "Desk", 39, "Washed-linen mat that warms up a cold desk."),
        new(
            3,
            "Aluminium Laptop Stand",
            "Desk",
            64,
            "Angled stand that keeps your laptop cool and upright."
        ),
        new(
            4,
            "Cork Coaster Set",
            "Desk",
            18,
            "Four sealed-cork coasters for the inevitable coffee."
        ),
        new(5, "Bookshelf Speaker", "Audio", 149, "Compact two-way speaker tuned for small rooms."),
        new(
            6,
            "USB-C Audio Interface",
            "Audio",
            119,
            "Low-latency interface for calls and recording."
        ),
        new(7, "Over-Ear Headphones", "Audio", 199, "Closed-back headphones for focused work."),
        new(8, "Task Lamp", "Lighting", 79, "Dimmable LED lamp with a warm-to-cool range."),
        new(
            9,
            "Clip-On Reading Light",
            "Lighting",
            24,
            "Rechargeable light that clamps to any shelf."
        ),
        new(
            10,
            "Bias Light Strip",
            "Lighting",
            32,
            "Behind-the-monitor strip that eases eye strain."
        ),
        new(11, "Felt Cable Tray", "Storage", 29, "Under-desk tray that hides the cable mess."),
        new(
            12,
            "Stacking Drawer",
            "Storage",
            54,
            "Shallow drawer for the stuff that clutters a desk."
        ),
    ];

    public static IReadOnlyList<string> Categories { get; } =
        Products.Select(p => p.Category).Distinct().Order().ToList();
}
