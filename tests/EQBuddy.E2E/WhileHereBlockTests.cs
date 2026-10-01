using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// WHILE YOU'RE HERE in the running app (DRA-42 D1): the Guide room's block answers for the zone
/// the LOG last entered, and draws what the producer answered.
///
/// <para>The zone arrives as a log line through the app's own parser (trap 23) — the fact the
/// Planner's SIGN asked to be pinned is that the block keys on the latest "You have entered"
/// line, so a test that set the zone any other way would be testing a different claim. The
/// producer's answer and the rows on screen are dumped beside each other from one moment
/// (trap 56), because "the store says so" and "the screen says so" are different claims.</para>
/// </summary>
public class WhileHereBlockTests
{
    /// <summary>A tracked quest with pieces that drop in West Commonlands ONLY — the unit
    /// suite's exhibit, and the reason entering Commonlands must not light it.</summary>
    private const string ArmorOfRo = "Armor of Ro Quests";

    /// <summary>How many of the quest's open steps the shipped catalogs place in a zone —
    /// computed from the SAME producer the app runs, over a profile holding only the pin, so
    /// this suite cannot drift from the harvest (trap 23).</summary>
    private static int ExpectedRequired(string zone)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wh-e2e-{Guid.NewGuid():N}.json");
        try
        {
            var ledger = new QuestLedgerStore(path) { TrackFilter = _ => true };
            ledger.SetTracked("x", ArmorOfRo, true);
            var s = new AppSettings();
            s.SkyQuestChecklist.AddRange(SkyChecklistRows.Items.Select(i => i.Clone()));
            return WhileHere.For(new WhileHereInputs(zone, s, ledger, "x",
                QuestCatalog.LoadEmbedded(), GuideCatalog.Default, ItemCatalog.Default, [], ""))
                .Required.Count;
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + ".rules"); } catch { }
        }
    }

    [Fact]
    public void TheBlockAnswersForTheZoneTheLogLastEnteredAndDrawsIt()
    {
        var expected = ExpectedRequired("West Commonlands");
        Assert.True(expected >= 2, "the fixture needs a tracked quest with steps in West Commonlands");
        Assert.NotEqual(expected, ExpectedRequired("Commonlands"));

        using var app = new AppHarness(
            configureSettings: null,
            environment: new Dictionary<string, string> { ["EQBUDDY_SHELL"] = "quests:general" });
        app.SeedQuestLedger(tracked: [ArmorOfRo]);
        app.Launch();

        app.WaitForDump("shellQuestsTab", "general", "the shell to reach the Guide room");

        // Commonlands first: the looser containment rule would light West Commonlands' pieces
        // here too, and the exact join must answer only for Commonlands' own.
        app.AppendLogLines("You have entered Commonlands.");
        app.WaitForDump("shellWhileHereZone", "Commonlands".Length,
            "the block to read the entered zone");
        Assert.Equal(ExpectedRequired("Commonlands"), app.DumpValue("shellWhileHereRequired"));

        // Then West Commonlands — the LATEST entered line is where the player is.
        app.AppendLogLines("You have entered West Commonlands.");
        app.WaitForDump("shellWhileHereRequired", expected,
            "the tracked quest's West Commonlands steps to be required here");
        var seen = app.DumpValues("shellWhileHereRequired", "shellWhileHereRelevant",
            "shellWhileHereStepsDrawn", "shellWhileHereOpen");
        Assert.Equal(expected, seen[0]);
        // The screen drew what the producer answered — both step groups, each under the
        // room's cap — from the same moment as the answer (trap 56).
        var cap = WhileHerePresentation.StepsPerGroup;
        Assert.Equal(Math.Min(seen[0], cap) + Math.Min(seen[1], cap), seen[2]);
        Assert.Equal(1, seen[3]);
    }

    [Fact]
    public void LeavingAZoneWithTrackedStepsOpenDrawsTheNoticeTheBlockCounted()
    {
        // DRA-42 D2: the notice arrives AFTER the move, from the log's own entered lines, and
        // names exactly what the block listed as the player's own work while they were there —
        // the same producer asked about the zone just left (trap 4).
        Assert.True(ExpectedRequired("West Commonlands") >= 2,
            "the fixture needs a tracked quest with steps in West Commonlands");

        using var app = new AppHarness(
            configureSettings: null,
            environment: new Dictionary<string, string> { ["EQBUDDY_SHELL"] = "quests:general" });
        app.SeedQuestLedger(tracked: [ArmorOfRo]);
        app.Launch();
        app.WaitForDump("shellQuestsTab", "general", "the shell to reach the Guide room");

        app.AppendLogLines("You have entered West Commonlands.");
        app.WaitForDump("shellWhileHereRequired", ExpectedRequired("West Commonlands"),
            "the tracked quest's West Commonlands steps to be required here");
        // Not asserted absent here: the fixture log's own earlier zone may already have left
        // something open, and that notice is true. What matters is what the NEXT move says.
        var there = app.DumpValues("shellWhileHereRequired", "shellWhileHereRelevant");

        // Somewhere with none of those steps: Commonlands, the exact join's committed negative.
        // Waited on the ZONE, a positive event only the new entered line can cause (trap 62).
        app.AppendLogLines("You have entered Commonlands.");
        app.WaitForDump("shellWhileHereZone", "Commonlands".Length, "the block to read Commonlands");
        var left = app.DumpValues("shellWhileHereLeft", "shellWhileHereLeftSteps",
            "shellWhileHereLeftOpen", "shellWhileHereLeftStepsDrawn");
        Assert.Equal(1, left[0]);
        Assert.Equal(there[0] + there[1], left[1]);
        // It arrives CLOSED — the rows are behind its door.
        Assert.Equal(0, left[2]);
        Assert.Equal(0, left[3]);
    }
}
