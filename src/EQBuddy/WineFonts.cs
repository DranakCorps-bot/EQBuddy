using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace EQBuddy;

/// <summary>
/// Wine prefixes ship no Segoe fonts, and Wine's DirectWrite consults ONLY the
/// primary font's cmap for WPF apps — no system fallback, and no per-character
/// traversal even within an explicit family list (field-tested 2026-08-14: an
/// icon-only font listed first rendered its own glyphs and boxed every Latin
/// letter). So every glyph the app draws — text AND the 💀/🔮/💰 section icons
/// (issue #8's Wine players; CrossOver on macOS alike) — must live in one font.
/// The app bundles exactly that: an OFL Noto Sans base with the icon glyphs
/// merged in (rebuilt by scripts/build-icon-font.py whenever a new icon appears —
/// a unit test pins the coverage), swapped in only when actually running under
/// Wine. Native Windows never takes this path and keeps Segoe UI Variable and its
/// color emoji untouched.
///
/// **The family ships three weights** (Regular 400, SemiBold 600, Bold 700), and
/// that is not a nicety — the app names SemiBold or Bold in 71 places. WPF resolves
/// a FontWeight to a face by usWeightClass, and when the family has no face to
/// resolve to it SYNTHESISES one: the Regular outlines smeared wider, with their
/// sidebearings and kern pairs untouched. Shipping Regular alone therefore fixed
/// the boxed icons and left every bold run in the widget mis-fitted, which is what
/// was reported from CrossOver on macOS on 2026-08-21 as "the kerning is off" (found
/// by reading the font's tables; nothing on Windows can show it). The family string
/// below names no weight because it does not have to — WPF picks the face from the
/// FontWeight on each control, and BundledFontFaceTests asserts all three exist,
/// group under one typographic family, and each carry the icons.
/// </summary>
internal static class WineFonts
{
    /// <summary>Swaps AppFontFamily to the bundled font under Wine. Called before
    /// any window exists, so the first frame is already correct.</summary>
    public static void ApplyIfNeeded(ResourceDictionary appResources)
    {
        if (!IsWine()) return;
        if (PrefixSegoeDrawsIcons()) return;
        appResources["AppFontFamily"] = new FontFamily(
            new Uri("pack://application:,,,/"),
            "./Fonts/#EQBuddy Sans, Segoe UI Variable Text, Segoe UI");
    }

    /// <summary>A prefix whose installed "Segoe UI Variable Text" can draw the
    /// widget's icons gets the authentic look instead of the bundled stand-in —
    /// Wine players who go to the trouble of installing a Segoe(-alike) with icon
    /// coverage shouldn't be overruled by it. Two deliberate strictnesses: the
    /// check names the PRIMARY family specifically, because Wine's DirectWrite
    /// never walks past the first family in a list, so "Segoe UI" alone would
    /// still box; and it demands a real icon glyph (💀, the canary), because a
    /// genuine Segoe copied in without icon coverage would bring the empty boxes
    /// back — that prefix keeps the bundled font.</summary>
    private static bool PrefixSegoeDrawsIcons()
    {
        try
        {
            foreach (var family in Fonts.SystemFontFamilies)
            {
                if (!string.Equals(family.Source, "Segoe UI Variable Text",
                        StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var typeface in family.GetTypefaces())
                    if (typeface.TryGetGlyphTypeface(out var glyphs) &&
                        glyphs.CharacterToGlyphMap.ContainsKey(0x1F480))
                        return true;
                return false;
            }
        }
        catch
        {
            // Font cosmetics must never stop startup.
        }
        return false;
    }

    /// <summary>The canonical Wine check: ntdll exports wine_get_version under
    /// Wine and never on real Windows. No environment variables, no guessing.</summary>
    private static bool IsWine()
    {
        try
        {
            var ntdll = GetModuleHandleW("ntdll.dll");
            return ntdll != IntPtr.Zero &&
                   GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
        }
        catch
        {
            // Font cosmetics must never stop startup.
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);
}
