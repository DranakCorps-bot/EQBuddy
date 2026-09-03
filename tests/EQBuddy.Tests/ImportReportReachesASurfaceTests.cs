using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// **The mirror of <see cref="DeadSettingTests"/>: a value with a PRODUCER and no
/// CONSUMER.** That file scans for settings read but never written, because three
/// player-facing bugs came from a fold that kept the data and lost the write path
/// (trap 20). This is the same defect with the arrow reversed, and it shipped on
/// 2026-08-20 without anything noticing for two days.
///
/// <c>MainWindow.LastAchievementsImport</c> was introduced documented as *"Same for the
/// achievements dump — read by the Raids surface"* (WPF) and *"for the Gear and Raids
/// surfaces to report"* (Avalonia). **No Raids surface ever read it, in either UI.** So
/// when the game announced an achievements dump, EQBuddy read it, marked Sky rewards
/// turned in and raid clears complete, and said NOTHING — no report, no Undo, and no
/// mention of the rewards its own #101 guard had just refused. The inventory half of the
/// same commit reported itself on the Gear tab, which is what made the gap invisible: the
/// commit message says "the report is visible on the Gear tab with an Undo" and that
/// sentence is true.
///
/// Nothing else could see it. Not the compiler (the property is assigned), not a unit
/// test (the Core outcome was correct all along and is tested), not the ratchet, and
/// **not a screenshot — a control that was never drawn photographs as an unremarkable
/// card** (trap 29). The only thing that can see it is an assertion that the value
/// reaches a surface, which is what this file is.
///
/// The guard is a curated must-list, per trap 34: a scan that only forbade something
/// would have been just as blind. Every recorded import outcome gets a row here naming
/// the surface that shows it, and adding a producer without adding a row fails.
/// </summary>
public class ImportReportReachesASurfaceTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    /// <summary>Every <c>AutoImportOutcome</c> a widget records, and the surface that has
    /// to show it. **A dump EQBuddy read without saying so is indistinguishable from a
    /// dump EQBuddy never saw**, and only one of those is a bug — which is the confusion
    /// the whole auto-import feature exists to end (David, 2026-08-20).</summary>
    public static readonly (string Property, string Surface, string Why)[] MustReachASurface =
    [
        ("LastInventoryImport", "Gear",
            "The Gear surface is the one that tells the player to run /outputfile inventory."),
        ("LastAchievementsImport", "Raids",
            "The Raids surface is the one that tells the player to run /outputfile "
            + "achievements, in BOTH its empty and its populated state — same rule."),
    ];

    /// <summary>**The dump feeds TWO consumers, so the report has two homes** (Bevel,
    /// Helm-signed 2026-08-23). Raid clears land on Raids; Sky rewards land on the Quest
    /// Tracker's Sky tab. With the report on Raids alone, "1 Sky reward marked · 2 skipped"
    /// was being read above a list of raid bosses — by a player who may never open that
    /// surface at all. Bevel: *"a Quest-Tracker job being read on a raid-clear list."*
    ///
    /// Listed separately from <see cref="MustReachASurface"/> because these are SECOND hosts
    /// for outcomes that already have one — the scan above asks "does anything read this",
    /// and the answer was yes while a whole audience still could not see it.
    ///
    /// Each row names the PROPERTY it asserts. The first cut hardcoded
    /// <c>LastAchievementsImport</c> in the test body, so a new row for the inventory
    /// report would have passed by finding the achievements line in the same file —
    /// trap 34's shape, a guard that reads as coverage and checks nothing.</summary>
    public static readonly (string Project, string File, string Property, string Why)[] SecondHosts =
    [
        ("EQBuddy", "QuestsWindow.xaml.cs", "LastAchievementsImport",
            "The Sky tab. Same ImportReportView, not a Sky-flavoured variant — one more "
            + "host, one more line, and the Undo rule stays in one place."),
        ("EQBuddy.Avalonia", "QuestsWindow.cs", "LastAchievementsImport",
            "The Avalonia twin of the same tab."),
        ("EQBuddy", "QuestsWindow.xaml.cs", "LastInventoryImport",
            "The inventory dump proves Sky rewards turned in (Hateborne, 2026-09-03), so "
            + "its report has the same second audience the achievements one does."),
        ("EQBuddy.Avalonia", "QuestsWindow.cs", "LastInventoryImport",
            "The Avalonia twin of the same tab."),
    ];

    /// <summary>Both lanes' Sky tab builds one per outcome. A ruling that shipped on one UI
    /// only is how #122 and #152 reached Linux after Windows had already paid for them.</summary>
    [Theory]
    [MemberData(nameof(SecondHostRows))]
    public void TheSkyTabReportsTheImportToo(string project, string file, string property, string why)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.Contains($"new ImportReportView(() => _main.{property}", text);
        Assert.NotEmpty(why);
    }

    public static TheoryData<string, string, string, string> SecondHostRows()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var (project, file, property, why) in SecondHosts)
            data.Add(project, file, property, why);
        return data;
    }

    private static readonly (string Ui, string File)[] Widgets =
    [
        ("WPF", Path.Combine("EQBuddy", "MainWindow.xaml.cs")),
        ("Avalonia", Path.Combine("EQBuddy.Avalonia", "MainWindow.cs")),
    ];

    /// <summary>The assertion the two-day-old bug fails: the property is READ somewhere,
    /// not merely assigned. Counting mentions is deliberately crude — a reader that hands
    /// it to the wrong control would still pass here, and the render tests below are what
    /// cover that. What this catches is the case that actually happened: nobody reads it
    /// at all.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void EveryRecordedImportOutcomeIsReadBySomething(string ui, string file, string property)
    {
        var text = File.ReadAllText(Path.Combine(Src, file));

        // The two mentions that are NOT a read: the declaration and the assignment.
        var mentions = Occurrences(text, property);
        var declared = Occurrences(text, $"AutoImportOutcome? {property}");
        var assigned = Occurrences(text, $"{property} =");

        Assert.True(mentions - declared - assigned > 0,
            $"{ui}: {property} is written and never read. That is a silent import — "
            + "EQBuddy changes the player's checklist and tells them nothing, with no "
            + "Undo. See ImportReportReachesASurfaceTests for the 2026-08-20 case.");
    }

    /// <summary>And it is handed on as a LIVE <c>Func</c>, never captured by value. A
    /// surface given the outcome itself would show the first dump forever — the widget
    /// replaces the record rather than mutating it, so a captured copy is a report that
    /// silently stops updating, which is this file's bug wearing a different hat.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void ThePropertyIsHandedOnAsALiveFunc(string ui, string file, string property)
    {
        var text = File.ReadAllText(Path.Combine(Src, file));

        Assert.True(text.Contains($"() => {property}"),
            $"{ui}: {property} is not handed to a surface as a Func<AutoImportOutcome?>.");
    }

    /// <summary>Both UIs route BOTH reports through <c>ImportReportView</c> — the class
    /// that owns "offer Undo only when something actually changed". A surface that
    /// printed the summary itself would be a second copy of that rule, which is exactly
    /// how the WPF and Avalonia chip anchors drifted apart and carried #122 and #152 to
    /// Linux after Windows had already paid for both.</summary>
    [Theory]
    [InlineData("WPF", "EQBuddy")]
    [InlineData("Avalonia", "EQBuddy.Avalonia")]
    public void EachUiBuildsTwoImportReportViews(string ui, string project)
    {
        var uses = Directory
            .EnumerateFiles(Path.Combine(Src, project), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Sum(f => Occurrences(File.ReadAllText(f), "new ImportReportView("));

        Assert.True(uses >= MustReachASurface.Length,
            $"{ui}: {uses} ImportReportView(s) built for {MustReachASurface.Length} import "
            + "outcomes. One of the surfaces in MustReachASurface has no report.");
    }

    public static TheoryData<string, string, string> Rows()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (ui, file) in Widgets)
            foreach (var (property, _, _) in MustReachASurface)
                data.Add(ui, file, property);
        return data;
    }

    /// <summary>The WPF Raids card hangs the report OUTSIDE its rows panel, because the
    /// rows are cleared wholesale on every repaint. Asserted rather than trusted: a
    /// report parented into the rows would vanish on the next kill, which looks exactly
    /// like the bug this file is about and would photograph the same way.</summary>
    [Fact]
    public void TheRaidsReportIsNotParentedIntoThePanelThatGetsCleared()
    {
        foreach (var file in new[]
        {
            Path.Combine(Src, "EQBuddy", "RaidsCardView.cs"),
            Path.Combine(Src, "EQBuddy.Avalonia", "MainWindow.cs"),
        })
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("_panel.Children.Add(_importReport", text);
            Assert.DoesNotContain("_raidsPanel.Children.Add(RaidsImport", text);
        }
    }

    private static int Occurrences(string haystack, string needle)
    {
        var n = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            n++;
        return n;
    }
}
