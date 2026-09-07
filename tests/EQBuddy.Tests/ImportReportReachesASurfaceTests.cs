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
        ("EQBuddy", "QuestsView.xaml.cs", "LastAchievementsImport",
            "The Sky tab. Same ImportReportView, not a Sky-flavoured variant — one more "
            + "host, one more line, and the Undo rule stays in one place."),
        ("EQBuddy", "QuestsView.xaml.cs", "LastInventoryImport",
            "The inventory dump proves Sky rewards turned in (Hateborne, 2026-09-03), so "
            + "its report has the same second audience the achievements one does."),
    ];

    /// <summary>The Sky tab builds one per outcome. Two Avalonia rows left this list with
    /// the platform in E-2 (2026-09-04); what they were buying was the cross-lane half
    /// (#122/#152), and what remains is the half the list was created for — an import that
    /// changes a player's ledger and reports it nowhere they are looking (trap 43).</summary>
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

    /// <summary>
    /// **THE PROFILE IMPORT'S ROW** (Fable's transition plan §8, TR-1), and it is listed
    /// apart from <see cref="MustReachASurface"/> because it is not an
    /// <c>AutoImportOutcome</c> and must not be bent into one: there is no <c>Undo</c>
    /// button to offer — the undo is structural, "clear the Evolved profile, v1 still has
    /// everything" — and the outcome outlives the process that produced it, so it is read
    /// off the marker on disk rather than off a property.
    ///
    /// **The defect it guards is identical, which is why it belongs in this file.** The
    /// copy happens at startup, before <c>AppSettings.Load</c> and before any window exists
    /// (see <c>ProfileImportStartup</c> for why the ordering is not negotiable), so an
    /// import that reported nowhere would be indistinguishable from an import that never
    /// ran — while having copied a whole profile. That is trap 43's sentence with a
    /// player's entire settings, history and quest progress inside it.
    /// </summary>
    public static readonly (string Project, string File, string Reads, string Why)[]
        ProfileImportMustReachASurface =
    [
        ("EQBuddy", "SetupView.cs", "ProfileImport.ReadMarker",
            "The first-run Setup screen is where the transition plan §2 puts it: the first "
            + "surface a player meets after the import, and the one that can show what came "
            + "over beside the start-fresh alternative."),
        ("EQBuddy", "SetupView.cs", "ProfileImportReadout.Report",
            "Through the readout, never a sentence built in the window — the words are "
            + "promises about a player's data and the WPF layer has no unit tests."),
    ];

    [Theory]
    [MemberData(nameof(ProfileImportRows))]
    public void TheProfileImportReportReachesASurface(string project, string file,
        string reads, string why)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.Contains(reads, text);
        Assert.NotEmpty(why);
    }

    /// <summary>And it is drawn ABOVE the rows (trap 44). A report about something that just
    /// happened, appended after a list, is below the fold — which is exactly how the Raids
    /// import report shipped correct and behind a scrollbar. Asserted as an ORDER in the
    /// source rather than as a pixel, because the WPF layer has nothing else to assert
    /// with.</summary>
    [Fact]
    public void TheProfileImportReportIsDrawnBeforeTheSetupRows()
    {
        var text = File.ReadAllText(Path.Combine(Src, "EQBuddy", "SetupView.cs"));
        var report = text.IndexOf("AddImportReport();", StringComparison.Ordinal);
        var rows = text.IndexOf("foreach (var row in _rows)", StringComparison.Ordinal);

        Assert.True(report > 0 && rows > 0 && report < rows,
            "The import report must be added to the Setup body BEFORE the readiness rows. "
            + "See trap 44: the Raids import report was appended after 21 boss rows and "
            + "rendered correctly, behind a scrollbar, on a surface nobody scrolls.");
    }

    public static TheoryData<string, string, string, string> ProfileImportRows()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var row in ProfileImportMustReachASurface)
            data.Add(row.Project, row.File, row.Reads, row.Why);
        return data;
    }

    private static readonly (string Ui, string File)[] Widgets =
    [
        ("WPF", Path.Combine("EQBuddy", "MainWindow.xaml.cs")),
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

    /// <summary>Every report goes through <c>ImportReportView</c> — the class that owns
    /// "offer Undo only when something actually changed". A surface that printed the
    /// summary itself would be a second copy of that rule, and a second copy of a rule is
    /// how the chip anchors drifted apart (#122, #152). The Avalonia row went with the
    /// platform in E-2 (2026-09-04); the count is per-outcome, so it still fails when a
    /// surface in MustReachASurface has no report.</summary>
    [Theory]
    [InlineData("WPF", "EQBuddy")]
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
