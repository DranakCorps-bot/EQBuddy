namespace EQBuddy.UI.Shared;

/// <summary>How the text stack is asked to position glyphs.</summary>
public enum TextLayoutMode
{
    /// <summary>WPF's default. Glyph advances stay fractional, so a string measures
    /// and scales accurately — at the cost of needing the rasteriser underneath to
    /// place glyphs at sub-pixel positions.</summary>
    Ideal,

    /// <summary>GDI-compatible whole-pixel advances. Less accurate under a scale
    /// transform, and the only one Wine renders correctly.</summary>
    Display,
}

/// <summary>
/// Which glyph-positioning model each environment gets, as a pure decision so it can be
/// tested — the WPF layer that applies it has no unit tests (docs/TestPlan.md §5).
///
/// **Why this exists.** A CrossOver player reported text "with kerning issues" on
/// 2026-08-21. It was not kerning, and it was not the font: the bundled font's advances
/// are correct to a pixel over a 63-character line, all eleven word spaces landed where
/// the font predicts, and rendering the same string from the same .ttf outside WPF is
/// clean. What was wrong was five 1-2px gaps INSIDE words — "an d th is", "bun dles",
/// "Win e".
///
/// A probe built into the app (TextProbeWindow) rendered one sentence under all eight
/// TextFormattingMode x TextRenderingMode combinations under CrossOver, and the answer
/// was flat: every <see cref="TextLayoutMode.Ideal"/> cell split the 12 words into 17
/// pieces, every <see cref="TextLayoutMode.Display"/> cell into exactly 12, and
/// TextRenderingMode changed nothing at all in either. Ideal also measured NARROW
/// (359px against the font's 361.9px), which is the tell: Wine is truncating each
/// glyph's fractional advance rather than carrying it, so the text creeps left until
/// the accumulated error flushes as a visible gap mid-word.
///
/// So Wine gets Display and nothing else changes. Native Windows keeps Ideal, which is
/// WPF's default, is correct there, and is the better choice under the widget's UI-scale
/// LayoutTransform (trap 1) — Display snaps to whole pixels before that transform is
/// applied, so scaled text goes slightly soft. That trade is worth taking only where
/// Ideal is visibly broken, which is why this is a per-environment decision and not a
/// new global default. Making Display the default everywhere is a product call with a
/// blast radius of every surface on every platform; it is not this.
/// </summary>
public static class TextRenderingPolicy
{
    /// <summary>Escape hatch, both ways. A Wine setup that prefers Ideal (or a Windows
    /// one that wants Display's crisper small text) says so here rather than needing a
    /// build. Anything unrecognised falls through to the environment's own answer, so a
    /// typo cannot leave the app with no text policy at all.</summary>
    public const string OverrideVariable = "EQBUDDY_TEXTMODE";

    /// <summary>Three inputs, in strict precedence: the environment variable is a debug
    /// escape hatch and outranks everything; then the player's own switch, which only
    /// has meaning where the choice exists; then the environment, which is the only
    /// thing that decides it on Windows.
    ///
    /// <paramref name="wholePixelText"/> is <c>AppSettings.WineWholePixelText</c>. It is
    /// deliberately ignored off Wine rather than merely defaulted: Ideal is correct on
    /// Windows and there is nothing there for the switch to fix, so a profile carried
    /// from a Wine machine cannot turn a Windows widget's text mode into a setting the
    /// Windows UI never offered to change.</summary>
    public static TextLayoutMode Decide(bool underWine, bool wholePixelText, string? overrideValue)
    {
        var requested = overrideValue?.Trim();
        if (string.Equals(requested, "display", StringComparison.OrdinalIgnoreCase))
            return TextLayoutMode.Display;
        if (string.Equals(requested, "ideal", StringComparison.OrdinalIgnoreCase))
            return TextLayoutMode.Ideal;
        if (!underWine) return TextLayoutMode.Ideal;
        return wholePixelText ? TextLayoutMode.Display : TextLayoutMode.Ideal;
    }
}
