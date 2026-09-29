using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The minimized bar must not walk through the log's earlier sessions while the startup
/// replay is running (Founder's recording, 2026-09-28). The replay itself is real: a log
/// holding two sessions an hour apart really does show the FIRST session's numbers from a
/// snapshot taken between them — the first test proves that premise so the gate is not
/// guarding against something that cannot happen.
/// </summary>
public sealed class ReplayPaintGateTests
{
    private static void Feed(SessionStats stats, params string[] lines)
    {
        foreach (var line in lines)
        {
            Assert.True(LogParser.TrySplitLine(line, out var ts, out var msg), line);
            if (LogParser.Parse(ts, msg) is { } evt) stats.Apply(evt);
        }
    }

    [Fact]
    public void MidReplayTheLiveSnapshotCarriesAnEarlierSessionsNumbers()
    {
        var stats = new SessionStats();
        // An OLD session: one hit, then nothing for hours.
        Feed(stats, "[Mon Sep 28 09:00:00 2026] You slash a gnoll pup for 40 points of damage.");
        var midReplay = stats.Snapshot();

        Assert.True(midReplay.DamageDealt > 0,
            "premise: a snapshot taken mid-replay shows an old session's damage");
        // ...and the gate hides it while ingest is not done,
        var shown = ReplayPaintGate.ForDisplay(initialIngestDone: false, midReplay);
        Assert.Same(ReplayPaintGate.Replaying, shown);
        Assert.Equal(0, shown.DamageDealt);
    }

    [Fact]
    public void TheBarKnowsTheReplayPlaceholderByIdentityNotByBeingEmpty()
    {
        Assert.True(ReplayPaintGate.IsReplaying(ReplayPaintGate.ForDisplay(false, new StatsSnapshot())));
        // A genuinely empty session (a fresh character, nothing logged yet) is NOT "reading":
        // the bar must draw its real zeroes rather than claim a re-read that is not running.
        Assert.False(ReplayPaintGate.IsReplaying(new StatsSnapshot()));
        Assert.False(ReplayPaintGate.IsReplaying(ReplayPaintGate.ForDisplay(true, new StatsSnapshot())));
    }

    [Fact]
    public void TheReadingWordsSayWhatIsHappeningAndThatItEnds()
    {
        Assert.Equal("Reading log…", ReplayPaintGate.ReadingLabel);
        Assert.Contains("re-reads your log", ReplayPaintGate.ReadingTip);
        Assert.Contains("once", ReplayPaintGate.ReadingTip);
    }

    [Fact]
    public void TheChipRowIsEmptyDuringTheReplayAndItsBuilderIsNotEvenRun()
    {
        var built = 0;
        var row = new List<HudChipEntry>();
        List<HudChipEntry> Build() { built++; return row; }

        Assert.Empty(ReplayPaintGate.ChipRow(initialIngestDone: false, Build));
        Assert.Equal(0, built);

        // ...and once the replay is done, the real row — the same list, built once.
        Assert.Same(row, ReplayPaintGate.ChipRow(initialIngestDone: true, Build));
        Assert.Equal(1, built);
    }

    [Fact]
    public void OnceIngestIsDoneTheLiveSnapshotIsShownUnchanged()
    {
        var stats = new SessionStats();
        Feed(stats, "[Mon Sep 28 13:40:00 2026] You slash a gnoll pup for 40 points of damage.");
        var live = stats.Snapshot();

        Assert.Same(live, ReplayPaintGate.ForDisplay(initialIngestDone: true, live));
    }
}
