namespace EQBuddy.UI.Shared;

/// <summary>
/// The colors behind every theme, as data rather than per-UI resources.
///
/// Both UIs build their brushes from this one table: WPF composes a ResourceDictionary at
/// runtime and swaps it into Application.Resources, Avalonia mutates its brush singletons.
/// They used to hold separate copies — XAML dictionaries on one side, a hex table on the
/// other — which nothing kept in step, so a theme tweak could silently reach only one UI.
///
/// A theme is a full set of <see cref="Keys"/>: partial palettes aren't allowed, because a
/// missing key resolves to nothing and paints an invisible control rather than failing
/// loudly. <c>ThemePaletteTests</c> enforces that, and that every value parses.
/// </summary>
public static class ThemePalettes
{
    /// <summary>Brush resource keys, in declaration order; <see cref="Values"/> rows follow
    /// this order. The names are the WPF resource keys, which the Avalonia side maps onto
    /// its own brush singletons — it deliberately implements a subset (it doesn't style
    /// scrollbar thumbs or toggle highlights), and simply ignores keys it has no brush for.</summary>
    public static readonly string[] Keys =
    [
        "BgBrush", "PanelBrush", "PanelHoverBrush", "BorderBrush", "TextBrush", "DimBrush",
        "AccentBrush", "GoodBrush", "BadBrush", "WarnBrush", "PopupBrush", "ComboBoxBrush",
        "ToggleHighlightBrush", "ScrollThumbBrush", "ScrollThumbHoverBrush",
        "GoodWashBrush", "WarnWashBrush",
        // 2026-08-11 modernization: incoming damage wears a COOL hue in every theme so
        // it can never be misread as your own output (which wears the accent). The other
        // new tones of the redesign — hairlines, bar tracks, raised chips — are alpha
        // derivations of these keys, composed by each UI (WPF: ThemeManager), not rows here.
        "IncomingBrush",
        // 2026-08-13 chart pass (David's approved mockup): data SERIES get their own
        // steps, separate from the ambient UI accents — deeper cuts that hold up as
        // 2px lines and area fills. The ParchmentBrass set passed a five-check
        // CVD/contrast validation (you #B4892C · incoming #4796DB · pet #5CA352,
        // legend-ordered blue-between-brass-and-green); other themes carry hue-family
        // derivations. Crit is the one bright accent — one hue, one meaning.
        "ChartYouBrush", "ChartPetBrush", "ChartIncomingBrush", "ChartCritBrush",
        // DRA-352 D1: the mez family's chicklet ink — "mez counts down in blue". A resource
        // per palette so the renderer never carries a hex; every value clears 4.5:1 on its
        // own BgBrush (ThemePaletteTests), because it is 11px text over a moving game.
        "MezChipBrush",
    ];

    /// <summary>Hex values per theme key, each row in <see cref="Keys"/> order. #AARRGGBB —
    /// the alpha matters: panel and wash colors are deliberately translucent so the game
    /// shows through, and the backgrounds carry the widget's own see-through alpha.</summary>
    private static readonly Dictionary<string, string[]> Values = new()
    {
        // Dark parchment-and-brass fantasy — the original EQBuddy look, and the default.
        ["ParchmentBrass"] =
        [
            "#F216130E", "#26FFFFFF", "#33FFD98C", "#66C9A227", "#FFEDE4D3", "#FF9C927F",
            "#FFE3B341", "#FF7FBF5F", "#FFD9634F", "#FFE0A030", "#FF262119", "#FF2A251F",
            "#33E3B341", "#40FFD98C", "#8CFFD98C", "#337FBF5F", "#33E0A030", "#FF6FA8B8",
            "#FFB4892C", "#FF5CA352", "#FF4796DB", "#FFF0BC55", "#FF6CB4F0",
        ],
        // Cool dark slate/blue-grey.
        ["BlueGrey"] =
        [
            "#F2181C21", "#26FFFFFF", "#335C8AC2", "#665C7A99", "#FFE4E9EF", "#FF8B96A3",
            "#FF5FA8D3", "#FF6FBF7F", "#FFD9634F", "#FFE0A030", "#FF20242B", "#FF242830",
            "#335C8AC2", "#405C8AC2", "#8C5C8AC2", "#336FBF7F", "#33E0A030", "#FFB08FD9",
            "#FF4E8FC2", "#FF5CA352", "#FF9B7BD1", "#FFF0BC55", "#FF7CC8FF",
        ],
        ["Turquoise"] =
        [
            "#F2131C1C", "#26FFFFFF", "#3340C7B8", "#6629A99A", "#FFE0F2EF", "#FF87A6A0",
            "#FF3FCFBE", "#FF6FBF7F", "#FFD9634F", "#FFE0A030", "#FF16211F", "#FF1A2725",
            "#3340C7B8", "#4040C7B8", "#8C40C7B8", "#336FBF7F", "#33E0A030", "#FFA88FD9",
            "#FF35AB9E", "#FF5CA352", "#FF9B7BD1", "#FFF0BC55", "#FF6CB4F0",
        ],
        ["Redish"] =
        [
            "#F21F1615", "#26FFFFFF", "#33D96A55", "#66B34A3D", "#FFF2E2DE", "#FFA88A83",
            "#FFE0654A", "#FF7FBF5F", "#FFD9345F", "#FFE0A030", "#FF251815", "#FF291C18",
            "#33D96A55", "#40D96A55", "#8CD96A55", "#337FBF5F", "#33E0A030", "#FF6FA8B8",
            "#FFC75B43", "#FF5CA352", "#FF4796DB", "#FFF0BC55", "#FF6CB4F0",
        ],
        ["Grey"] =
        [
            "#F21A1A1A", "#26FFFFFF", "#33BFBFBF", "#66808080", "#FFEAEAEA", "#FF9C9C9C",
            "#FFC0C0C0", "#FF7FBF5F", "#FFD9634F", "#FFE0A030", "#FF232323", "#FF272727",
            "#33BFBFBF", "#40BFBFBF", "#8CBFBFBF", "#337FBF5F", "#33E0A030", "#FF6FA8B8",
            "#FFA8A8A8", "#FF5CA352", "#FF4796DB", "#FFF0BC55", "#FF6CB4F0",
        ],
        // Solarized Light — Ethan Schoonover's base3/base2/base01/base00 scale. The only
        // light theme, and so the one that catches any color still hardcoded dark.
        // Body text is base01 (#586E75), Solarized's "emphasized" tone, not the usual body
        // base00 (#657B83): base00 on base3 measures 4.1:1, under WCAG AA, and this is 11px
        // text over a moving game. ThemePaletteTests enforces the 4.5:1 floor.
        ["Solarized"] =
        [
            "#F2FDF6E3", "#14002B36", "#33268BD2", "#66586E75", "#FF586E75", "#FF93A1A1",
            "#FF268BD2", "#FF859900", "#FFDC322F", "#FFCB4B16", "#FFEEE8D5", "#FFEEE8D5",
            "#33268BD2", "#40586E75", "#8C586E75", "#33859900", "#33CB4B16", "#FF6C71C4",
            "#FF1E6EA7", "#FF6E8B00", "#FF6C71C4", "#FFB58900", "#FF1E6EA7",
        ],
        ["SolarizedDark"] =
        [
            "#F2002B36", "#26FFFFFF", "#33268BD2", "#66586E75", "#FF839496", "#FF586E75",
            "#FF268BD2", "#FF859900", "#FFDC322F", "#FFCB4B16", "#FF073642", "#FF0A3C48",
            "#33268BD2", "#40268BD2", "#8C268BD2", "#33859900", "#33CB4B16", "#FF6C71C4",
            "#FF268BD2", "#FF859900", "#FF6C71C4", "#FFB58900", "#FF5FB0EC",
        ],
        // Maximum readability over a bright moving game (field feedback 2026-08-03:
        // "light grey on dark grey"). Near-opaque background — the translucency that
        // makes the other themes pretty is what washes them out — plus pure white
        // text, a bright dim tier (~10:1), and stronger borders and status colors.
        ["HighContrast"] =
        [
            "#FC0A0A0A", "#33FFFFFF", "#4DFFD24D", "#CCFFFFFF", "#FFFFFFFF", "#FFCFCFCF",
            "#FFFFD24D", "#FF66E060", "#FFFF6659", "#FFFFB84D", "#FF101010", "#FF161616",
            "#4DFFD24D", "#59FFFFFF", "#A6FFFFFF", "#4066E060", "#40FFB84D", "#FF7FD4FF",
            "#FFFFD24D", "#FF66E060", "#FF7FD4FF", "#FFFFFFFF", "#FF7FD4FF",
        ],
        // The user-colored theme. This row is the seed (Grey neutrals + brass accent),
        // shown until colors are picked; with colors set, CustomTheme.PaletteFor
        // derives the live palette and this row is only the fallback.
        [CustomTheme.Key] = CustomTheme.SeedRow,
    };

    /// <summary>Theme keys that have a palette — the same set, and order, as
    /// <see cref="ThemeCatalog.Themes"/>.</summary>
    public static IEnumerable<string> DefinedThemes => Values.Keys;

    /// <summary>Key → hex for one theme. An unrecognized theme falls back to the first
    /// entry in <see cref="ThemeCatalog"/> rather than throwing, so an older or
    /// hand-edited settings.json still paints something.</summary>
    public static IEnumerable<(string Key, string Hex)> For(string themeKey)
    {
        var resolved = ThemeCatalog.Themes[ThemeCatalog.IndexOf(themeKey)].Key;
        var row = Values[resolved];
        for (var i = 0; i < Keys.Length; i++) yield return (Keys[i], row[i]);
    }
}
