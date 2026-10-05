namespace EQBuddy.UI.Shared;

/// <summary>One entry of the Font picker. <see cref="Key"/> is what
/// <c>AppSettings.AppFont</c> stores; <see cref="Family"/> is the face's typographic family
/// name; <see cref="BundledFolder"/> is the folder under <c>src/EQBuddy/Fonts/</c> that ships
/// it, or null for a face Windows itself provides.</summary>
public sealed record AppFontOption(string Key, string Label, string Family, string? BundledFolder)
{
    public bool IsBundled => BundledFolder is not null;
}

/// <summary>What the app will actually draw in, and — when that is not what was picked —
/// the sentence that says why. A pick that silently became the default would be a silent
/// no-op (CLAUDE.md), so the reason is part of the answer rather than a log line.</summary>
public sealed record AppFontResolution(AppFontOption Picked, AppFontOption Drawn, string? Reason)
{
    public bool FellBack => !ReferenceEquals(Picked, Drawn);
}

/// <summary>
/// **The Font picker's curated list, and the one rule that turns a saved key into a face**
/// (discussion #1046, Miss Outlaw; DRA-1048). Framework-free so it can be unit-tested; the
/// WPF half is <c>EQBuddy/AppFont.cs</c>, which swaps the <c>AppFontFamily</c> resource every
/// TextBlock already reads — ONE producer of the app's face (trap 4), so the widget, the HUD
/// chips, the alert banner and every window follow one setting.
///
/// **Curated, not "every installed font"** (Planner, DRA-1048 decision 1): every entry here
/// can be screenshot-reviewed, and an arbitrary family is one nobody has looked at in a
/// 10-px chip. Two are bundled (both SIL OFL 1.1, three weights each — WPF synthesises a
/// missing weight, see <c>WineFonts</c>); two ship with Windows and are CHECKED, because a
/// stripped install that lacks one must not turn the pick into a silent no-op.
///
/// **The fallback chain always ends in Segoe UI**, so a glyph the chosen face lacks (the
/// 💀/🔮 section icons, emoji) still falls back the way it does today on native Windows.
/// Mono sites (<c>Consolas</c>, <c>MonoFamily</c>) do not read this and stay mono.
/// </summary>
public static class AppFontChoice
{
    /// <summary>The default face — Theme.xaml's own <c>AppFontFamily</c> value, and the tail
    /// of every other choice's chain.</summary>
    public const string DefaultFamilies = "Segoe UI Variable Text, Segoe UI";

    public static readonly AppFontOption Default =
        new("", "Default (Segoe UI)", "Segoe UI", null);

    /// <summary>The list, in the order the picker shows it. Pinned by
    /// <c>AppFontChoiceTests</c>: a row added here is a face somebody must screenshot.</summary>
    public static readonly IReadOnlyList<AppFontOption> Options =
    [
        Default,
        new("atkinson-hyperlegible-next", "Atkinson Hyperlegible Next", "Atkinson Hyperlegible Next",
            "AtkinsonHyperlegibleNext"),
        new("open-sans", "Open Sans", "Open Sans", "OpenSans"),
        new("verdana", "Verdana", "Verdana", null),
        new("tahoma", "Tahoma", "Tahoma", null),
    ];

    /// <summary>The picker's row label.</summary>
    public const string Label = "Font";

    /// <summary>The line under the picker.</summary>
    public const string Tip = "Changes the text in the widget, chips, alerts and windows.";

    /// <summary>Why the picker is dimmed under Wine (trap 17: a disabled control says why).
    /// Wine's DirectWrite reads only the FIRST font in a list, so any face but the bundled
    /// icon font boxes every section icon — see <c>WineFonts</c>.</summary>
    public const string WineTip =
        "Under Wine EQBuddy keeps its own font, because Wine cannot fall back to a second font "
        + "for the section icons and they would draw as empty boxes.";

    /// <summary>The sentence for a Windows font that is not installed on this PC.</summary>
    public static string NotInstalled(AppFontOption option) =>
        $"{option.Label} is not installed on this PC, so EQBuddy is using {Default.Label}.";

    /// <summary>The option a saved key names; blank, null and unknown keys are the default.
    /// Case-insensitive, because a hand-edited settings.json is a real writer.</summary>
    public static AppFontOption Find(string? key) =>
        Options.FirstOrDefault(o => o.Key.Length > 0
            && string.Equals(o.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;

    /// <summary>The one rule. A bundled face is always present (it is packed in the .exe); a
    /// Windows face is used only when <paramref name="isInstalled"/> says this PC has it,
    /// and otherwise falls back to the default WITH the sentence saying so.</summary>
    public static AppFontResolution Resolve(string? key, Func<string, bool> isInstalled)
    {
        var picked = Find(key);
        if (picked == Default || picked.IsBundled || isInstalled(picked.Family))
            return new AppFontResolution(picked, picked, null);
        return new AppFontResolution(picked, Default, NotInstalled(picked));
    }

    /// <summary>The family string WPF is handed: the chosen face first, then the default chain.
    /// A bundled face is addressed inside the app's own resources
    /// (<c>./Fonts/&lt;folder&gt;/#Family</c>, resolved against the pack base URI); a system
    /// face by name.</summary>
    public static string FamilySource(AppFontOption option) =>
        option == Default ? DefaultFamilies
        : option.IsBundled ? $"./Fonts/{option.BundledFolder}/#{option.Family}, {DefaultFamilies}"
        : $"{option.Family}, {DefaultFamilies}";
}
