using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **The player's own colour per damage/healing TYPE** (David, 2026-09-29: "in options we can
/// let people color code the types to whichever color they want from a color wheel").
///
/// A pick lives in <see cref="AppSettings.KindColours"/> — the kind's enum NAME → "#RRGGBB" —
/// and applies to that type in EVERY theme, overriding both the dark and the light default.
/// It reaches every surface through the ONE producer the defaults already use: the pick rides
/// the palette as an explicit <c>Kind*Brush</c> row (<see cref="PaletteRows"/>, appended by
/// <see cref="CustomTheme.PaletteFor"/>), and <see cref="ThemeTones.Derive"/> lets an explicit
/// row win over its default. So the desktop's resource dictionary, the phone's first frame
/// (<c>CompanionHost</c>'s constructor) and every later broadcast (<c>PaletteApplied</c>) all
/// read the same answer, and nothing downstream knows a pick exists.
///
/// **Keyed by the kind, never by a position** — a row's colour is its TYPE's, whatever order
/// the rows or the legend happen to be in.
/// </summary>
public static class KindColours
{
    /// <summary>The player's valid pick for a kind as "#RRGGBB", or null — absent, or a value
    /// that does not read as a colour (ignored, never thrown on: a hand-edited settings file
    /// must not stop the widget painting).</summary>
    public static string? Pick(IReadOnlyDictionary<string, string>? picks, OutputKind kind)
    {
        if (picks is null) return null;
        foreach (var (name, hex) in picks)
            if (string.Equals(name, kind.ToString(), StringComparison.OrdinalIgnoreCase))
                return CustomTheme.Valid(hex);
        return null;
    }

    /// <summary>The palette rows the picks add: one explicit <c>Kind*Brush</c> per valid pick,
    /// opaque, in <see cref="OutputKindPresentation.Order"/>. Empty when nothing is picked, so
    /// a profile that never opened the block gets exactly the palette it always got.</summary>
    public static IEnumerable<(string Key, string Hex)> PaletteRows(IReadOnlyDictionary<string, string>? picks)
    {
        foreach (var kind in OutputKindPresentation.Order)
            if (Pick(picks, kind) is { } hex)
                yield return (OutputKindPresentation.BrushKey(kind), "#FF" + hex[1..]);
    }

    /// <summary>The colour a kind is drawn in under a theme's defaults and the picks — the
    /// Options swatch's answer, from the same derivation the meters read.</summary>
    public static string Effective(AppSettings settings, OutputKind kind)
    {
        var key = OutputKindPresentation.BrushKey(kind);
        var hex = ThemeTones.Derive(CustomTheme.PaletteFor(settings)).Last(e => e.Key == key).Hex;
        var (_, r, g, b) = ThemeTones.Parse(hex);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>Records (or, with null, clears) a pick. The dictionary is REPLACED rather than
    /// mutated, so the write is visible to <c>DeadSettingTests</c>' scan and a deserialized
    /// dictionary's comparer never matters. An invalid hex clears nothing and writes nothing.</summary>
    public static void Set(AppSettings settings, OutputKind kind, string? hex)
    {
        var next = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in settings.KindColours)
            if (!string.Equals(name, kind.ToString(), StringComparison.OrdinalIgnoreCase))
                next[name] = value;
        if (hex is not null)
        {
            if (CustomTheme.Valid(hex) is not { } valid) return;
            next[kind.ToString()] = valid;
        }
        settings.KindColours = next;
    }

    /// <summary>Clears every pick — "Reset all".</summary>
    public static void ResetAll(AppSettings settings) => settings.KindColours = [];

    /// <summary>Whether the player has a valid pick for this kind (the row's Reset shows).</summary>
    public static bool IsPicked(AppSettings settings, OutputKind kind) =>
        Pick(settings.KindColours, kind) is not null;
}
