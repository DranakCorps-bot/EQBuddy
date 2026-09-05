using System.Reflection;
using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE EVOLVED SHELL'S TERMINOLOGY BAN, AS A BUILD STEP.**
///
/// §4 of `docs/BEVEL-v2-staging-critique.md` (Helm-signed 2026-09-04, amended 2026-09-05)
/// lists eight kinds of word that are OURS and must never reach a player: card, breakout,
/// theme, overlay section / mini-stat, mini pill, cog menu, "widget" as the product's name,
/// and raw type names. Bevel asked for the guard twice and gave the reason both times —
/// *"a terminology rule with no guard is a rule that lasts one PR"* — and its §6 ask 6 named
/// the shape (`GameCommandsTests`) and the open question with it: **are the player-facing
/// strings reachable from one place, and if not, that is itself the finding.**
///
/// **The finding, answered on this tip: NO, and by design.** The shell's words arrive from
/// three different places and no single scan can see all three, which is why this file has
/// three tiers rather than one clever regex:
///
///  1. **What a test can simply READ** — <see cref="ShellPages"/>' rail labels and room
///     descriptions, the five Core surfaces' tab labels, and the whole-room empties in
///     <see cref="ShellRoomEmpty"/> / <see cref="HomeReadout"/> / <see cref="LivePresentation"/>.
///     This is the strongest tier: it asserts the VALUES the shell renders, not the source
///     that produced them, so it cannot be fooled by how a string is spelled or assembled.
///     Its UI.Shared half is reflected over rather than listed, so a const added tomorrow is
///     covered tomorrow (trap 30 — a hand-maintained list stops covering the set the day the
///     set grows).
///  2. **What only the SOURCE has** — the inline literals in the WPF room files: tooltips,
///     button captions, headings built in code. The WPF layer has no unit tests
///     (docs/TestPlan.md §5), so a sentence left inline in a room is one nothing else can
///     check, and a curated file list with a reason per row is the same shape
///     `GameCommandsTests.SurfacesNeedingACommand` and `DeadSettingTests.Known` already use.
///  3. **The ban list itself** — pinned to the signed table, both directions, so an amended
///     §4 fails the build instead of leaving the guard quietly describing an older rule.
///     Trap 52 is what a guard costs when nobody re-derives the premise underneath it.
///
/// **What this deliberately does NOT cover, so nobody reads it as wider than it is.** It is
/// the SHELL scanner Fable's I-16 asked for, which is why it is not called
/// `BannedVocabularyTests`: the ban's own sentence also covers the HUD, Settings copy,
/// toasts and What's-new player text. The v1 widget, `OptionsWindow` and every shipped
/// `WhatsNew.json` entry are outside it — the shipped entries are immutable by rule
/// (`whatsnew-guard.ps1`) and the v1 surfaces are the debt the shell exists to retire, so a
/// scan over them would be red on arrival and switched off within a week (trap 54: a guard
/// that cries wolf gets switched off). Widening this to a surface is the deliberate act of
/// adding its row below, and the room it belongs to has to be clean first.
/// </summary>
public class ShellTerminologyTests
{
    /// <summary>
    /// One row of the signed §4 table. <paramref name="DocRow"/> is the table's left cell
    /// VERBATIM and is what <see cref="BanListIsExactlyTheSignedTable"/> pins, so the list
    /// below cannot drift from the ruling it enforces. <paramref name="Sample"/> is a
    /// sentence the row must catch — the negative control that stops a row going vacuous
    /// (trap 39: a guard written because of a real bug is the one nobody re-checks).
    /// </summary>
    public sealed record BanRow(string DocRow, string Pattern, string SayInstead, string Sample);

    /// <summary>The ban, in the signed table's own order. Editing this list without editing
    /// the doc fails; editing the doc without editing this list fails too.</summary>
    public static readonly BanRow[] Ban =
    [
        new("card / card key / launcher card", @"\bcards?\b",
            "the thing the player is looking at, by its job name",
            "Open the loot card for the rest."),
        new("breakout", @"\bbreakouts?\b",
            "window, or nothing — if it still exists it is a Live panel or a HUD chip",
            "Double-click to open its breakout."),
        new("theme / theme body", @"\bthemes?\b",
            "the room: Live, Progress, Gear, Quests, World",
            "The Progress theme has four tabs."),
        new("overlay section / mini-stat", @"\boverlay\s+sections?\b|\bmini[-\s]?stats?\b",
            "HUD / the number",
            "Pick which mini-stats the pill shows."),
        new("mini pill", @"\bmini[-\s]?pills?\b",
            "the HUD, or the chip by its job — the DPS chip, the mez chip",
            "Double-click a mini pill chip to open its window."),
        new("cog menu / Cards & windows (as a *finder*)", @"\bcog\s+menu\b|\bcards?\s*&\s*windows\b",
            "Settings, or the nav item",
            "Find it again in the cog menu."),
        new("widget (as the name of the product)", @"\bwidgets?\b",
            "EQBuddy, or the HUD",
            "Drag the widget where you want it."),
        new("IWidgetCard, AbsorbedTitles, SectionScroll, dump of internals",
            @"\bIWidgetCard\b|\bAbsorbedTitles\b|\bSectionScroll\b",
            "never",
            "SectionScroll is off for this room."),
    ];

    // ---------------------------------------------------------------------------------
    // Tier 3 — the list is the ruling, not a copy of it
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The eight rows above ARE the eight rows of §4, in order, verbatim. Both directions
    /// on purpose: a row deleted from this file and a row added to the doc are the same
    /// defect — a guard that has stopped covering the rule it cites — and only one of them
    /// is visible in a diff of this file.
    /// </summary>
    [Fact]
    public void BanListIsExactlyTheSignedTable()
    {
        var rows = SignedTableRows();
        Assert.Equal(Ban.Select(b => b.DocRow).ToArray(), rows);
    }

    /// <summary>The left column of §4's table, read out of the signed critique.</summary>
    private static string[] SignedTableRows()
    {
        var doc = File.ReadAllLines(
            Path.Combine(Repo, "docs", "BEVEL-v2-staging-critique.md"));
        var heading = Array.FindIndex(doc, l => l.StartsWith("### Terminology ban", StringComparison.Ordinal));
        Assert.True(heading >= 0,
            "docs/BEVEL-v2-staging-critique.md no longer has a '### Terminology ban' section. "
            + "That doc is the signed ruling this whole file enforces — if the ban has moved, "
            + "point this test at where it moved to rather than deleting the pin.");

        var separator = Array.FindIndex(doc, heading, l => l.StartsWith("|---", StringComparison.Ordinal));
        Assert.True(separator > heading, "§4's ban table has lost its header separator.");

        var rows = new List<string>();
        for (var i = separator + 1; i < doc.Length && doc[i].StartsWith("|", StringComparison.Ordinal); i++)
            rows.Add(doc[i].Split('|')[1].Trim());
        return [.. rows];
    }

    // ---------------------------------------------------------------------------------
    // Tier 1 — the words the shell actually shows, read at runtime
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Every rail label and every room description, for every member of
    /// <see cref="ShellPage"/> — not just the landed ones. A room's words are written in the
    /// PR that lands the room, and a room that has not landed yet is exactly the one nobody
    /// is looking at.
    /// </summary>
    [Fact]
    public void EveryRailLabelAndRoomDescriptionIsPlayerVocabulary()
    {
        foreach (var page in Enum.GetValues<ShellPage>())
        {
            AssertClean(ShellPages.Label(page), $"ShellPages.Label({page})");
            AssertClean(ShellPages.Describe(page), $"ShellPages.Describe({page})");
        }
    }

    /// <summary>
    /// Every tab label the shell puts in a room's strip, through the one definition the rail
    /// and the rooms share. The labels are Core's (<c>LiveSurface</c>, <c>ProgressSurface</c>,
    /// <c>LootSurface</c>, <c>QuestSurface</c>, <c>WorldSurface</c>), so this is the tier
    /// that would catch a v1 word arriving in the shell through a surface it did not write.
    /// </summary>
    [Fact]
    public void EveryRoomTabLabelIsPlayerVocabulary()
    {
        foreach (var page in Enum.GetValues<ShellPage>())
            foreach (var (label, key) in ShellPages.Rooms(page))
                AssertClean(label, $"ShellPages.Rooms({page}) tab '{key}'");
    }

    /// <summary>
    /// **The empties are the highest-value tier and the likeliest to slip.** An empty state
    /// is the only state a new player sees, it is written in prose rather than picked from a
    /// list, and it is where an author reaches for the internal name of the thing that is
    /// missing. All six rooms' whole-room empties, plus Home's readiness block, which is the
    /// most prose the shell carries in one place.
    /// </summary>
    [Fact]
    public void EveryWholeRoomEmptyAndReadinessSentenceIsPlayerVocabulary()
    {
        foreach (var m in new[]
                 {
                     ShellRoomEmpty.Progress, ShellRoomEmpty.Gear,
                     ShellRoomEmpty.World, ShellRoomEmpty.Quests,
                 })
        {
            AssertClean(m.Heading, "ShellRoomEmpty heading");
            AssertClean(m.Explanation, "ShellRoomEmpty explanation");
        }

        var readiness = HomeReadout.Readiness(("erollisi", "Dranak"), _ => null);
        Assert.NotEmpty(readiness);
        foreach (var row in readiness)
        {
            AssertClean(row.Name, "HomeReadout.Readiness name");
            AssertClean(row.Feeds, "HomeReadout.Readiness feeds");
            AssertClean(HomeReadout.ReadinessAnswer(row), "HomeReadout.ReadinessAnswer");
        }
        AssertClean(HomeReadout.ReadinessHeadline(readiness), "HomeReadout.ReadinessHeadline");

        foreach (var link in HomeReadout.Links())
        {
            AssertClean(link.Label, "HomeReadout.Links label");
            AssertClean(link.Detail, "HomeReadout.Links detail");
        }
    }

    /// <summary>
    /// The shell's word modules, reflected rather than listed: every public string constant
    /// on <see cref="HomeReadout"/>, <see cref="LivePresentation"/>, <see cref="ShellRoomEmpty"/>
    /// and <see cref="ShellPages"/>. A const added tomorrow is covered tomorrow, which is the
    /// one property a hand-maintained list can never have (trap 30).
    /// </summary>
    [Fact]
    public void EveryShellStringConstantIsPlayerVocabulary()
    {
        Type[] wordModules =
            [typeof(HomeReadout), typeof(LivePresentation), typeof(ShellRoomEmpty), typeof(ShellPages)];

        var seen = 0;
        foreach (var type in wordModules)
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.GetValue(null) is string s) { AssertClean(s, $"{type.Name}.{f.Name}"); seen++; }
                else if (f.GetValue(null) is RoomEmptyMessage m)
                {
                    AssertClean(m.Heading, $"{type.Name}.{f.Name}.Heading");
                    AssertClean(m.Explanation, $"{type.Name}.{f.Name}.Explanation");
                    seen++;
                }
            }

        // The negative that keeps the reflection honest: a rename that empties the sweep
        // would otherwise pass in silence, which is exactly how a guard reads as coverage
        // while seeing nothing (trap 34).
        Assert.True(seen >= 6,
            $"Only {seen} shell string constants were reached — the reflection above has "
            + "stopped finding the shell's words. Check the module list, not the assertion.");
    }

    // ---------------------------------------------------------------------------------
    // Tier 2 — the inline literals only the source has
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The Evolved shell's own files: the six rooms, the window that hosts them, the rail
    /// row, and the two shared pieces that build a room's chrome. Every one of them can put
    /// a sentence on screen from a literal that no other test can reach.
    ///
    /// **A row is deliberate in both directions.** A file listed here that no longer exists
    /// fails loudly rather than silently scanning nothing — trap 53 is six dark days bought
    /// by a stale name in a harness — and a new shell file with player text and no row here
    /// is unguarded, which is the state this file exists to end.
    ///
    /// **What is NOT here and why:** `ShellDumpFacts.cs` (diagnostics, ours by design — the
    /// `EQBUDDY_EXPAND` dump is not a surface), and the lifted v1 views the rooms HOST
    /// (`MapView`, `QuestsView`, `GearCardView`, …). Those are shared with the widget, so
    /// their copy is v1's debt rather than the shell's, and a lane that cannot edit product
    /// `src/` cannot fix what it would report. They join the list as the cards retire.
    /// </summary>
    public static readonly (string File, string Why)[] ShellStringSources =
    [
        ("EQBuddy/ShellWindow.xaml", "the shell's chrome and window title"),
        ("EQBuddy/ShellWindow.xaml.cs", "nav, room switching, the title that carries the room"),
        ("EQBuddy/ShellHost.cs", "what the shell hands a room"),
        ("EQBuddy/RailRow.cs", "one nav row — its label and the tooltip the collapsed rail leans on"),
        ("EQBuddy/RoomEmptyState.cs", "the room-level empty wrapper: heading, explanation, action"),
        ("EQBuddy/ShellRoomIdentity.cs", "who the shell says it is following"),
        ("EQBuddy/IShellRoom.cs", "the room contract every room's chrome is built against"),
        ("EQBuddy/HomeRoom.cs", "identity, readiness and the ⧉ tooltips — the most prose in the shell"),
        ("EQBuddy/LiveRoom.cs", "eight tabs of session surfaces"),
        ("EQBuddy/LiveSessionPanes.cs", "the Pace and Encounters panes Live brought from History"),
        ("EQBuddy/ProgressRoom.cs", "experience, wealth, faction, raids"),
        ("EQBuddy/GearRoom.cs", "bags, wishlist, loot"),
        ("EQBuddy/QuestsRoom.cs", "the tracker, Epic and Sky"),
        ("EQBuddy/WorldRoom.cs", "map, camps, path, travels, drops"),
        ("EQBuddy.UI.Shared/ShellPages.cs", "the rail's labels, descriptions and addresses"),
        ("EQBuddy.UI.Shared/ShellLayout.cs", "the two degrade axes — any text they name"),
        ("EQBuddy.UI.Shared/ShellRoomEmpty.cs", "the four data rooms' whole-room empties"),
        ("EQBuddy.UI.Shared/HomeReadout.cs", "identity, readiness and deep-link sentences"),
        ("EQBuddy.UI.Shared/LivePresentation.cs", "Live's words, badge and captions"),
    ];

    public static TheoryData<string, string> SourceRows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (file, why) in ShellStringSources) rows.Add(file, why);
        return rows;
    }

    [Theory]
    [MemberData(nameof(SourceRows))]
    public void NoShellSourceCarriesBannedVocabularyInAStringItCanShow(string file, string why)
    {
        var path = Path.Combine(Repo, "src", file.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path),
            $"{file} is on ShellStringSources ({why}) and does not exist. If the shell file "
            + "moved, move its row; if it is gone, delete the row and say so in the commit. A "
            + "list that scans a file that is not there is a guard reporting green on nothing.");

        var text = File.ReadAllText(path);
        var literals = file.EndsWith(".xaml", StringComparison.Ordinal)
            ? VisibleXamlValues(text)
            : PlayerVisibleLiterals(text);

        foreach (var literal in literals)
        {
            if (Exempt.Any(e => e.File == file && e.Literal == literal)) continue;
            AssertClean(literal, $"{file} ({why})");
        }
    }

    /// <summary>
    /// The escape hatch, and it is deliberately narrow: a literal in a shell file that a
    /// player never reads, which the two heuristics below cannot recognise. Empty today.
    ///
    /// **An exemption nobody can see is a blind spot rather than an exemption** — the reason
    /// `SurfaceOwnershipTests` writes its two down with the PR that removes each. Adding a
    /// row means saying, in the row, why the string never reaches a player; the assertion
    /// underneath means a row that stops matching anything fails instead of rotting.
    /// </summary>
    public static readonly (string File, string Literal, string Why)[] Exempt = [];

    [Fact]
    public void EveryExemptionStillMatchesSomethingReal()
    {
        foreach (var (file, literal, why) in Exempt)
        {
            var path = Path.Combine(Repo, "src", file.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"{file} is exempt ({why}) and does not exist.");
            Assert.Contains(literal, File.ReadAllText(path), StringComparison.Ordinal);
        }
    }

    // ---------------------------------------------------------------------------------
    // The scanner cannot go vacuous
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Every row catches its own sample. Trap 39's lesson is that the most trustworthy-
    /// looking guard is the one nobody re-checks: an assertion that can never fail reads as
    /// coverage forever. Each row here is proved to fail on a sentence a real PR could write.
    /// </summary>
    [Fact]
    public void EveryBanRowCatchesItsOwnSample()
    {
        foreach (var row in Ban)
        {
            var source = $$"""
                namespace X; internal class Y { void M() { var s = "{{row.Sample}}"; } }
                """;
            var literals = PlayerVisibleLiterals(source);
            Assert.Contains(row.Sample, literals);
            Assert.True(Offenders(row.Sample).Contains(row.DocRow),
                $"The ban row '{row.DocRow}' does not catch its own sample: \"{row.Sample}\". "
                + "A row that cannot fail is not a guard.");
        }
    }

    /// <summary>
    /// The other half: what the scanner must NOT flag. Our own prose is full of these words
    /// — this very file is — and a scan that read comments would be red on arrival and gone
    /// within a week (trap 54: a guard that cries wolf gets switched off). Same for the
    /// `EQBUDDY_EXPAND` dump keys, which are diagnostics rather than a surface.
    /// </summary>
    [Fact]
    public void TheScannerReadsStringsAndNotCommentsOrDumpFacts()
    {
        const string source = """
            namespace X;
            /// <summary>A room with nothing in it is the same defect as a blank card.</summary>
            internal class Y
            {
                // the Progress theme's breakout is gone
                /* IWidgetCard went with the widget */
                void M()
                {
                    var facts = $"shellGearCards={1} shellLiveBreakouts={2} ";
                    var url = "https://eqlwiki.com/wiki/Plane_of_Sky";
                    var shown = "Nothing to chart yet";
                }
            }
            """;

        var literals = PlayerVisibleLiterals(source);
        Assert.Contains("Nothing to chart yet", literals);
        // The `//` inside a URL is not a comment: a line filter would have swallowed the
        // rest of that line, and with it any sentence sitting after it.
        Assert.Contains("https://eqlwiki.com/wiki/Plane_of_Sky", literals);
        Assert.DoesNotContain(literals, l => l.StartsWith("shellGearCards=", StringComparison.Ordinal));
        Assert.All(literals, l => Assert.Empty(Offenders(l)));
    }

    // ---------------------------------------------------------------------------------
    // Machinery
    // ---------------------------------------------------------------------------------

    private static void AssertClean(string text, string where)
    {
        var hits = Offenders(text);
        if (hits.Count == 0) return;

        var say = string.Join("; ", hits.Select(h =>
            $"'{h}' → {Ban.First(b => b.DocRow == h).SayInstead}"));
        Assert.Fail(
            $"{where} says \"{text}\" — that is implementation vocabulary on screen. {say}. "
            + "The ban is §4 of docs/BEVEL-v2-staging-critique.md, Helm-signed 2026-09-04 and "
            + "amended 2026-09-05; "
            + "these words are ours, not the player's. If the word genuinely is not player-"
            + "visible, add a row to ShellTerminologyTests.Exempt saying why.");
    }

    /// <summary>Which ban rows a piece of text trips, by their doc-row names.</summary>
    private static List<string> Offenders(string text) =>
        [.. Ban.Where(b => Regex.IsMatch(text, b.Pattern, RegexOptions.IgnoreCase))
              .Select(b => b.DocRow)];

    /// <summary>
    /// Every string literal in a C# file that could reach a player, with the two kinds that
    /// could not removed:
    ///
    ///  * **Comments.** Doc comments are where this codebase argues about `card`, `breakout`
    ///    and `theme` by name, and it must go on being able to. A character walk rather than
    ///    a line filter, because `//` inside a URL inside a string is not a comment.
    ///  * **`EQBUDDY_EXPAND` dump facts.** A literal shaped `key=` is a diagnostic the E2E
    ///    suite reads, not a sentence: a player-visible string never opens with an
    ///    identifier welded to an equals sign, and the rooms carry dozens of them.
    ///
    /// Interpolation holes are stripped too — `$"{cardCount} left"` is an identifier the
    /// player never sees inside a sentence they do.
    /// </summary>
    private static List<string> PlayerVisibleLiterals(string source)
    {
        var found = new List<string>();
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
            }
            else if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i += 2;
            }
            else if (c == '\'')
            {
                i++;
                while (i < source.Length && source[i] != '\'')
                    i += source[i] == '\\' ? 2 : 1;
                i++;
            }
            else if (c == '"')
            {
                var verbatim = i > 0 && source[i - 1] == '@';
                i++;
                var start = i;
                while (i < source.Length)
                {
                    if (!verbatim && source[i] == '\\') { i += 2; continue; }
                    if (source[i] == '"') break;
                    if (!verbatim && source[i] == '\n') break;
                    i++;
                }
                var literal = source[start..Math.Min(i, source.Length)];
                i++;
                if (!IsDumpFact(literal)) found.Add(StripHoles(literal));
            }
            else i++;
        }
        return found;
    }

    /// <summary>A literal that opens `identifier=` is an <c>EQBUDDY_EXPAND</c> fact.</summary>
    private static bool IsDumpFact(string literal) =>
        Regex.IsMatch(literal, @"^[A-Za-z][A-Za-z0-9]*=");

    private static string StripHoles(string literal) =>
        Regex.Replace(literal, @"\{[^{}]*\}", " ");

    /// <summary>
    /// XAML is scanned narrowly and on purpose: only the attributes that put words on
    /// screen. Every other attribute value is a type name, a resource key or a number —
    /// `Style="{StaticResource CardPanel}"` is our architecture, correctly named, and
    /// flagging it would be the false positive that gets this file deleted. Bindings are
    /// skipped for the same reason; what they resolve to is tier 1's job.
    /// </summary>
    private static List<string> VisibleXamlValues(string xaml)
    {
        var body = Regex.Replace(xaml, "<!--.*?-->", " ", RegexOptions.Singleline);
        return
        [
            .. Regex.Matches(body, @"\b(?:Text|Content|ToolTip|Header|Title)\s*=\s*""([^""]*)""")
                .Select(m => m.Groups[1].Value)
                .Where(v => !v.StartsWith('{')),
        ];
    }

    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
