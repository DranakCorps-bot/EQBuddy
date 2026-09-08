using System.Xml.Linq;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **THE ≤4 LOCK ON THE MINIMIZED MENU, READ OUT OF THE XAML THAT DECLARES IT.**
///
/// Bevel's cog/Options IA faces §B (`docs/BEVEL-cog-options-ia-faces.md`), Helm-signed
/// 2026-09-08 ~2:13 PM CT with the owner's ~2:15 PM CT Guide amendment. The widget's
/// context menu is ONE menu hanging off the shared root border, so before this cut every
/// row on it was also on the minimized bar — eleven rows over a fullscreen game, of which
/// the four that matter mid-session are doors.
///
/// **The failure this refuses is the quiet one.** A row added to `MainWindow.xaml` without
/// `Tag="expanded"` is an extra item on the minimized menu that compiles, runs, photographs
/// as an ordinary menu (trap 29) and breaks no other test — the exact shape of the "a
/// guard that forbids the wrong thing cannot see a missing thing" pairing trap 34 names, so
/// this file asserts BOTH directions: the four that must be there, and that nothing else is.
///
/// **It parses the XAML rather than launching the app** for the reason `RetiredCardsTests`
/// already reads the same file: the WPF layer has no unit test project (docs/TestPlan.md
/// §5), the menu is declarative, and what can go wrong here is a declaration. That the
/// declaration is APPLIED at runtime is a different claim and a different guard — trap 42 —
/// and it is `ShellHostTests`' `menuRows` dump fact from a launched app.
/// </summary>
public sealed class WidgetMenuTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>The widget's context menu, as declared. The root border is the only element
    /// in the file carrying one, and the menu's direct children are the top-level rows.</summary>
    private static List<XElement> TopLevelItems()
    {
        var doc = XDocument.Load(Path.Combine(Repo, "src", "EQBuddy", "MainWindow.xaml"));
        var menu = doc.Descendants(Xaml + "ContextMenu").Single();
        return [.. menu.Elements()];
    }

    private static bool ExpandedOnly(XElement element) =>
        (string?)element.Attribute("Tag") == WidgetMenuPolicy.ExpandedOnlyTag;

    /// <summary>
    /// **The minimized menu is exactly the four doors, in the signed order.** Order is part
    /// of the assertion because §B lists them as a reading order — Options, World, Mobile,
    /// Guide — and a menu whose contents are right in a different sequence is a different
    /// screen to the person scanning it.
    /// </summary>
    [Fact]
    public void TheMinimizedMenuIsExactlyTheFourDoorsInOrder()
    {
        var mini = TopLevelItems()
            .Where(e => e.Name == Xaml + "MenuItem" && !ExpandedOnly(e))
            .Select(e => (string?)e.Attribute("Header") ?? "")
            .ToList();

        Assert.Equal(WidgetMenuPolicy.MiniRows, mini);
    }

    /// <summary>
    /// **The other direction — every row that is NOT one of the four carries the tag.** A
    /// row is hidden by `MainWindow.ApplyMenuMode`, which acts on the tag; an untagged row
    /// is one nothing hides. Asserted over rows AND separators, because a mini menu whose
    /// last row is followed by a rule looks broken in a way no count would catch.
    /// </summary>
    [Fact]
    public void EveryRowAndRuleThatIsNotOneOfTheFourIsTaggedExpandedOnly()
    {
        foreach (var element in TopLevelItems())
        {
            var header = (string?)element.Attribute("Header");
            if (header is not null && WidgetMenuPolicy.MiniRows.Contains(header)) continue;

            Assert.True(ExpandedOnly(element),
                $"the context-menu {element.Name.LocalName} " +
                $"{(header is null ? "(separator)" : $"\"{header}\"")} is not one of the four "
                + $"minimized rows and does not carry Tag=\"{WidgetMenuPolicy.ExpandedOnlyTag}\", "
                + "so it would show on the minimized bar — faces §B's ≤4 lock");
        }
    }

    /// <summary>
    /// **The rows the cut REMOVED are gone from the file, by their old spelling.** A rename
    /// that leaves the old row behind is two doors where the amendment asked for one, and
    /// it is invisible to the two assertions above the moment the leftover carries the tag.
    ///
    /// `Open EQBuddy…`, `EQBuddy window…` and `Open rooms…` are all named because the
    /// amendment cut all three by name — the middle one was a rename that was considered
    /// and explicitly refused, so a later reader reaching for it finds a failing test rather
    /// than a plausible-looking gap.
    /// </summary>
    [Fact]
    public void TheRowsTheAmendmentCutAreNotInTheMenuUnderAnySpelling()
    {
        var headers = TopLevelItems()
            .Select(e => (string?)e.Attribute("Header") ?? "")
            .ToList();

        foreach (var cut in new[] { "Open EQBuddy…", "EQBuddy window…", "Open rooms…", "Quests…" })
            Assert.DoesNotContain(cut, headers);
    }

    /// <summary>
    /// **The Guide row is the shell's door, and three files have to spell it the same.**
    /// The XAML row, <see cref="WidgetMenuPolicy.GuideRow"/> (which the dump fact and this
    /// suite are keyed off), and the "No longer on the widget" line that tells a player who
    /// lost the Quests card which row to choose. `RetiredCardsTests` checks the third
    /// against the file; this checks the first two against each other, so a rename cannot
    /// pass by editing the XAML and the retired list while leaving the policy behind.
    /// </summary>
    [Fact]
    public void TheGuideRowIsSpeltTheSameInTheXamlAndThePolicy()
    {
        Assert.Contains(WidgetMenuPolicy.GuideRow, WidgetMenuPolicy.MiniRows);
        Assert.Contains(WidgetMenuPolicy.GuideRow,
            TopLevelItems().Select(e => (string?)e.Attribute("Header") ?? ""));

        // And it opens the room the shell calls Guide — the WIRE key, which did not move
        // when the label did, because `page:room` is persisted.
        Assert.Equal("quests", WidgetMenuPolicy.GuideAddress);
        Assert.Equal("Guide", ShellPages.Label(ShellPage.Quests));
    }
}
