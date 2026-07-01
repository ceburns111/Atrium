-- Run-once (journaled): seed categories and the desk-goods catalog.
INSERT INTO dbo.Categories (Name)
VALUES (N'Desk'), (N'Audio'), (N'Lighting'), (N'Storage');

INSERT INTO dbo.Products (Name, CategoryId, Price, Blurb)
SELECT v.Name, c.Id, v.Price, v.Blurb
FROM (VALUES
    (N'Walnut Monitor Shelf', N'Desk', 89, N'Solid walnut riser that lifts your screen to eye level.'),
    (N'Linen Desk Mat', N'Desk', 39, N'Washed-linen mat that warms up a cold desk.'),
    (N'Aluminium Laptop Stand', N'Desk', 64, N'Angled stand that keeps your laptop cool and upright.'),
    (N'Cork Coaster Set', N'Desk', 18, N'Four sealed-cork coasters for the inevitable coffee.'),
    (N'Bookshelf Speaker', N'Audio', 149, N'Compact two-way speaker tuned for small rooms.'),
    (N'USB-C Audio Interface', N'Audio', 119, N'Low-latency interface for calls and recording.'),
    (N'Over-Ear Headphones', N'Audio', 199, N'Closed-back headphones for focused work.'),
    (N'Task Lamp', N'Lighting', 79, N'Dimmable LED lamp with a warm-to-cool range.'),
    (N'Clip-On Reading Light', N'Lighting', 24, N'Rechargeable light that clamps to any shelf.'),
    (N'Bias Light Strip', N'Lighting', 32, N'Behind-the-monitor strip that eases eye strain.'),
    (N'Felt Cable Tray', N'Storage', 29, N'Under-desk tray that hides the cable mess.'),
    (N'Stacking Drawer', N'Storage', 54, N'Shallow drawer for the stuff that clutters a desk.')
) AS v (Name, Category, Price, Blurb)
JOIN dbo.Categories c ON c.Name = v.Category;
