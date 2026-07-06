# MudBlazor Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hand-rolled BEM/token design system with MudBlazor across the Portal shell and all three modules, keeping the Atrium identity via a `MudTheme` mapped from the existing tokens.

**Architecture:** Full cutover in always-green milestones: foundation (package + theme, old CSS still loaded) → shell → Storefront pages → Admin → Reports/home → teardown of the old system → docs. Both stylesheets coexist mid-run (BEM classes and Mud classes don't collide; minor visual oddities are expected and are `[~]`, not failures). `ProductThumb`, `Notice`, and `PageHeader` stay custom, restyled onto Mud's emitted CSS variables.

**Tech Stack:** .NET 10 Blazor Server, MudBlazor (latest stable — pinned in Task 1), bUnit, Playwright.

**Spec:** `docs/superpowers/specs/2026-07-05-mudblazor-migration-design.md` — read it first.
**Precondition:** the retire-support-agent plan is fully executed on this branch line (no `AgentChat`, no `AssistantLauncher`, no `agentchat.js`).

## Global Constraints

- **Run mechanics:** same as the retire-support-agent plan (runbook `docs/archive/runs/README.md`): run branch `run/mudblazor-migration` cut from the retire run's end state; one atomic commit per task; orchestrator re-runs the gate before every commit; 2 attempts → revert-to-green + BLOCKED; halt on 2 consecutive BLOCKED.
- **The gate (Lane A):** `dotnet csharpier format . && dotnet build Atrium.slnx -v q` (0/0) then `dotnet test Atrium.slnx` (Docker up).
- **Live URLs / users (Lane B):** Portal https://localhost:7001 (fallback http://localhost:5035); Keycloak http://localhost:8080; health http://localhost:5260/health + http://localhost:5109/health; `testuser`/`password`, `admin`/`password`. Boot via `aspire run` from `src/Atrium.AppHost` (background), wait for both `Healthy`.
- **Visual protocol:** Lane B asserts *functional* outcomes (element exists, flow completes, zero console errors). Anything subjective (spacing, contrast, "looks off") is `[~]` best-effort: screenshot it, flag it in the LOG, keep going. Never block on visuals; never declare visual polish "done".
- **MudBlazor API caveat:** property names below (`PaletteLight`, `Typography`, `LayoutProperties`, typography sub-classes) match the MudBlazor 8.x API. If the pinned version's compiler errors say a property moved/renamed, adapt the *name* — the **values** are authoritative (they mirror `tokens.css` exactly).
- **Never hand-roll styling** where a Mud component/utility exists (`Class="pa-4 mt-2 d-flex gap-3"` utilities are fine; new bespoke CSS files are not — the only sanctioned bespoke CSS after teardown is `Notice.razor.css`, `ProductThumb`'s inline SVG styling, and `PageHeader`'s scoped file if needed).
- Screenshots → gitignored `artifacts/mudblazor-migration/`.

---

### Task 1: Foundation — package, services, links (app renders unchanged)

**Files:**
- Modify: `src/Atrium.Design/Atrium.Design.csproj`, `src/Atrium.Design/_Imports.razor`, `src/Atrium.Portal/Components/_Imports.razor` (or the Portal's root `_Imports.razor` — locate it), `src/Atrium.Modules.Storefront/_Imports.razor`, `src/Atrium.Modules.Admin/_Imports.razor`, `src/Atrium.Modules.Reports/_Imports.razor`, `src/Atrium.Portal/Program.cs`, `src/Atrium.Portal/Components/App.razor`

**Interfaces:**
- Produces: `MudBlazor` available in every Razor compilation; `AddMudServices()` registered; Mud css/js served. Old `tokens.css`/`atrium.css` links **stay** until Task 7.

- [ ] **Step 1: Add the package to Design only** (Portal + modules get it transitively):

```bash
dotnet add src/Atrium.Design/Atrium.Design.csproj package MudBlazor
```

Record the resolved version in the run LOG. Build must succeed against `net10.0`; if the latest stable fails to restore against net10, try the previous minor and note it.

- [ ] **Step 2: Add `@using MudBlazor`** to each listed `_Imports.razor`.

- [ ] **Step 3: Register services.** In `src/Atrium.Portal/Program.cs` add near the other service registrations (around the existing `AddScoped<ToastService>()` at line 21):

```csharp
builder.Services.AddMudServices();
```

with `using MudBlazor.Services;` at the top.

- [ ] **Step 4: Add Mud assets in `App.razor`.** After the existing `atrium.css` link add:

```html
    <link rel="stylesheet" href="_content/MudBlazor/MudBlazor.min.css" />
```

and before the `blazor.web.js` script tag add:

```html
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

- [ ] **Step 5: Gate** (Lane A). Expected green — nothing consumes Mud yet.

- [ ] **Step 6: Commit** — `git add -A && git commit -m "feat(design): add MudBlazor package, services, and static assets (ADR-0014)"`

---

### Task 2: `AtriumTheme` — the token palette as a MudTheme

**Files:**
- Create: `src/Atrium.Design/AtriumTheme.cs`
- Test: `tests/Atrium.UnitTests/AtriumThemeTests.cs`

**Interfaces:**
- Produces: `static class AtriumTheme` with `public static readonly MudTheme Theme` — consumed by `MainLayout` (Task 3) and by nothing else.

- [ ] **Step 1: Write the failing test** at `tests/Atrium.UnitTests/AtriumThemeTests.cs`:

```csharp
using Atrium.Design;

namespace Atrium.UnitTests;

/// <summary>Pins the brand-critical values carried over from the retired tokens.css (ADR-0014).</summary>
public class AtriumThemeTests
{
    [Fact]
    public void Light_palette_keeps_the_atrium_teal_and_paper()
    {
        Assert.Equal("#117b68", AtriumTheme.Theme.PaletteLight.Primary.Value.ToLowerInvariant());
        Assert.Equal("#fbfbfa", AtriumTheme.Theme.PaletteLight.Background.Value.ToLowerInvariant());
    }

    [Fact]
    public void Dark_palette_flips_to_the_luminous_teal_with_dark_on_accent()
    {
        Assert.Equal("#2dbd9b", AtriumTheme.Theme.PaletteDark.Primary.Value.ToLowerInvariant());
        Assert.Equal("#08211b", AtriumTheme.Theme.PaletteDark.PrimaryContrastText.Value.ToLowerInvariant());
    }
}
```

- [ ] **Step 2: Run it to verify it fails** — `dotnet test tests/Atrium.UnitTests -- --filter-class "*AtriumThemeTests"` → FAIL (`AtriumTheme` does not exist). (`MudColor.Value` includes an alpha suffix in some versions — if the assert fails on `#117b68ff`, compare with `StartsWith` instead; note it in the LOG.)

- [ ] **Step 3: Implement `AtriumTheme.cs`.** Values are verbatim from `tokens.css` (`:root` → light, `:root[data-theme="dark"]` → dark) — the token name is noted beside each:

```csharp
using MudBlazor;

namespace Atrium.Design;

/// <summary>
/// The Atrium MudBlazor theme — single source of truth for color, type, and shape, carrying the
/// palette forward from the retired tokens.css value-for-value (ADR-0014). Custom components use
/// Mud's emitted CSS variables (--mud-palette-*) instead of the old tokens.
/// </summary>
public static class AtriumTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Background = "#fbfbfa",            // --paper
            Surface = "#ffffff",               // --surface
            BackgroundGray = "#f4f4f3",        // --surface-2
            TextPrimary = "#18181b",           // --ink
            TextSecondary = "#3f3f46",         // --ink-2
            TextDisabled = "#a1a1aa",          // --faint
            ActionDefault = "#71717a",         // --muted
            Primary = "#117b68",               // --accent
            PrimaryDarken = "#0c5a4c",         // --accent-ink
            PrimaryContrastText = "#ffffff",   // --on-accent
            Secondary = "#117b68",             // accent doubles as secondary; variants differentiate
            SecondaryContrastText = "#ffffff",
            Tertiary = "#e6f2ef",              // --accent-soft
            Info = "#117b68",
            Success = "#16785a",               // --success
            Warning = "#8a5a0f",               // --warning
            Error = "#a23b3b",                 // --danger
            LinesDefault = "#e7e7e4",          // --line
            LinesInputs = "#d6d6d1",           // --line-strong
            Divider = "#e7e7e4",               // --line
            AppbarBackground = "#fbfbfa",      // topbar sits on paper
            AppbarText = "#18181b",
            DrawerBackground = "#ffffff",      // sidebar sits on surface
            DrawerText = "#18181b",
        },
        PaletteDark = new PaletteDark
        {
            Background = "#131316",
            Surface = "#1b1b1f",
            BackgroundGray = "#26262b",
            TextPrimary = "#f4f4f5",
            TextSecondary = "#d4d4d8",
            TextDisabled = "#71717a",
            ActionDefault = "#a1a1aa",
            Primary = "#2dbd9b",
            PrimaryDarken = "#58d4b7",
            PrimaryContrastText = "#08211b",   // dark-on-teal clears AA in dark mode
            Secondary = "#2dbd9b",
            SecondaryContrastText = "#08211b",
            Tertiary = "#16362e",
            Info = "#2dbd9b",
            Success = "#35c99a",
            Warning = "#e0a94a",
            Error = "#e07070",
            LinesDefault = "#2c2c31",
            LinesInputs = "#3f3f46",
            Divider = "#2c2c31",
            AppbarBackground = "#131316",
            AppbarText = "#f4f4f5",
            DrawerBackground = "#1b1b1f",
            DrawerText = "#f4f4f5",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"],
                FontSize = "0.9375rem",        // --text-base
                LineHeight = "1.55",           // --leading-normal
            },
            H4 = new H4Typography { FontFamily = ["Space Grotesk", "Inter", "system-ui", "sans-serif"], FontSize = "1.9375rem", FontWeight = "600", LineHeight = "1.15" },
            H5 = new H5Typography { FontFamily = ["Space Grotesk", "Inter", "system-ui", "sans-serif"], FontSize = "1.375rem", FontWeight = "600", LineHeight = "1.15" },
            H6 = new H6Typography { FontFamily = ["Space Grotesk", "Inter", "system-ui", "sans-serif"], FontSize = "1.0625rem", FontWeight = "600", LineHeight = "1.15" },
            Button = new ButtonTypography { FontFamily = ["Inter", "system-ui", "sans-serif"], FontWeight = "500", TextTransform = "none" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",       // --r-md
            DrawerWidthLeft = "16rem",         // --sidebar-w
            AppbarHeight = "3.5rem",           // --topbar-h
        },
    };
}
```

(`TextTransform = "none"` matters — Material's ALL-CAPS buttons would read off-brand.)

- [ ] **Step 4: Run the test** — same filter → PASS. Then the full Lane A gate.

- [ ] **Step 5: Commit** — `git commit -am "feat(design): AtriumTheme — tokens.css palette as a MudTheme"`

---

### Task 3: Shell — MudLayout, AppBar, Drawer, dark mode, UserMenu

**Files:**
- Modify: `src/Atrium.Portal/Components/Layout/MainLayout.razor`, `NavMenu.razor`, `UserMenu.razor`
- Delete: `src/Atrium.Design/Components/ThemeToggle.razor`

**Interfaces:**
- Consumes: `AtriumTheme.Theme` (Task 2); existing `theme.js` (`get()`/`set(theme)` on localStorage + `data-theme`).
- Produces: the shell every page renders inside. `ToastHost` **stays** in the layout until Task 7 (module pages still use `ToastService` until migrated). `SessionErrorBoundary` logic untouched.

- [ ] **Step 1: Replace `MainLayout.razor`** with:

```razor
@inherits LayoutComponentBase
@implements IDisposable
@inject NavigationManager Nav
@inject Atrium.Design.AccessTokenHolder Tokens
@inject IJSRuntime JS

<MudThemeProvider Theme="AtriumTheme.Theme" @bind-IsDarkMode="_isDark" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="0" Dense="false">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start"
                       OnClick="ToggleNav" aria-label="Toggle navigation" />
        <MudText Typo="Typo.body2">
            <MudLink Href="" Underline="Underline.None" Color="Color.Inherit">Atrium</MudLink>
            <span class="mx-2">/</span>@Section
        </MudText>
        <MudSpacer />
        <MudIconButton Icon="@(_isDark ? Icons.Material.Outlined.LightMode : Icons.Material.Outlined.DarkMode)"
                       Color="Color.Inherit" OnClick="ToggleTheme" aria-label="Toggle theme" />
        <UserMenu />
    </MudAppBar>
    <MudDrawer @bind-Open="_navOpen" Elevation="0" Breakpoint="Breakpoint.Md"
               ClipMode="DrawerClipMode.Always" Variant="DrawerVariant.Responsive">
        <NavMenu />
    </MudDrawer>
    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.Large" Class="py-6">
            <SessionErrorBoundary @ref="_errorBoundary">
                @Body
            </SessionErrorBoundary>
        </MudContainer>
    </MudMainContent>
</MudLayout>

<ToastHost />

<div id="blazor-error-ui" data-nosnippet>
    An unhandled error has occurred.
    <a href="." class="reload">Reload</a>
    <span class="dismiss">🗙</span>
</div>

@code {
    private bool _navOpen = true;
    private bool _isDark;
    private IJSObjectReference? _theme;
    private SessionErrorBoundary? _errorBoundary;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    protected override void OnInitialized() => Nav.LocationChanged += OnLocationChanged;

    // Capture the signed-in user's access token so module HttpClients can attach it on every request.
    protected override async Task OnParametersSetAsync()
    {
        if (AuthState is not null)
        {
            Tokens.AccessToken = (await AuthState).User.FindFirst("access_token")?.Value;
        }
    }

    // Sync dark mode from the persisted choice (theme.js reads localStorage / data-theme, which the
    // host page's inline script applied before first paint). Interop only after first interactive
    // render, never during prerender (ADR-0010).
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }
        try
        {
            _theme ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Atrium.Design/js/theme.js");
            var dark = await _theme.InvokeAsync<string>("get") == "dark";
            if (dark != _isDark)
            {
                _isDark = dark;
                StateHasChanged();
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
            // Circuit gone or prerender — next interactive render syncs.
        }
    }

    private async Task ToggleTheme()
    {
        _isDark = !_isDark;
        try
        {
            _theme ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Atrium.Design/js/theme.js");
            await _theme.InvokeVoidAsync("set", _isDark ? "dark" : "light");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
        }
    }

    private void ToggleNav() => _navOpen = !_navOpen;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        InvokeAsync(() =>
        {
            // Clear any error card so the newly-navigated page gets a fresh attempt.
            _errorBoundary?.Recover();
            StateHasChanged();
        });
    }

    private string Section
    {
        get
        {
            var segments = new Uri(Nav.Uri).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length == 0 ? "Home" : char.ToUpperInvariant(segments[0][0]) + segments[0][1..];
        }
    }

    public void Dispose() => Nav.LocationChanged -= OnLocationChanged;
}
```

Notes: the old `nav-open`/backdrop/`CloseNav` mechanics die — `MudDrawer Variant="Responsive"` owns mobile behavior. Keep the `IAsyncDisposable`-style guarded interop exactly as shown.

- [ ] **Step 2: Rewrite `NavMenu.razor`** keeping the brand SVG and the module-count footer, with Mud nav primitives. Preserve the role-gating logic verbatim:

```razor
@inject Atrium.Portal.Modularity.ModuleCatalog Catalog

<MudDrawerHeader Class="d-flex align-center gap-2">
    @* Custom "skylight" mark — a light-well viewed top-down — not a borrowed icon set. *@
    <svg viewBox="0 0 28 28" width="28" height="28" fill="none" aria-hidden="true">
        <rect x="3" y="3" width="22" height="22" rx="6" stroke="currentColor" stroke-width="2" />
        <rect x="10" y="10" width="8" height="8" rx="2" fill="currentColor" />
    </svg>
    <MudText Typo="Typo.h6">Atrium</MudText>
</MudDrawerHeader>

<MudNavMenu Class="flex-grow-1">
    <MudNavLink Href="" Match="NavLinkMatch.All">Home</MudNavLink>

    @if (Catalog.Modules.Count > 0)
    {
        <MudText Typo="Typo.overline" Class="px-4 mt-4 mud-text-secondary">Apps</MudText>
        @foreach (var module in Catalog.Modules)
        {
            @foreach (var item in module.NavItems)
            {
                var role = item.RequiredRole ?? module.RequiredRole;
                @if (role is null)
                {
                    <MudNavLink Href="@item.Path">@item.Title</MudNavLink>
                }
                else
                {
                    <AuthorizeView Roles="@role">
                        <Authorized>
                            <MudNavLink Href="@item.Path">@item.Title</MudNavLink>
                        </Authorized>
                    </AuthorizeView>
                }
            }
        }
    }
</MudNavMenu>

<MudText Typo="Typo.caption" Class="px-4 py-3 mud-text-secondary">
    @if (_visibleCount == Catalog.Modules.Count)
    {
        @($"{Catalog.Modules.Count} module{(Catalog.Modules.Count == 1 ? "" : "s")} loaded")
    }
    else
    {
        @($"{_visibleCount} of {Catalog.Modules.Count} modules visible")
    }
</MudText>

@code {
    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    private int _visibleCount;

    protected override async Task OnParametersSetAsync()
    {
        var user = AuthState is null ? null : (await AuthState).User;
        _visibleCount = Catalog.Modules.Count(m =>
            Atrium.Portal.Modularity.ModuleVisibility.IsVisible(m, user)
        );
    }
}
```

- [ ] **Step 3: Rewrite `UserMenu.razor`** on `MudMenu` (drops the Design `Menu` — keep the `Initial` helper verbatim):

```razor
@namespace Atrium.Portal.Components.Layout

<AuthorizeView>
    <Authorized>
        <MudMenu AnchorOrigin="Origin.BottomRight" TransformOrigin="Origin.TopRight" aria-label="Account menu">
            <ActivatorContent>
                <MudAvatar Color="Color.Primary" Size="Size.Small">@Initial(context.User.Identity?.Name)</MudAvatar>
            </ActivatorContent>
            <ChildContent>
                <div class="px-4 py-2">
                    <MudText Typo="Typo.body2">@context.User.Identity?.Name</MudText>
                    <MudText Typo="Typo.caption" Class="mud-text-secondary">Signed in</MudText>
                </div>
                <MudDivider />
                <MudMenuItem Href="/account/logout">Sign out</MudMenuItem>
            </ChildContent>
        </MudMenu>
    </Authorized>
    <NotAuthorized>
        <MudButton Variant="Variant.Outlined" Size="Size.Small" Href="/account/login" Color="Color.Inherit">Sign in</MudButton>
    </NotAuthorized>
</AuthorizeView>

@code {
    // First letter of the signed-in name, upper-cased; "?" as a defensive fallback.
    private static string Initial(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : char.ToUpperInvariant(name.TrimStart()[0]).ToString();
}
```

- [ ] **Step 4: Delete `src/Atrium.Design/Components/ThemeToggle.razor`** (`git rm`) — the AppBar icon button replaced it. (The Design `Menu` component itself is deleted in Task 7; `MenuTests` still passes until then.)

- [ ] **Step 5: Gate** (Lane A). Then **Lane B milestone drive:** boot the stack; sign in as `testuser`; assert drawer nav works, breadcrumb shows the section, theme icon toggles dark and it **persists across a full page reload**, avatar menu opens with Sign out, drawer collapses at 390×844 and opens via the menu button. Screenshots light+dark of the shell → `artifacts/mudblazor-migration/`. Console: zero errors.

- [ ] **Step 6: Commit** — `git commit -am "feat(portal): MudBlazor shell — MudLayout/AppBar/Drawer, themed dark mode, MudMenu account control"`

---

### Task 4: Storefront pages — Shop, Cart, Checkout, Orders

**Files:**
- Modify: `src/Atrium.Modules.Storefront/Pages/Shop.razor`, `CartPage.razor`, `Checkout.razor`, `OrdersPage.razor`

**Interfaces:**
- Consumes: the shell (Task 3); existing typed clients/`CartService`/`CartPersistence` — **all `@code` logic stays byte-identical except `ToastService` → `ISnackbar`**.
- Produces: pages free of BEM classes (`btn`, `chip`, `product-grid`, `atrium-table`, `empty`, `skeleton`…).

Element-mapping directives for all four pages (apply mechanically; keep every `@code` guard/sequence/idempotency mechanic untouched):

| Current | Replace with |
|---|---|
| `<a class="btn btn--ghost" href=…>` / `btn--secondary` links | `<MudButton Variant="Variant.Text" Href=…>` / `Variant.Outlined` |
| `<Button Variant="ButtonVariant.X" …>` | `<MudButton Variant=… Color=…>` — Primary→`Filled`/`Primary`, Accent→`Filled`/`Secondary`, Secondary→`Outlined`/`Primary`, Ghost→`Text`/`Primary`; `Small`→`Size="Size.Small"`; `Disabled`/`OnClick` map 1:1 |
| category `chip`/`chip--on` buttons | `<MudChip T="string" Variant="@(selected ? Variant.Filled : Variant.Text)" Color="Color.Primary" OnClick=…>` |
| `product-grid` + `product` cards | `<MudGrid>` of `<MudItem xs="12" sm="6" md="3">` each holding `<MudCard>` (`MudCardContent` with `ProductThumb`, name `Typo.subtitle1`, blurb `Typo.body2` secondary; `MudCardActions` with price + Add button) |
| `skeleton` placeholders | `<MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="180px" />` (same count) |
| `<p class="empty">…</p>` | `<MudText Class="mud-text-secondary py-8" Align="Align.Center">…</MudText>` |
| `<table class="atrium-table …">` (Orders) | `<MudTable Items=… Hover="true">` with `<HeaderContent>`/`<RowTemplate>`; numeric cells right-aligned via `Style="text-align:right"` on both `MudTh`/`MudTd` |
| `Badge` | `<MudChip T="string" Size="Size.Small" Variant="Variant.Text" Color=…>` (Neutral→`Default`, Success→`Success`, Warning→`Warning`, Danger→`Error`) |
| `@inject ToastService Toasts` + `Toasts.Show(msg, ToastVariant.X)` | `@inject ISnackbar Snackbar` + `Snackbar.Add(msg, Severity.X)` (Success→`Success`, Danger→`Error`, Neutral→`Normal`) |
| form fields (Checkout, if any) | `MudTextField`/`MudNumericField` with `Label=`/`@bind-Value` |
| `PageHeader` / `Notice` / `ProductThumb` usages | **unchanged** (they stay custom) |

- [ ] **Step 1: Convert `Shop.razor`.** The markup section becomes (the `@code` block changes ONLY `ToastService`→`ISnackbar`):

```razor
@page "/storefront"
@implements IDisposable
@inject CatalogClient Catalog
@inject CartService Cart
@inject CartPersistence Persistence
@inject ISnackbar Snackbar

<PageTitle>Storefront</PageTitle>

<PageHeader Eyebrow="Storefront"
            Title="Browse"
            Description="A small catalog of desk goods. Add items to your cart and check out.">
    <Actions>
        <MudButton Variant="Variant.Text" Href="/storefront/orders">Orders</MudButton>
        <MudButton Variant="Variant.Outlined" Href="/storefront/cart">Cart (@Cart.Count)</MudButton>
    </Actions>
</PageHeader>

<div class="d-flex flex-wrap gap-2 mb-4">
    <MudChip T="string" Variant="@(_category is null ? Variant.Filled : Variant.Text)"
             Color="Color.Primary" OnClick="() => Filter(null)">All</MudChip>
    @foreach (var category in _categories)
    {
        <MudChip T="string" Variant="@(_category == category.Name ? Variant.Filled : Variant.Text)"
                 Color="Color.Primary" OnClick="() => Filter(category.Name)">@category.Name</MudChip>
    }
</div>

@if (_failed)
{
    <Notice Title="Couldn't load the catalog"
            Body="Something interrupted the request. Please try again.">
        <MudButton Variant="Variant.Outlined" OnClick="Load">Try again</MudButton>
    </Notice>
}
else if (_products is null)
{
    <MudGrid>
        @for (var i = 0; i < 8; i++)
        {
            <MudItem xs="12" sm="6" md="3">
                <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="220px" />
            </MudItem>
        }
    </MudGrid>
}
else if (_products.Count == 0)
{
    <MudText Class="mud-text-secondary py-8" Align="Align.Center">No products in this category.</MudText>
}
else
{
    <MudGrid>
        @foreach (var product in _products)
        {
            <MudItem xs="12" sm="6" md="3">
                <MudCard Elevation="0" Outlined="true" Class="d-flex flex-column" Style="height:100%">
                    <MudCardContent Class="flex-grow-1">
                        <ProductThumb Name="@product.Name" />
                        <MudText Typo="Typo.subtitle1" Class="mt-2">@product.Name</MudText>
                        <MudText Typo="Typo.body2" Class="mud-text-secondary">@product.Blurb</MudText>
                    </MudCardContent>
                    <MudCardActions Class="d-flex justify-space-between align-center px-4 pb-3">
                        <MudText Typo="Typo.subtitle2">@Money.Format(product.Price)</MudText>
                        <MudButton Size="Size.Small" Variant="Variant.Outlined" OnClick="() => Add(product)">Add</MudButton>
                    </MudCardActions>
                </MudCard>
            </MudItem>
        }
    </MudGrid>
}
```

In `@code`, `Add(product)` becomes `Snackbar.Add($"Added {product.Name}", Severity.Success);`. Everything else (sequence guard, hydrate-on-first-render, dispose) stays identical.

- [ ] **Step 2: Convert `CartPage.razor`, `Checkout.razor`, `OrdersPage.razor`** by the directive table. Read each file first; do not restructure logic — the idempotency key handling in `Checkout` (`_orderKey`) and the `_saving` re-entrancy guard are load-bearing and must survive verbatim.

- [ ] **Step 3: Gate** (Lane A — the existing `CartServiceTests`, `OrderPricingTests`, `PaymentTests` prove logic survived).

- [ ] **Step 4: Lane B milestone drive:** as `testuser`: shop renders as Mud cards; category chip filters; add 2 items (snackbar appears); cart shows items + quantity edit; checkout places the order; confirmation renders; orders list shows it. Zero console errors. Screenshots per page.

- [ ] **Step 5: Commit** — `git commit -am "feat(storefront-ui): migrate Shop/Cart/Checkout/Orders to MudBlazor"`

---

### Task 5: Admin — MudTable + inline MudDialog + Mud form fields

**Files:**
- Modify: `src/Atrium.Modules.Admin/Pages/Products.razor`

**Interfaces:**
- Consumes: `AdminCatalogClient`, `ProductForm` state machine — **all `@code` logic stays except `ToastService` → `ISnackbar`**. Refinement over the spec: the existing open/close state machine maps cleanest onto an **inline `<MudDialog @bind-Visible>`**, not `IDialogService` — keep `_dialogOpen`, `_editingId`, `FormValid` exactly as they are.

- [ ] **Step 1: Convert the table.** Both the skeleton branch and the data branch become one `MudTable` (`Loading="@(_products is null)"`):

```razor
<MudTable Items="@(_products ?? [])" Hover="true" Elevation="0" Outlined="true"
          Loading="@(_products is null)" LoadingProgressColor="Color.Primary">
    <HeaderContent>
        <MudTh>Name</MudTh>
        <MudTh>Category</MudTh>
        <MudTh Style="text-align:right">Price</MudTh>
        <MudTh>Blurb</MudTh>
        <MudTh aria-label="Actions"></MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd DataLabel="Name">@context.Name</MudTd>
        <MudTd DataLabel="Category">
            <MudChip T="string" Size="Size.Small" Variant="Variant.Text">@context.Category</MudChip>
        </MudTd>
        <MudTd DataLabel="Price" Style="text-align:right">@Money.Format(context.Price)</MudTd>
        <MudTd DataLabel="Blurb" Class="mud-text-secondary">@context.Blurb</MudTd>
        <MudTd>
            <MudButton Size="Size.Small" Variant="Variant.Outlined" OnClick="() => StartEdit(context)">Edit</MudButton>
        </MudTd>
    </RowTemplate>
    <NoRecordsContent>
        <MudText Class="mud-text-secondary py-8">No products yet.</MudText>
    </NoRecordsContent>
</MudTable>
```

(The `_failed` → `Notice` branch above it stays; its retry `Button` becomes a `MudButton Variant="Variant.Outlined"`. The `PageHeader` "New product" button likewise.)

- [ ] **Step 2: Convert the dialog.** Replace the `<Dialog Open=…>` block with:

```razor
@* One dialog + one form serves both create and edit — _editingId (null = create) picks the mode. *@
<MudDialog @bind-Visible="_dialogOpen" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">@DialogTitle</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField Label="Name" @bind-Value="_form.Name" Immediate="true" Required="true"
                      Placeholder="Walnut Monitor Shelf" />
        <MudSelect T="string" Label="Category" @bind-Value="_form.Category" Class="mt-3">
            @foreach (var category in _categories)
            {
                <MudSelectItem T="string" Value="@category">@category</MudSelectItem>
            }
        </MudSelect>
        <MudNumericField Label="Price" @bind-Value="_form.Price" Immediate="true" Required="true"
                         Min="0.01m" Step="0.01m" Format="F2" Class="mt-3" />
        <MudTextField Label="Blurb" @bind-Value="_form.Blurb" Immediate="true" Required="true"
                      Placeholder="One-line description." Class="mt-3" />
    </DialogContent>
    <DialogActions>
        <MudButton Variant="Variant.Text" OnClick="Cancel" Disabled="_saving">Cancel</MudButton>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="Save"
                   Disabled="_saving || !FormValid">@SaveLabel</MudButton>
    </DialogActions>
</MudDialog>
```

In `@code` add `private readonly DialogOptions _dialogOptions = new() { CloseOnEscapeKey = true, BackdropClick = false };` and delete `OnDialogOpenChanged` (the two-way `@bind-Visible` replaces it). `Toasts.Show(msg, ToastVariant.Danger/Success)` → `Snackbar.Add(msg, Severity.Error/Success)`. Everything else — `FormValid` (including the two-decimal price rule), `_saving` guard, keep-dialog-open-on-error — stays verbatim.

- [ ] **Step 3: Gate** (Lane A).

- [ ] **Step 4: Lane B milestone drive:** as `admin`: table renders; New product → dialog opens; save disabled until valid; create → snackbar + row appears; edit an existing product → change price → save → row updates; Esc closes the dialog. Zero console errors. Screenshots.

- [ ] **Step 5: Commit** — `git commit -am "feat(admin-ui): migrate product table + edit dialog to MudBlazor"`

---

### Task 6: Reports dashboard + Portal pages (Home, Error, NotFound, Forbidden)

**Files:**
- Modify: `src/Atrium.Modules.Reports/Pages/Dashboard.razor`, `src/Atrium.Portal/Components/Pages/Home.razor`, `Error.razor`, `NotFound.razor`, `Forbidden.razor`

**Interfaces:**
- Consumes: the Task 4 directive table (identical mappings apply).

- [ ] **Step 1: Convert `Dashboard.razor`.** Stat tiles → `<MudGrid>` of `<MudItem xs="12" sm="4">` + `<MudPaper Outlined Class="pa-4">` (label `Typo.overline` secondary, value `Typo.h5`); the sales-by-product bars → per row a flex line with the product name, `<MudProgressLinear Color="Color.Primary" Value="@pct" Rounded="true" Class="flex-grow-1 mx-3" Style="height:8px" />` (`pct` = value/max×100, computed inline from the existing data), and the formatted amount. Keep the existing loading/failed/empty branches, mapped per the directive table. Do **not** add MudChart (out of scope).

- [ ] **Step 2: Convert `Home.razor`.** Module cards → `<MudGrid>` of `<MudCard Elevation="0" Outlined="true">` with the module name (`Typo.h6`), description (`Typo.body2` secondary), and an open `MudButton Variant="Variant.Text" Href="@module.BasePath"`. Preserve the existing role-gating of cards exactly as written (same `ModuleVisibility`/`AuthorizeView` mechanics the page already uses).

- [ ] **Step 3: Convert `Error.razor` / `NotFound.razor` / `Forbidden.razor`** — these are `Notice`-shaped pages; keep `Notice`, convert any `Button`/`btn` links per the directive table.

- [ ] **Step 4: Gate** (Lane A), then Lane B milestone drive: home cards render + role-gate (`testuser` sees no Admin/Reports card), `/reports` renders stats + bars as `admin`, a bogus URL renders NotFound. Screenshots; zero console errors.

- [ ] **Step 5: Commit** — `git commit -am "feat(portal-ui,reports-ui): migrate home, reports dashboard, and status pages to MudBlazor"`

---

### Task 6b: bUnit render smokes for the migrated pages (the overnight backbone)

These run inside the deterministic gate with no browser — they are what keeps the run honest if
Playwright flakes. One test per migrated page: render under Mud services, assert one load-bearing
element.

**Files:**
- Create: `tests/Atrium.UnitTests/CannedJsonHandler.cs`, `tests/Atrium.UnitTests/PageRenderSmokeTests.cs`

**Interfaces:**
- Consumes: the migrated pages (Tasks 4–6); the modules' typed clients (concrete classes over `HttpClient` — fake at the `HttpMessageHandler` level, never mock the client class).

- [ ] **Step 1: Create the shared canned handler** at `tests/Atrium.UnitTests/CannedJsonHandler.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;

namespace Atrium.UnitTests;

/// <summary>
/// Fakes a typed client's transport: returns a canned JSON body for any request whose path ends
/// with a registered suffix; 404 otherwise. Lets page render-smokes construct the REAL typed
/// clients (they are concrete classes over HttpClient) without a server.
/// </summary>
public sealed class CannedJsonHandler(params (string PathSuffix, string Json)[] routes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        foreach (var (suffix, json) in routes)
        {
            if (request.RequestUri!.AbsolutePath.TrimEnd('/').EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json),
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            }
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
```

- [ ] **Step 2: Write the smokes** in `tests/Atrium.UnitTests/PageRenderSmokeTests.cs`. Setup per test class: `JSInterop.Mode = JSRuntimeMode.Loose;` and `Services.AddMudServices();` (namespace `MudBlazor.Services`). Register each page's injected services exactly as declared at the top of the page file (`@inject` lines):
  - Typed clients (`CatalogClient`, `AdminCatalogClient`, orders/reports clients): construct the real class over `new HttpClient(new CannedJsonHandler(...)) { BaseAddress = new Uri("https://gateway/") }` — open each client class first and match its actual constructor (some also take `AccessTokenHolder`; register a fresh `AccessTokenHolder` too). Canned JSON: serialize real DTOs from `Atrium.Contracts` (e.g. `[new ProductDto(1, "Lamp", "Desk", 29.99m)]`-shaped, matching each DTO's actual properties) with `System.Text.Json.JsonSerializer.Serialize`.
  - Module-scoped services (`CartService`, `CartPersistence`): register the real ones the way `StorefrontModuleTests.cs` / `CartServiceTests.cs` already do — copy that registration.
  - Assert per page: Shop → at least one `.mud-card` and the chips row render; Cart → renders (empty-cart text or table) without exception; Orders → a `.mud-table` element exists; Admin Products → a `.mud-table` exists and clicking "New product" makes the dialog's Name field appear; Reports Dashboard → a `.mud-paper` stat tile exists; Home → a module card renders.
  - `MudDialog` content renders through the provider — for the Admin dialog assertion, render `<MudPopoverProvider />` + `<MudDialogProvider />` alongside the page in the test fragment.
  - One test may be dropped to `[~]` (logged, not silently skipped) if a page's service graph genuinely can't be constructed off-circuit — but attempt all six.

- [ ] **Step 3: Run them** — `dotnet test tests/Atrium.UnitTests -- --filter-class "*PageRenderSmokeTests"` → all green. Then the full Lane A gate.

- [ ] **Step 4: Commit** — `git commit -am "test(ui): bUnit render smokes for all MudBlazor-migrated pages"`

---

### Task 7: Teardown — delete the old design system

**Files:**
- Delete from `src/Atrium.Design/`: `Components/Button.razor`, `Components/Badge.razor`, `Components/Field.razor`, `Components/Menu.razor`, `Components/Dialog.razor`, `Components/Dialog.razor.css`, `Components/ToastHost.razor`, `Toasts.cs`, `Enums.cs`, `wwwroot/js/dialog.js`, `wwwroot/css/atrium.css`, `wwwroot/css/tokens.css`
- Modify: `src/Atrium.Design/Components/Notice.razor.css`, `Components/PageHeader.razor`, `Components/ProductThumb.razor`, `src/Atrium.Portal/Components/App.razor`, `src/Atrium.Portal/Program.cs`, `src/Atrium.Portal/Components/Layout/MainLayout.razor`
- Delete: `tests/Atrium.UnitTests/MenuTests.cs`

- [ ] **Step 1: Prove nothing still consumes the old system** (fix any hit before deleting):

```bash
grep -rnE "ButtonVariant|BadgeVariant|ToastService|ToastVariant|<Button|<Badge|<Field|<Menu|<Dialog |<ToastHost|<ThemeToggle|btn--|chip--|atrium-table|product-grid|nav__|menu__|dialog__|class=\"empty\"|class=\"skeleton|product--skeleton|skeleton-line" \
  src/ --include="*.razor" --include="*.cs" | grep -v "MudButton\|Atrium.Design/Components"
```

Expected: no output.

- [ ] **Step 2: Delete** the files listed above (`git rm`). Remove `<ToastHost />` from `MainLayout.razor` and `builder.Services.AddScoped<ToastService>();` from Portal `Program.cs` (every page now uses `ISnackbar`).

- [ ] **Step 3: Re-base the surviving custom components on Mud variables.**
  - `Notice.razor.css` and any styles in `PageHeader`/`ProductThumb`: replace old token references with Mud's emitted variables — `var(--paper)`→`var(--mud-palette-background)`, `var(--surface)`→`var(--mud-palette-surface)`, `var(--ink)`→`var(--mud-palette-text-primary)`, `var(--muted)`/`var(--ink-2)`→`var(--mud-palette-text-secondary)`, `var(--accent)`→`var(--mud-palette-primary)`, `var(--line)`→`var(--mud-palette-lines-default)`, `var(--accent-soft)`→`var(--mud-palette-primary-hover)` (or a fixed rgba of primary), radius→`var(--mud-default-borderradius)`, fonts→inherit. `ProductThumb` generates SVG fill colors in C# — if it reads token *names*, switch to the Mud variable names; if it hard-codes hex ramps, leave them (deterministic art is self-contained).
  - **`ProductThumb` sizing classes:** `CartPage.razor` uses `Class="product-thumb--sm"`, and the base `product-thumb` rules live in `atrium.css`. Before deleting `atrium.css`, move the `product-thumb` / `product-thumb--sm` rule blocks verbatim into a new scoped `src/Atrium.Design/Components/ProductThumb.razor.css` (swapping any token vars per the map above) so the thumbs keep their dimensions.
  - Rewrite `PageHeader.razor` internals on Mud typography (params unchanged — `Eyebrow`, `Title`, `Description`, `Actions`):

```razor
@namespace Atrium.Design

<div class="d-flex justify-space-between align-end flex-wrap gap-3 mb-6">
    <div>
        @if (!string.IsNullOrEmpty(Eyebrow))
        {
            <MudText Typo="Typo.overline" Color="Color.Primary">@Eyebrow</MudText>
        }
        <MudText Typo="Typo.h4">@Title</MudText>
        @if (!string.IsNullOrEmpty(Description))
        {
            <MudText Typo="Typo.body2" Class="mud-text-secondary mt-1">@Description</MudText>
        }
    </div>
    @if (Actions is not null)
    {
        <div class="d-flex gap-2">@Actions</div>
    }
</div>

@code {
    [Parameter] public string? Eyebrow { get; set; }
    [Parameter, EditorRequired] public required string Title { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public RenderFragment? Actions { get; set; }
}
```

(Match the existing parameter declarations exactly if they differ — read the current file first; the visual is what changes, not the contract.)

- [ ] **Step 4: Clean `App.razor`.** Remove the `tokens.css` and `atrium.css` links (Mud + `Atrium.Portal.styles.css` + fonts remain). Keep the inline no-flash script (theme.js still reads/writes `data-theme` + localStorage) and add directly after it a one-rule dark shim so the first paint isn't white in dark mode:

```html
    <style>
        [data-theme="dark"] body { background: #131316; color: #f4f4f5; }
    </style>
```

- [ ] **Step 5: Delete `tests/Atrium.UnitTests/MenuTests.cs`** (`git rm` — its subject is gone). Verify no other test references deleted types: `grep -rn "ToastService\|ButtonVariant\|Atrium.Design.Components" tests/`.

- [ ] **Step 6: Gate** (Lane A) + SAFE-REVERT-POINT commit:

```bash
git add -A && git commit -m "chore(design): tear down BEM primitives, tokens.css, atrium.css, dialog.js — MudBlazor is the design system (ADR-0014)"
git commit --allow-empty -m "chore(run): SAFE-REVERT-POINT — MudBlazor cutover complete, old system deleted, gates green"
```

---

### Task 8: Docs — ADR-0014, atrium-ui skill rewrite, doc sweep

**Files:**
- Create: `docs/adr/0014-adopt-mudblazor.md`
- Modify: `.claude/skills/atrium-ui/SKILL.md`, `docs/adr/README.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md`, `AGENTS.md`, `docs/interview/07-CLARIFICATIONS.md`
- Note: ADR-0010 (native dialog primitive) is also superseded — the native `<dialog>` died with `Dialog.razor`.

- [ ] **Step 1: Write ADR-0014** at `docs/adr/0014-adopt-mudblazor.md`:

```markdown
# ADR-0014 — Adopt MudBlazor; retire the hand-rolled design system

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** pre-demo hardening (2026-07)

## Context

Atrium's UI was a hand-owned design system: BEM primitives (`Button`, `Badge`, `Field`, `Menu`,
`Dialog`, `ToastHost`) in `Atrium.Design`, styled by CSS custom-property tokens (`tokens.css`) and
one shared stylesheet (`atrium.css`). It bought exactly what it promised — flat specificity,
token-driven theming, zero library dependency, a native-`<dialog>` modal (ADR-0010) — at the cost
of owning every component behavior (focus management, responsive drawer, form-field states) and
every line of CSS (1,000+ and growing with each page).

## Decision

Replace the primitives with **MudBlazor**, keeping the Atrium identity by porting the token
palette into a `MudTheme` (`AtriumTheme.cs` — light + dark palettes, Space Grotesk/Inter/JetBrains
Mono typography, the 8px/radius scale). `tokens.css` and `atrium.css` are deleted; components that
have no library equivalent stay custom (`ProductThumb`'s generated art, `Notice`, `PageHeader`) and
consume Mud's emitted CSS variables (`--mud-palette-*`).

This supersedes the "no UI library" stance implicit in the design system and supersedes
[ADR-0010](0010-native-dialog-primitive.md) (native `<dialog>`): `MudDialog` owns modality now,
which also deleted `dialog.js` — JS interop is down to cart persistence, a 15-line theme
persistence module, and the framework's reconnect UI.

## Consequences

- Component behavior (a11y, keyboard handling, responsive drawer, table/dialog/snackbar mechanics)
  is maintained upstream; pages express intent (`MudTable`, `MudDialog`) instead of markup+CSS.
- The trade is real: a library dependency and Material's opinions, tempered by the theme; the
  bespoke BEM look is approximated, not cloned (buttons keep sentence case, the palette is
  identical).
- The `atrium-ui` skill now enforces "MudBlazor + AtriumTheme, no ad-hoc CSS" — same consistency
  goal as before, new substrate.
```

- [ ] **Step 2: Rewrite `.claude/skills/atrium-ui/SKILL.md`.** Read the current file first and preserve its frontmatter shape (name/description trigger style). Replace the body's guidance with the MudBlazor rules:
  - All UI work uses MudBlazor components + `AtriumTheme` (never modify palette values inline; change `AtriumTheme.cs`).
  - Layout/spacing via Mud utility classes (`d-flex`, `pa-*`, `gap-*`, `mt-*`); no new `.css` files, no hard-coded colors — bespoke CSS is sanctioned only in the surviving custom components (`Notice`, `PageHeader`, `ProductThumb`), which use `--mud-palette-*` variables.
  - Component choices are fixed: buttons `MudButton` (sentence case), tables `MudTable` (not `MudDataGrid`), dialogs inline `MudDialog @bind-Visible` for form dialogs, toasts `ISnackbar`, forms `MudTextField`/`MudSelect`/`MudNumericField`, icons `Icons.Material.*` (brand SVG excepted).
  - Update the frontmatter description so the trigger still fires on any UI/Razor/styling work but names MudBlazor instead of tokens/primitives.

- [ ] **Step 3: Update `docs/adr/0010-native-dialog-primitive.md`** status line to `Superseded by [ADR-0014](0014-adopt-mudblazor.md)` (body untouched), and index both 0014 + the 0010 annotation in `docs/adr/README.md`.

- [ ] **Step 4: Update `CLAUDE.md`.** The `Atrium.Design` description becomes "the shared design-system RCL (MudBlazor + `AtriumTheme` + a few custom components + `AccessTokenHolder`)"; ADR range → "0001–0014"; the skills table row for atrium-ui keeps its "never hand-roll styling" framing.

- [ ] **Step 5: Update `docs/ARCHITECTURE.md` + `AGENTS.md`.** Search for `BEM`, `token`, `atrium.css`, `primitive`, `dialog.js`; rewrite those passages to describe `AtriumTheme` + MudBlazor (one honest line that the system migrated pre-demo, per ADR-0014).

- [ ] **Step 6: Tick the interview item.** In `docs/interview/07-CLARIFICATIONS.md`:

```markdown
- [x] **Reimplement the UI with MudBlazor.** Done 2026-07 (ADR-0014). The `Dialog`/`Modal`
  primitive: replaced by `MudDialog` (inline `@bind-Visible` for the admin form dialog) — ADR-0010
  superseded. JS interop after: `cart-storage.js` + 15-line `theme.js` + the framework reconnect
  modal; `dialog.js` deleted.
```

Also append a one-line pointer to ADR-0014 at the end of the §03 BEM answer ("the fork was taken deliberately — see ADR-0014").

- [ ] **Step 7: Gate + commit** — `git add -A && git commit -m "docs: ADR-0014 adopt MudBlazor; supersede ADR-0010; rewrite atrium-ui skill; sweep design-system references"`

---

### Task 9: Final Lane B sweep

**Files:** artifacts only (`artifacts/mudblazor-migration/final/`)

- [ ] **Step 1: Boot the stack** (Global Constraints) and run the full functional pass:
  1. `testuser`/`password`: home → shop (filter, add ×2, snackbar) → cart (qty edit) → checkout → confirmation → orders. No Admin/Reports nav or cards.
  2. `admin`/`password`: admin table; create product "Smoke Test Lamp" (price 19.99); edit it; see both snackbars; reports dashboard renders stats + bars; delete/cleanup not required (seed data is disposable).
  3. Deep-link check: hard-refresh on `/storefront/cart` renders (both routers still registered).
  4. Session pages: bogus URL → NotFound.
  5. Zero browser console errors on every page.
- [ ] **Step 2: Theme/responsive sweep:** for **every** page above: screenshot light, toggle dark, screenshot dark; then at 390×844 assert the drawer is closed by default, opens via the menu button, and navigation works; screenshot each page at 390×844. All screenshots → `artifacts/mudblazor-migration/final/` named `page-theme-viewport.png`.
- [ ] **Step 3: LOG + wrap-up:** pass/fail per step; every `[~]` visual flag listed with its screenshot path; push `run/mudblazor-migration`; do **not** merge to `main` unattended.
