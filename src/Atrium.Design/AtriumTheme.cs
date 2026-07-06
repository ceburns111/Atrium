using MudBlazor;

namespace Atrium.Design;

/// <summary>
/// Shared MudBlazor theme for the Atrium platform — brand palette and typography ported
/// value-for-value from <c>tokens.css</c>.  Pass <see cref="Instance"/> to
/// <c>MudThemeProvider.Theme</c>; the light / dark palettes are resolved by the provider
/// based on <c>IsDarkMode</c>.
/// </summary>
public static class AtriumTheme
{
    // Font-family stacks — mirrored from tokens.css --font-* custom properties.
    private static readonly string[] FontDisplay =
    [
        "Space Grotesk",
        "Inter",
        "system-ui",
        "sans-serif",
    ];
    private static readonly string[] FontSans =
    [
        "Inter",
        "system-ui",
        "-apple-system",
        "Segoe UI",
        "Roboto",
        "sans-serif",
    ];
    public static readonly MudTheme Instance = new()
    {
        // ── Palette: Light (tokens :root) ───────────────────────────────────────
        PaletteLight = new PaletteLight
        {
            // Brand accent — deep atrium teal-green
            Primary = "#117b68",
            PrimaryContrastText = "#ffffff", // --on-accent light

            // Neutral ramp
            Background = "#fbfbfa", // --paper
            Surface = "#ffffff", // --surface
            AppbarBackground = "#fbfbfa", // --paper (shell topbar)
            DrawerBackground = "#fbfbfa", // --paper (shell sidebar)

            // Text
            TextPrimary = "#18181b", // --ink
            TextSecondary = "#71717a", // --muted
            TextDisabled = "#a1a1aa", // --faint
            AppbarText = "#18181b", // --ink

            // Lines / dividers
            LinesDefault = "#e7e7e4", // --line
            LinesInputs = "#e7e7e4", // --line
            TableLines = "#e7e7e4", // --line
            Divider = "#e7e7e4", // --line

            // Status
            Success = "#16785a", // --success
            Warning = "#8a5a0f", // --warning
            Error = "#a23b3b", // --danger
        },

        // ── Palette: Dark (tokens :root[data-theme="dark"]) ────────────────────
        PaletteDark = new PaletteDark
        {
            // Brand accent — luminous teal on dark
            Primary = "#2dbd9b",
            PrimaryContrastText = "#08211b", // --on-accent dark

            // Neutral ramp (inverted zinc)
            Background = "#131316", // --paper dark
            Surface = "#1b1b1f", // --surface dark
            AppbarBackground = "#131316", // --paper dark (shell topbar)
            DrawerBackground = "#131316", // --paper dark (shell sidebar)

            // Text
            TextPrimary = "#f4f4f5", // --ink dark
            TextSecondary = "#a1a1aa", // --muted dark
            TextDisabled = "#71717a", // --faint dark
            AppbarText = "#f4f4f5", // --ink dark

            // Lines / dividers
            LinesDefault = "#2c2c31", // --line dark
            LinesInputs = "#2c2c31", // --line dark
            TableLines = "#2c2c31", // --line dark
            Divider = "#2c2c31", // --line dark

            // Status (lightened for legibility on dark surfaces)
            Success = "#35c99a", // --success dark
            Warning = "#e0a94a", // --warning dark
            Error = "#e07070", // --danger dark
        },

        // ── Typography ──────────────────────────────────────────────────────────
        // Scale anchored to tokens.css: xs=0.75rem, sm=0.8125rem, base=0.9375rem,
        // md=1.0625rem, lg=1.375rem, xl=1.9375rem.  Heading sizes extrapolate upward
        // from xl using a Major-Third ratio for display impact.
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = FontSans,
                FontSize = "0.9375rem", // --text-base
                FontWeight = "400",
                LineHeight = "1.55", // --leading-normal
                LetterSpacing = "normal",
            },

            // Headings — Space Grotesk display face
            H1 = new H1Typography
            {
                FontFamily = FontDisplay,
                FontSize = "3rem",
                FontWeight = "700",
                LineHeight = "1.15", // --leading-tight
                LetterSpacing = "-0.02em",
            },
            H2 = new H2Typography
            {
                FontFamily = FontDisplay,
                FontSize = "2.5rem",
                FontWeight = "700",
                LineHeight = "1.15",
                LetterSpacing = "-0.01em",
            },
            H3 = new H3Typography
            {
                FontFamily = FontDisplay,
                FontSize = "2rem",
                FontWeight = "600",
                LineHeight = "1.15",
                LetterSpacing = "-0.01em",
            },
            H4 = new H4Typography
            {
                FontFamily = FontDisplay,
                FontSize = "1.9375rem", // --text-xl
                FontWeight = "600",
                LineHeight = "1.15",
                LetterSpacing = "normal",
            },
            H5 = new H5Typography
            {
                FontFamily = FontDisplay,
                FontSize = "1.375rem", // --text-lg
                FontWeight = "600",
                LineHeight = "1.15",
                LetterSpacing = "normal",
            },
            H6 = new H6Typography
            {
                FontFamily = FontDisplay,
                FontSize = "1.0625rem", // --text-md
                FontWeight = "600",
                LineHeight = "1.55",
                LetterSpacing = "normal",
            },

            // Subtitles — Space Grotesk (sub-heading role)
            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = FontDisplay,
                FontSize = "1.0625rem", // --text-md
                FontWeight = "500",
                LineHeight = "1.55",
                LetterSpacing = "normal",
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontFamily = FontDisplay,
                FontSize = "0.9375rem", // --text-base
                FontWeight = "500",
                LineHeight = "1.55",
                LetterSpacing = "normal",
            },

            // Body — Inter sans
            Body1 = new Body1Typography
            {
                FontFamily = FontSans,
                FontSize = "0.9375rem", // --text-base
                FontWeight = "400",
                LineHeight = "1.55",
                LetterSpacing = "normal",
            },
            Body2 = new Body2Typography
            {
                FontFamily = FontSans,
                FontSize = "0.8125rem", // --text-sm
                FontWeight = "400",
                LineHeight = "1.55",
                LetterSpacing = "normal",
            },

            // Interactive / utility — Inter sans
            Button = new ButtonTypography
            {
                FontFamily = FontSans,
                FontSize = "0.8125rem", // --text-sm
                FontWeight = "500",
                LineHeight = "1.55",
                LetterSpacing = "0.04em",
                TextTransform = "none",
            },
            Caption = new CaptionTypography
            {
                FontFamily = FontSans,
                FontSize = "0.75rem", // --text-xs
                FontWeight = "400",
                LineHeight = "1.55",
                LetterSpacing = "normal",
            },
            Overline = new OverlineTypography
            {
                FontFamily = FontSans,
                FontSize = "0.75rem", // --text-xs
                FontWeight = "600",
                LineHeight = "1.55",
                LetterSpacing = "0.08em", // --tracking-label
                TextTransform = "uppercase",
            },
        },

        // ── Layout ──────────────────────────────────────────────────────────────
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px", // --r-md
        },
    };
}
