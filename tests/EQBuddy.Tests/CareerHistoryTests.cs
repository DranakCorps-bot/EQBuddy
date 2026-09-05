using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The CAREER half of `HistoryWindow`'s merge** — the Evolved Progress room's History
/// tab. Bevel's History pre-design §1/§4, Helm-signed 2026-09-05 ~10:10 AM CT.
///
/// Everything the tab decides lives in `UI.Shared` for the standing reason: the WPF layer
/// has no unit tests (docs/TestPlan.md §5), so a rule left inline in
/// <c>CareerHistoryView</c> is a rule nothing can check. These rows are what the sign
/// requires, each written so it fails if the requirement stops being met:
///
///  1. The browse reads the ROW and says so — a thin surface must not look like a thin
///     session (#234's lesson).
///  2. The running sitting is excluded, through the predicate Home and Live already merge
///     on rather than a second spelling of it.
///  3. The two ladders are the studio's arithmetic, not a second copy of it.
///  4. The tab is desktop-shell-only, and the badge behind it is a count and not a clock.
/// </summary>
public class CareerHistoryTests
{
    private static readonly DateTime Start = new(2026, 9, 5, 19, 0, 0);

    private static SessionRow Row(long id = 1, string zone = "Lower Guk",
        string endReason = "Manual", DateTime? start = null, int kills = 147,
        int deaths = 0, string note = "", string tags = "") =>
        new(id, "test", "Testchar", start ?? Start, null, 8040, 5760, endReason, zone,
            kills, 12.4, 51408, 23, deaths, 84.13, note, tags);

    // ---- 1. the browse says what it is -----------------------------------------

    /// <summary>The list row is when-and-where over the sitting's own numbers. Same FACTS
    /// as the studio's own row, in the shape a two-line native row wants — two lists
    /// describing one session differently is trap 4 with a UI on it.</summary>
    [Fact]
    public void ACareerRowLeadsWithWhenAndWhereAndThenTheNumbers()
    {
        var row = HistoryPresentation.BuildCareerRow(Row());

        Assert.Equal(1, row.Id);
        Assert.Equal("Sat Sep 5, 7:00 PM — Lower Guk", row.Title);
        Assert.Equal("2h 14m · 147 kills · 12.4% xp · 51p 4g 8c", row.Detail);
    }

    /// <summary>Deaths appear only when there were some — the one number whose absence is
    /// the good news, and the same refusal `LivePresentation.Facts` already makes.</summary>
    [Fact]
    public void ACleanSittingDoesNotCarryAZeroDeathCount()
    {
        Assert.DoesNotContain("death", HistoryPresentation.BuildCareerRow(Row()).Detail);
        Assert.Contains("2 deaths", HistoryPresentation.BuildCareerRow(Row(deaths: 2)).Detail);
        Assert.Contains("1 death", HistoryPresentation.BuildCareerRow(Row(deaths: 1)).Detail);
    }

    /// <summary>A crash-recovered sitting says so on its row. It is the one state where the
    /// numbers may be short through no fault of the player's, and the studio's own list has
    /// always marked it.</summary>
    [Fact]
    public void ARecoveredSittingIsMarkedOnItsRow()
    {
        Assert.Contains("recovered", HistoryPresentation.BuildCareerRow(
            Row(endReason: SessionRepository.RecoveredEndReason)).Detail);
    }

    /// <summary>A sitting with no zone gets no dash to nowhere. `BuildSessionRow` prints a
    /// literal "-" in that case because it is a fixed-width text list; a native row can
    /// simply not say it.</summary>
    [Fact]
    public void ASittingWithNoZoneNamesNoZone()
    {
        var row = HistoryPresentation.BuildCareerRow(Row(zone: ""));
        Assert.Equal("Sat Sep 5, 7:00 PM", row.Title);
    }

    [Fact]
    public void TheDetailPaneReadsEveryFieldTheRowCarries()
    {
        var detail = HistoryPresentation.BuildCareerDetail(Row(note: "good camp", tags: "guk"));

        Assert.Contains("Duration   2h 14m — active 96m", detail);
        Assert.Contains("Zone       Lower Guk", detail);
        Assert.Contains("Kills      147", detail);
        Assert.Contains("XP         12.4%", detail);
        Assert.Contains("Damage     84.1 dps", detail);
        Assert.Contains("Loot       23 items", detail);
        Assert.Contains("Money      51p 4g 8c", detail);
        Assert.Contains("Ended      Manual", detail);
        Assert.Contains("Note  good camp", detail);
        Assert.Contains("Tags  guk", detail);
    }

    /// <summary>Notes and tags are omitted when empty rather than printed as bare labels —
    /// an empty "Note" line reads as a note that failed to load (trap 17, in text).</summary>
    [Fact]
    public void AnUnannotatedSittingShowsNoNoteOrTagLabel()
    {
        var detail = HistoryPresentation.BuildCareerDetail(Row());
        Assert.DoesNotContain("Note", detail);
        Assert.DoesNotContain("Tags", detail);
    }

    /// <summary>
    /// **#234's lesson, applied before it can be reported.** This browse shows nine numbers
    /// where the studio shows a graph, a pull list and two breakdowns — and a trimmed
    /// surface that looks complete is the defect, not the trimming. So the tab SAYS which
    /// one it is and names the door to the rest, and that door is real: `HistoryWindow`
    /// keeps its context-menu entry this pass (Helm, 2026-09-05, item 5).
    /// </summary>
    [Fact]
    public void TheBrowseNamesTheStudioThatHoldsWhatItDoesNot()
    {
        var pointer = HistoryPresentation.StudioPointer;

        Assert.Contains("Session history", pointer);
        foreach (var job in new[] { "comparison", "notes", "export", "delete" })
            Assert.Contains(job, pointer, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The empty state is not "no data": a sitting is recorded when it ENDS, so the
    /// honest fact for a first-time player is that theirs has not finished — and Live is
    /// where it can be watched meanwhile.</summary>
    [Fact]
    public void TheEmptyStateExplainsWhenASittingIsRecordedAndWhereTheLiveOneIs()
    {
        Assert.NotEmpty(HistoryPresentation.CareerEmptyHeading);
        Assert.Contains("Live", HistoryPresentation.CareerEmptyExplanation);
        Assert.DoesNotContain("no data", HistoryPresentation.CareerEmptyExplanation,
            StringComparison.OrdinalIgnoreCase);
    }

    // ---- 2. the running sitting is not browsable -------------------------------

    /// <summary>
    /// **THE SPLIT ITSELF.** The archiver checkpoints the running sitting into the store, so
    /// it IS in `StoredSessions()` — and the picture behind it is up to five minutes old and
    /// never reloads (Bevel §2). A career browse that offered it would be offering the stale
    /// copy of a sitting whose live copy is one room away.
    ///
    /// The predicate is `SessionSummary.IsTheLiveSession`, the same one Home and Live merge
    /// on. Re-spelling `EndReason == ActiveEndReason` at a second call site would drift from
    /// its one-second start-time tolerance exactly where a race exposes it (trap 33) — which
    /// is what the second half of this test is about.
    /// </summary>
    [Fact]
    public void TheRunningSittingIsExcludedFromTheBrowse()
    {
        var active = Row(endReason: SessionRepository.ActiveEndReason);
        Assert.True(SessionSummary.IsTheLiveSession(active, liveStart: null));

        var ended = Row(endReason: "Manual", start: Start.AddHours(-4));
        Assert.False(SessionSummary.IsTheLiveSession(ended, liveStart: Start));
    }

    /// <summary>The tolerance half: a sitting finalised under a real end reason while the
    /// snapshot has not rolled yet is still the live one, and a browse using the end reason
    /// alone would have listed it for a second with a stale snapshot behind it.</summary>
    [Fact]
    public void AJustFinalisedSittingIsStillTheLiveOneUntilTheSnapshotRolls()
    {
        var justEnded = Row(endReason: "Manual", start: Start);
        Assert.True(SessionSummary.IsTheLiveSession(justEnded, liveStart: Start.AddMilliseconds(400)));
        Assert.False(SessionSummary.IsTheLiveSession(justEnded, liveStart: Start.AddHours(1)));
    }

    // ---- 3. the ladders are the studio's arithmetic ----------------------------

    /// <summary>Levels come from ding lines (exact times); AA totals from each session's
    /// last AA event. A sitting that saw no AA is skipped so the step chart HOLDS the
    /// previous value — a hold, not a lie, which is the studio's own rule and the reason
    /// `BuildStepGraph` is a staircase rather than a slope.</summary>
    [Fact]
    public void TheLaddersTakeDingsExactlyAndSkipSittingsWithNoAa()
    {
        var (dings, aa) = HistoryPresentation.CareerLadders(
        [
            new SessionRepository.ProgressPoint(Start, 4, [(Start.AddMinutes(10), 22)]),
            new SessionRepository.ProgressPoint(Start.AddDays(1), 0, []),
            new SessionRepository.ProgressPoint(Start.AddDays(2), 7,
                [(Start.AddDays(2).AddMinutes(5), 23), (Start.AddDays(2).AddMinutes(50), 24)]),
        ]);

        Assert.Equal([22, 23, 24], dings.Select(d => d.Value));
        // Two AA observations, not three: the middle sitting earned none and is a HOLD.
        Assert.Equal([4, 7], aa.Select(a => a.Value));
    }

    /// <summary>A caption with no chart is null rather than an empty string, so a host
    /// cannot render a bare label over a blank canvas — trap 17 in typography, and exactly
    /// the pair the view moves together.</summary>
    [Fact]
    public void ACaptionWithNoChartIsNullRatherThanEmpty()
    {
        Assert.Null(HistoryPresentation.CareerLevelCaption(null, []));
        Assert.Null(HistoryPresentation.CareerAaCaption(null, []));

        var dings = new List<(DateTime Time, double Value)>
            { (Start, 22), (Start.AddDays(2), 24) };
        var graph = HistoryPresentation.BuildStepGraph(dings, 300, 80)!;
        Assert.Equal("Level 22 → 24 (Sep 5–Sep 7, 2 dings)",
            HistoryPresentation.CareerLevelCaption(graph, dings));
    }

    /// <summary>The ladders' own heading is the studio's, verbatim — two surfaces describing
    /// one chart two ways is the drift a shared constant exists to stop.</summary>
    [Fact]
    public void TheLaddersUseTheStudiosOwnHeading()
    {
        Assert.Equal("Character progress — every stored session",
            HistoryPresentation.CareerLaddersCaption);
    }

    // ---- 4. the tab, and the badge behind it -----------------------------------

    /// <summary>The badge is a COUNT and never an age. "3h ago" is the obvious alternative
    /// and it is the one thing it may not be: this room repaints on the widget's tick, and a
    /// chip whose text changes width every tick is trap 12 wearing a chip.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "1 sitting")]
    [InlineData(14, "14 sittings")]
    public void TheHistoryBadgeCountsSittings(int stored, string? expected) =>
        Assert.Equal(expected, ProgressTheme.History(stored));

    /// <summary>The tab is reachable by address in the Evolved shell, which is what makes it
    /// a room rather than a panel — and the address is the SURFACE's key, never a spelling
    /// the shell invented.</summary>
    [Fact]
    public void TheCareerTabIsReachableByItsOwnAddress()
    {
        Assert.Contains(ProgressSurface.KeyFor(ProgressTab.History),
            ShellPages.Rooms(ShellPage.Progress).Select(r => r.Key));
        Assert.Equal((ShellPage.Progress, (string?)"history"),
            ShellPages.ParseAddress("progress:history"));
        Assert.Equal(ProgressTab.History, ProgressSurface.TabForKey("history"));
    }
}
