using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The durable level-up list (#240, joeymavity). The rows themselves are a merge of two
/// sources that overlap for one tick, which is the whole reason this is a module with
/// tests rather than three lines in a card.
/// </summary>
public class LevelHistoryTests
{
    private static readonly DateTime Aug21 = new(2026, 8, 21, 20, 14, 0);
    private static readonly DateTime Aug22 = new(2026, 8, 22, 21, 30, 0);
    private static readonly DateTime Aug23 = new(2026, 8, 23, 19, 5, 0);

    private static SessionRepository.ProgressPoint Session(
        DateTime end, params (DateTime Time, int Level)[] dings) =>
        new(end, 0, [.. dings]);

    private static StatsSnapshot Live(params (DateTime Time, int Level)[] dings) =>
        new() { Levels = [.. dings.Select(d => new TimedDetail(d.Time, $"Level {d.Level}"))] };

    /// <summary>The list a player opens is "when did I ding", so the last ding is the first
    /// row. The merge itself has to run chronologically (the gap is measured against the
    /// ding BEFORE each row) — the reversal is what makes the two orders agree.</summary>
    [Fact]
    public void RowsAreNewestFirstAcrossBothSources()
    {
        var rows = LevelHistory.Rows(
            [Session(Aug21, (Aug21, 22)), Session(Aug22, (Aug22, 23))],
            Live((Aug23, 24)));

        Assert.Equal([24, 23, 22], rows.Select(r => r.Level));
    }

    /// <summary>A session finalised while the widget is still up is in BOTH sources: the
    /// ding is in the live snapshot from parse time and in the store from write time. The
    /// same level-up must be ONE row — the hand-over is the failure this module exists to
    /// make impossible, and it happens on exactly the surface someone opened to check.</summary>
    [Fact]
    public void ADingPresentInTheStoreAndTheLiveSessionIsOneRow()
    {
        var rows = LevelHistory.Rows([Session(Aug23, (Aug23, 24))], Live((Aug23, 24)));

        var row = Assert.Single(rows);
        Assert.Equal(24, row.Level);
        Assert.Equal(Aug23, row.Time);
    }

    /// <summary>Same level at a DIFFERENT time is a different ding — a re-level after a
    /// death, or the same number on another evening's snapshot. Dedupe keys on both
    /// fields; keying on the level alone would swallow it.</summary>
    [Fact]
    public void TheSameLevelAtADifferentTimeIsNotDeduped()
    {
        var rows = LevelHistory.Rows(
            [Session(Aug22, (Aug22, 23)), Session(Aug23, (Aug23, 23))], null);

        Assert.Equal(2, rows.Count);
    }

    /// <summary>A new profile has no stored sessions and the first launch after a session
    /// roll has no live dings. Either side alone is a complete answer.</summary>
    [Fact]
    public void EitherSourceAloneProducesTheList()
    {
        Assert.Equal([24], LevelHistory.Rows(null, Live((Aug23, 24))).Select(r => r.Level));
        Assert.Equal([22], LevelHistory.Rows([Session(Aug21, (Aug21, 22))], null)
            .Select(r => r.Level));
        Assert.Empty(LevelHistory.Rows(null, null));
        Assert.Empty(LevelHistory.Rows([Session(Aug21)], new StatsSnapshot()));
    }

    /// <summary>The gap spans sessions — that is the fact the session-scoped "(43m)" in the
    /// Experience summary line cannot tell you, and the reason this list is worth having.
    /// The OLDEST row has no previous ding, so it carries null rather than a zero that
    /// would read as "instant".</summary>
    [Fact]
    public void SincePreviousMeasuresAcrossSessionsAndIsNullForTheOldest()
    {
        var rows = LevelHistory.Rows(
            [Session(Aug21, (Aug21, 22)), Session(Aug22, (Aug22, 23))], Live((Aug23, 24)));

        Assert.Equal(Aug23 - Aug22, rows[0].SincePrevious);
        Assert.Equal(Aug22 - Aug21, rows[1].SincePrevious);
        Assert.Null(rows[2].SincePrevious);
    }

    /// <summary>Out-of-order input (a snapshot mined after a later one, a live ding read
    /// before the store) must not produce a negative gap: the sort is what defines
    /// "previous", not the order the dings arrived in.</summary>
    [Fact]
    public void GapsAreNeverNegativeWhateverOrderTheSourcesArriveIn()
    {
        var rows = LevelHistory.Rows(
            [Session(Aug23, (Aug23, 24)), Session(Aug21, (Aug21, 22))],
            Live((Aug22, 23)));

        Assert.Equal([24, 23, 22], rows.Select(r => r.Level));
        Assert.All(rows, r => Assert.True(r.SincePrevious is null or { Ticks: >= 0 }));
    }

    /// <summary>Only rows shaped like a level-up come out of the live snapshot's display
    /// text. <c>Levels</c> is a list of formatted strings, and a row that is not a level is
    /// not a level-up.</summary>
    [Fact]
    public void LiveEntriesThatAreNotLevelsAreIgnored()
    {
        var rows = LevelHistory.Rows(null, new StatsSnapshot
        {
            Levels = [new TimedDetail(Aug23, "Level 24"), new TimedDetail(Aug23, "Dinged!")],
        });

        Assert.Equal([24], rows.Select(r => r.Level));
    }

    /// <summary>A wall-clock stamp, fixed for the life of the row. **Never "x ago"**: an
    /// age changes measured text width on a SizeToContent window every tick (trap 12) and
    /// re-wakes every connected phone through the fingerprint (trap 8).</summary>
    [Fact]
    public void TimesAreWallClockAndNothingSaysAgo()
    {
        Assert.Equal("Aug 23, 7:05 PM", LevelHistory.Format(Aug23));

        var rows = LevelHistory.Rows([Session(Aug21, (Aug21, 22))], Live((Aug23, 24)));
        var text = string.Join("|", LevelHistory.CardRows(rows).Select(r => $"{r.Name} {r.Value}"))
                   + "|" + LevelHistory.FoldLabel(rows)
                   + "|" + string.Join("|", rows.Select(LevelHistory.Tooltip));
        Assert.DoesNotContain("ago", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The two largest units, and a zero second unit dropped rather than printed.
    /// Sub-minute says "&lt;1m" — "0m" would read as a measurement of nothing.</summary>
    [Theory]
    [InlineData(0, 0, 30, "<1m")]
    [InlineData(0, 0, 90, "1m")]
    [InlineData(0, 43, 0, "43m")]
    [InlineData(3, 20, 0, "3h 20m")]
    [InlineData(3, 0, 0, "3h")]
    [InlineData(27, 0, 0, "1d 3h")]
    [InlineData(48, 0, 0, "2d")]
    public void GapsFormatToTheirTwoLargestUnits(int hours, int minutes, int seconds, string expected) =>
        Assert.Equal(expected, LevelHistory.FormatGap(
            new TimeSpan(0, hours, minutes, seconds)));

    /// <summary>Bevel's call (2026-09-02): the gap is hover text, not a third column. The
    /// oldest row has no gap and therefore no tooltip — an empty hover is worse than none.</summary>
    [Fact]
    public void TheGapIsATooltipAndTheOldestRowHasNone()
    {
        var rows = LevelHistory.Rows([Session(Aug21, (Aug21, 22))], Live((Aug23, 24)));

        Assert.Equal("1d 22h since the previous level-up", LevelHistory.Tooltip(rows[0]));
        Assert.Null(LevelHistory.Tooltip(rows[1]));
    }

    /// <summary>The fold is closed by default, so its label has to answer the glance
    /// question on its own: how many, and when was the last one.</summary>
    [Fact]
    public void TheFoldedLabelCarriesTheCountAndTheLastDingsDate()
    {
        var rows = LevelHistory.Rows(
            [Session(Aug21, (Aug21, 22)), Session(Aug22, (Aug22, 23))], Live((Aug23, 24)));

        Assert.Equal("Level-ups (3) · last Aug 23", LevelHistory.FoldLabel(rows));
        Assert.Equal("", LevelHistory.FoldLabel([]));
    }

    /// <summary>One ding is still a fold with one row — the empty case is "no dings at
    /// all", not "not enough to bother".</summary>
    [Fact]
    public void OneDingStillGetsALabelAndARow()
    {
        var rows = LevelHistory.Rows(null, Live((Aug23, 24)));

        Assert.Equal("Level-ups (1) · last Aug 23", LevelHistory.FoldLabel(rows));
        Assert.Equal([("Level 24", "Aug 23, 7:05 PM")], LevelHistory.CardRows(rows));
    }
}
