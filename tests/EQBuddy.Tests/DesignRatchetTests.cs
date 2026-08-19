using System.Text.RegularExpressions;

namespace EQBuddy.Tests;

/// <summary>
/// THE MIGRATION RATCHET (docs/DesignSystem.md §5).
///
/// The Gate 1 audit counted 13 font sizes over 612 assignments, 174 distinct Thickness
/// tuples and 7 corner radii — and the reason that happened is not carelessness, it is
/// that nothing could DETECT it. A size nudged to make one row fit looks identical in a
/// diff to a size chosen from a scale.
///
/// So each migrated surface is added to <see cref="Migrated"/>, and from then on it may
/// not carry a literal font size, radius or spacing value: those come from
/// EQBuddy.UI.Shared.DesignTokens, which is the one place both UIs read them.
///
/// 0 and 1 stay legal. "No space" is not a spacing decision, and a 1px hairline or a
/// one-unit optical nudge is a rendering fact rather than a rhythm — putting those in
/// the scale would make the scale a lie.
///
/// **Add a surface to this list in the same PR that migrates it.** The list only ever
/// grows; that is what makes it a ratchet.
/// </summary>
public class DesignRatchetTests
{
    /// <summary>Surfaces rebuilt on the design system, and therefore held to it.
    /// Gate 2: Quests, both UIs. Gate 3: Spawns, both UIs. Gate 4: Loot — the card, the
    /// breakout, and the shared decisions behind both. And so on down §6/§11.5.
    ///
    /// A UI.Shared file earns a place here for the second check rather than the first: it
    /// has no sizes to grow, but it is where a surface's WORDS live, and a glyph typed
    /// into a heading there reaches every UI at once.</summary>
    private static readonly string[] Migrated =
    [
        "EQBuddy/QuestsWindow.xaml",
        "EQBuddy/QuestsWindow.xaml.cs",
        "EQBuddy.Avalonia/QuestsWindow.cs",
        "EQBuddy/SpawnsWindow.xaml",
        "EQBuddy/SpawnsWindow.xaml.cs",
        "EQBuddy.Avalonia/SpawnsWindow.cs",
        "EQBuddy.UI.Shared/LootPresentation.cs",
        "EQBuddy/LootCardView.cs",
        "EQBuddy/LootBreakoutView.cs",
        "EQBuddy.Avalonia/LootCardView.cs",
        // Gate 5: the widget's own markup — the FIRST widget file to join, and the one
        // the ratchet was written for. 473 violations were measured across the widget
        // files at the start of the gate. The two BreakoutWindow files
        // joined with it — the whole breakout surface, markup and code-behind.
        "EQBuddy/MainWindow.xaml",
        "EQBuddy/BreakoutWindow.xaml",
        "EQBuddy/BreakoutWindow.xaml.cs",
        // The 4,471-line hotspot, and the file §11.8 predicted could not join: it carried
        // ~74 glyphs, most of them inside user-facing STRINGS. Measuring rather than
        // guessing is what moved it — 56 were in COMMENTS (exempt, they never render),
        // and of what was left the largest group was CONTROLS that happen to be quoted:
        // icon-table entries, expander chevrons, a menu header. The genuinely editorial
        // remainder — text that NAMES a control, "click the 🗺", "under ⚙ Options" — was
        // reworded, because a tooltip that draws the glyph it is explaining draws it as
        // a box on the prefixes where the explanation is most needed.
        "EQBuddy/MainWindow.xaml.cs",
        // #217 Ask 1: the wiki contribution pack as its own surface. Built on the system
        // rather than migrated onto it, so it joins on the day it lands — the ✦ marker it
        // inherited from the old button is a vector here (IconPaths "Sparkle"), which is
        // exactly the dingbat that PRs #148/#166 exist because of.
        "EQBuddy.UI.Shared/WikiPackPresentation.cs",
        "EQBuddy/WikiPackWindow.xaml",
        "EQBuddy/WikiPackWindow.xaml.cs",
        "EQBuddy.Avalonia/WikiPackWindow.cs",
        // Gate 5, the other widget: 5,127 lines, 30 glyphs and 91 literal sizes, and the
        // build that actually runs under Wine — where PRs #148/#166 record emoji failing
        // to render AT ALL. Windows gave its glyphs up in 5c and this side kept them for
        // a fortnight, which is the parity gap CLAUDE.md's "neither surface is allowed to
        // quietly fall behind" is about.
        //
        // Two off-scale tuples were SNAPPED rather than re-decided, by copying the choice
        // the WPF twin already made: the hand-nudged KPI cell (11,6,4,7) and the grip
        // hairline (18,0,18,2). A migration that invents its own answer to a question the
        // other surface already settled is how the two drift again.
        "EQBuddy.Avalonia/MainWindow.cs",
        "EQBuddy.Avalonia/EqFoldLabel.cs",
    ];

    public static TheoryData<string> MigratedFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Migrated) data.Add(file);
        return data;
    }

    private static string SrcRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    /// <summary>Anything that is a bare number and at least 2 — the values that encode a
    /// design decision rather than a rendering fact.</summary>
    private static bool IsMagic(string token) =>
        double.TryParse(token.Trim().TrimEnd('d', 'f', 'D', 'F'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) && value >= 2;

    [Theory]
    [MemberData(nameof(MigratedFiles))]
    public void MigratedSurfacesCarryNoLiteralSizes(string relativePath)
    {
        var full = Path.Combine(SrcRoot, relativePath);
        Assert.True(File.Exists(full), $"Migrated surface moved or vanished: {full} — " +
            "update the path (or drop the entry) in DesignRatchetTests.Migrated.");

        var offences = new List<string>();
        var lines = File.ReadAllLines(full);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // C#: FontSize = 12.5
            foreach (Match m in Regex.Matches(line, @"\bFontSize\s*=\s*([^,;)\r\n]+)"))
                if (IsMagic(m.Groups[1].Value)) offences.Add($"{i + 1}: {line.Trim()}");

            // XAML: FontSize="12.5" / CornerRadius="10" / Margin="16,0,16,4" / Padding="6,4"
            foreach (Match m in Regex.Matches(line,
                         @"\b(FontSize|CornerRadius|Margin|Padding)=""([^""]*)"""))
                if (m.Groups[2].Value.Split(',').Any(IsMagic))
                    offences.Add($"{i + 1}: {line.Trim()}");

            // C#: new Thickness(...) / new CornerRadius(...) — each ARGUMENT must be a
            // token expression, not a bare number. "DesignTokens.StateRuleWidth / 2" is
            // an expression and passes; "8" does not.
            foreach (Match m in Regex.Matches(line, @"new (?:Thickness|CornerRadius)\(([^()]*)\)"))
                if (m.Groups[1].Value.Split(',').Any(IsMagic))
                    offences.Add($"{i + 1}: {line.Trim()}");
        }

        Assert.True(offences.Count == 0,
            $"{relativePath} carries literal sizes. Use a token from " +
            "EQBuddy.UI.Shared.DesignTokens (C#) or a {DynamicResource} into the token " +
            "dictionary (XAML) — see docs/DesignSystem.md §2. If the value genuinely is " +
            "not a design decision, it belongs in DesignTokens as a named one." +
            Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    /// <summary>Emoji and dingbats are out of the migrated CONTROLS (docs/DesignSystem.md
    /// §4): they render at a size and weight the app does not control, and PRs #148 and
    /// #166 exist because they failed to render at all in Wine prefixes — on the
    /// Linux/macOS builds that are EQBuddy's only uncontested ground.
    ///
    /// This scans for the families the audit counted. It deliberately allows the
    /// typographic marks that are TEXT rather than icons: "·" separates meta fragments,
    /// "×" multiplies a completion count, "→" joins a route, "≤" prefixes an era, "…"
    /// ends a placeholder. Those are words, not controls.
    ///
    /// **COMMENTS are exempt, and string literals are NOT** (measured 2026-08-18, when
    /// this decision was blocking Gate 5d). The rule exists because a glyph renders at a
    /// size and weight the app does not control, and fails to render AT ALL in some Wine
    /// prefixes. A glyph in a comment never renders, so the argument simply does not
    /// reach it — 56 of the glyphs left across the widget files were in comments, all of
    /// them non-issues inflating the count.
    ///
    /// A glyph in a STRING does render, so the Wine failure applies to it exactly as it
    /// applies to one in XAML — which is why exempting strings was rejected. It reads as
    /// the reasonable concession and is the loophole that would rot the rule: the largest
    /// single group of glyphs left in <c>MainWindow.xaml.cs</c> is not prose at all but
    /// CONTROLS passed as string arguments — <c>AppTheme.IconButton("⧉", …)</c>, the
    /// mini-bar's <c>"dps" =&gt; ("⚔", …)</c> icon table, expander chevrons, breakout
    /// window titles. Those are the rule's whole target, and they happen to be quoted.
    ///
    /// What remains after that is a genuinely small editorial set: text that NAMES a
    /// control elsewhere ("under ⚙ Options", "click the 🗺"). CLAUDE.md permits emoji in
    /// user-facing text that is content rather than UI, and those qualify — but they are
    /// still tofu on a Wine prefix, so they are worth rewording rather than exempting.</summary>
    [Theory]
    [MemberData(nameof(MigratedFiles))]
    public void MigratedSurfacesDrawIconsAsVectorsNotGlyphs(string relativePath)
    {
        const string allowed = "·×→←≤≥…‑–—’‘“”";
        var offences = new List<string>();
        var lines = File.ReadAllLines(Path.Combine(SrcRoot, relativePath));
        var inXmlComment = false;
        for (var i = 0; i < lines.Length; i++)
        {
            // XML comments span lines, and only the FIRST carries "<!--". Tracking the
            // block matters: MainWindow.xaml's comments run several lines each and their
            // continuations were being counted as offences, which inflates the number a
            // migration is measured against and hides the real ones among them.
            var line = lines[i];
            var opens = line.Contains("<!--", StringComparison.Ordinal);
            var closes = line.Contains("-->", StringComparison.Ordinal);
            var wasInComment = inXmlComment;
            if (opens && !closes) inXmlComment = true;
            else if (closes) inXmlComment = false;
            if (wasInComment || opens) continue;
            if (IsComment(line)) continue;
            foreach (var rune in lines[i].EnumerateRunes())
            {
                var value = rune.Value;
                var isIconish =
                    value is >= 0x2190 and <= 0x2BFF     // arrows, shapes, dingbats, symbols
                        or >= 0x1F300 and <= 0x1FAFF     // emoji
                        or 0x2122 or 0x00A9 or 0x00AE;
                if (isIconish && !allowed.Contains((char)Math.Min(value, 0xFFFF)))
                    offences.Add($"{i + 1}: U+{value:X4} {rune} — {lines[i].Trim()}");
            }
        }

        Assert.True(offences.Count == 0,
            $"{relativePath} draws with glyphs. Use DesignSystem.Icon / AppTheme.Icon with " +
            "a name from EQBuddy.UI.Shared.IconPaths instead — a glyph is a Wine bug " +
            "waiting to happen (#148, #166)." +
            Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    /// <summary>A whole-line C#-style comment. XML comment BLOCKS are tracked separately
    /// by the caller, because they span lines and only the first carries the marker.
    ///
    /// Deliberately conservative: only lines that OPEN as a comment count, so a glyph in
    /// trailing code before a `//` is still caught. A mid-line trailing comment is the
    /// one case this over-reports, and over-reporting is the safe direction.</summary>
    private static bool IsComment(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("//", StringComparison.Ordinal)
            || t.StartsWith("*", StringComparison.Ordinal)
            || t.StartsWith("<!--", StringComparison.Ordinal);
    }
}
