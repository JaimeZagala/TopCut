using MudBlazor;

namespace TopCut.Web.Theme;

/// <summary>
/// The application's single visual identity. Every layout hands this same instance to
/// <c>MudThemeProvider</c>, so a colour is changed in one place and nowhere else.
/// <para>
/// Deliberately neutral (docs/arquitectura.md): per-store branding is a later feature, and the
/// screen that matters most — the organiser's live view, used under pressure mid-tournament —
/// needs contrast and calm, not personality.
/// </para>
/// </summary>
public static class TopCutTheme
{
    /// <summary>
    /// The system font stack. No Google Fonts link on purpose: an external font request would send
    /// every visitor's IP address to a third party on page load, which is an awkward thing to do on
    /// the same page that shows the data-protection notice. It also keeps the app readable offline.
    /// </summary>
    private static readonly string[] FontStack =
    [
        "Segoe UI Variable Text",
        "Segoe UI",
        "system-ui",
        "-apple-system",
        "Roboto",
        "Helvetica Neue",
        "Arial",
        "sans-serif"
    ];

    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#4338CA",
            Secondary = "#C2410C",
            Tertiary = "#0F766E",
            AppbarBackground = "#312E81",
            AppbarText = "#F8FAFC",
            Background = "#F8FAFC",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            TextPrimary = "#1E293B",
            TextSecondary = "#52607A",
            Success = "#15803D",
            Warning = "#B45309",
            Error = "#B91C1C",
            Info = "#1D4ED8",
            LinesDefault = "#E2E8F0",
            TableLines = "#E2E8F0"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#A5B4FC",
            Secondary = "#FDBA74",
            Tertiary = "#5EEAD4",
            AppbarBackground = "#1E1B4B",
            AppbarText = "#E2E8F0",
            Background = "#0F172A",
            Surface = "#1E293B",
            DrawerBackground = "#1E293B",
            TextPrimary = "#E2E8F0",
            TextSecondary = "#94A3B8",
            Success = "#4ADE80",
            Warning = "#FBBF24",
            Error = "#F87171",
            Info = "#60A5FA",
            LinesDefault = "#334155",
            TableLines = "#334155"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FontStack },
            H1 = new H1Typography { FontFamily = FontStack, FontSize = "2.25rem", FontWeight = "600" },
            H2 = new H2Typography { FontFamily = FontStack, FontSize = "1.75rem", FontWeight = "600" },
            H3 = new H3Typography { FontFamily = FontStack, FontSize = "1.5rem", FontWeight = "600" },
            H4 = new H4Typography { FontFamily = FontStack, FontSize = "1.25rem", FontWeight = "600" },
            H5 = new H5Typography { FontFamily = FontStack, FontSize = "1.125rem", FontWeight = "600" },
            H6 = new H6Typography { FontFamily = FontStack, FontSize = "1rem", FontWeight = "600" },
            Button = new ButtonTypography { FontFamily = FontStack, TextTransform = "none" }
        }
    };
}
