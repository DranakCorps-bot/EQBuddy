using System.Text.Json;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

public sealed class HistoryPresentationTests
{
    [Fact]
    public void FilterRetainsQueryValuesWithoutParsingLabel()
    {
        var filter = HistoryPresentation.BuildFilter("freeport", "Kaybek");

        Assert.Equal("Kaybek (freeport)", filter.Label);
        Assert.Equal("freeport", filter.Server);
        Assert.Equal("Kaybek", filter.Character);
        Assert.Equal("All characters", HistoryFilterOption.All.Label);
    }

    [Theory]
    [InlineData("", " - 1h 2m")]
    [InlineData("Clan Crushbone", "Clan Crushbone - 1h 2m")]
    public void SessionRowFormatsZoneAndDuration(string zone, string expected)
    {
        var row = Row(primaryZone: zone, endReason: "Active");

        var text = HistoryPresentation.BuildSessionRow(row);

        Assert.Contains(expected, text);
        Assert.Contains("1 kill", text);
        Assert.EndsWith(" - (in progress)", text);
    }

    [Fact]
    public void SessionRowMarksRecoveredSession()
    {
        var text = HistoryPresentation.BuildSessionRow(Row(endReason: "RecoveredAfterCrash"));
        Assert.EndsWith(" - (recovered)", text);
    }

    [Fact]
    public void DetailSplitsOverviewAndBuildsNativeBreakdownRows()
    {
        var detail = HistoryPresentation.BuildDetail(Row(), Snapshot());

        // Header + rest carry the text sections; breakdowns become structured rows.
        Assert.Contains("Duration", detail.HeaderText);
        Assert.DoesNotContain("Top damage sources:", detail.HeaderText);
        Assert.Contains("Kills by creature:", detail.RestText);
        Assert.NotEmpty(detail.DamageRows);
        var top = detail.DamageRows[0];
        Assert.Equal(1.0, top.Fraction, 3);              // top source spans the full bar
        Assert.Contains("avg", top.Value);
        Assert.Contains("dps", top.Value);
        Assert.Contains("% of total", top.Tooltip);
        Assert.All(detail.DamageRows, r => Assert.InRange(r.Fraction, 0, 1));
    }

    [Fact]
    public void OverviewAndComparisonContainSharedReportSections()
    {
        var snapshot = Snapshot();
        var first = Row(character: "Kaybek", primaryZone: "Clan Crushbone");
        var second = Row(id: 2, character: "Douglas", primaryZone: "Blackburrow");

        var overview = HistoryPresentation.BuildOverview(first, snapshot);
        var comparison = HistoryPresentation.BuildComparison(first, snapshot, second, snapshot);

        Assert.Contains("Top damage sources:", overview);
        Assert.Contains("Kills by creature:", overview);
        Assert.Contains("Loot:", overview);
        Assert.Contains("Zones:", overview);
        Assert.Contains("SESSION COMPARISON", comparison);
        Assert.Contains("different character/zone", comparison);
        Assert.Equal(HistoryPresentation.MissingComparisonText,
            HistoryPresentation.BuildComparison(first, null, second, snapshot));
    }

    [Fact]
    public void ExportProducesSuggestedNameAndIndentedJson()
    {
        var row = Row(start: new DateTime(2026, 7, 18, 15, 4, 0));
        var json = HistoryPresentation.BuildExportJson(Snapshot());

        Assert.Equal("eqbuddy-Kaybek-20260718-1504.json", HistoryPresentation.BuildExportFileName(row));
        Assert.Contains(Environment.NewLine, json);
        Assert.NotNull(JsonDocument.Parse(json));
    }

    internal static StatsSnapshot Snapshot()
    {
        var stats = new SessionStats { CharacterName = "Kaybek", ServerName = "freeport" };
        foreach (var line in new[]
        {
            "[Sat Jul 18 15:00:00 2026] You have entered Clan Crushbone.",
            "[Sat Jul 18 15:00:05 2026] You slash orc pawn for 10 points of damage.",
            "[Sat Jul 18 15:00:10 2026] You have slain orc pawn!",
            "[Sat Jul 18 15:00:12 2026] --You have looted a Mote of Infinitesimal Potential from orc pawn's corpse.--",
            "[Sat Jul 18 15:00:15 2026] You gain party experience! (1.5%)",
        })
            stats.Apply(LogParser.Parse(line)!);
        return stats.Snapshot();
    }

    internal static SessionRow Row(long id = 1, string character = "Kaybek", string primaryZone = "Clan Crushbone",
        string endReason = "Manual", DateTime? start = null) =>
        new(id, "freeport", character, start ?? new DateTime(2026, 7, 18, 15, 0, 0), null,
            3720, 120, endReason, primaryZone, 1, 1.5, 100, 1, 0, 5, "", "");
}
