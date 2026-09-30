using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// BEFORE YOU LEAVE (DRA-42 D2, requirements §19, the log-only reading) — the standing line
/// while in a zone and the notice after an observed zone change, both the D1 producer's answer
/// re-grouped (trap 4), asserted over the SHIPPED catalogs for <see cref="WhileHereTests"/>'
/// reason: the join IS the item pages' drop zones.
/// </summary>
public sealed class WhileHereDepartureTests : IDisposable
{
    private const string Character = "dranak_legends";
    private const string ArmorOfRo = "Armor of Ro Quests";
    private const string NightfallStep = "Collect Nightfall Giant's Head";

    private static readonly DateTime Entered = new(2026, 9, 30, 20, 0, 0);
    private static readonly DateTime Left = Entered.AddMinutes(40);

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"while-here-left-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private static readonly QuestCatalog Quests = QuestCatalog.LoadEmbedded();

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.EpicQuestChecklist.AddRange(EpicQuestDefaults.Items());
        s.SkyQuestChecklist.AddRange(SkyChecklistRows.Items.Select(i => i.Clone()));
        return s;
    }

    private static WhileHereInputs Inputs(string? zone, AppSettings s, QuestLedgerStore ledger) =>
        new(zone, s, ledger, Character, Quests, GuideCatalog.Default, ItemCatalog.Default, [], "");

    /// <summary>The snapshot's zone list, as <c>SessionStats</c> writes it.</summary>
    private static List<TimedDetail> Zones(params (string Zone, DateTime At)[] entries) =>
        [.. entries.Select(e => new TimedDetail(e.At, e.Zone))];

    private static WhileHereDeparture? Depart(AppSettings s, QuestLedgerStore ledger,
        params (string Zone, DateTime At)[] entries)
    {
        var zones = Zones(entries);
        return WhileHere.DepartureFor(Inputs(zones.Count > 0 ? zones[^1].Text : null, s, ledger), zones);
    }

    private QuestLedgerStore Tracking()
    {
        var ledger = Store();
        ledger.SetTracked(Character, ArmorOfRo, true);
        return ledger;
    }

    // ── The departure: after the move, from the log ───────────────────────────────────────

    [Fact]
    public void LeavingWestCommonlandsWithATrackedStepOpenNamesItAfterTheMove()
    {
        var left = Depart(Settings(), Tracking(),
            ("West Commonlands", Entered), ("Commonlands", Left));

        Assert.NotNull(left);
        Assert.Equal("West Commonlands", left.From);
        // The stamp of the ENTRY that ended the stay, i.e. when the log saw the move.
        Assert.Equal(Left, left.At);
        Assert.Contains(left.Steps, s => s.Quest == ArmorOfRo && s.Step == NightfallStep);
    }

    [Fact]
    public void TheDepartureIsTheProducersAnswerForTheZoneLeftHoldingOnlyOwnWork()
    {
        // One producer, asked about another zone (trap 4): the steps are exactly D1's Required
        // and Relevant for West Commonlands, and none of its Optional quests are "left".
        var s = Settings();
        var ledger = Tracking();
        var there = WhileHere.For(Inputs("West Commonlands", s, ledger));
        Assert.NotEmpty(there.Optional);

        var left = Depart(s, ledger, ("West Commonlands", Entered), ("Commonlands", Left))!;
        Assert.Equal(
            there.Required.Concat(there.Relevant).Select(x => (x.Quest, x.StepId)),
            left.Steps.Select(x => (x.Quest, x.StepId)));
        Assert.Empty(left.Left.Optional);
        Assert.Equal(0, left.Left.Filtered);
        Assert.Equal(0, left.Left.UnplacedTracked);
    }

    [Fact]
    public void AZoneWhoseOnlyStepsAreOptionalLeavesNothingBehind()
    {
        // Nothing tracked, nothing started: the zone still has Optional quests, and leaving
        // them is not leaving anything of yours.
        var s = Settings();
        var ledger = Store();
        Assert.NotEmpty(WhileHere.For(Inputs("West Commonlands", s, ledger)).Optional);
        Assert.Null(Depart(s, ledger, ("West Commonlands", Entered), ("Commonlands", Left)));
    }

    [Fact]
    public void AStepTickedAfterLeavingLeavesTheNoticeBecauseNothingIsRememberedFromTheMove()
    {
        var s = Settings();
        var ledger = Tracking();
        var before = Depart(s, ledger, ("West Commonlands", Entered), ("Commonlands", Left))!;
        Assert.Contains(before.Steps, x => x.Step == NightfallStep);

        ledger.SetManual(Character, "Nightfall Giant's Head", 1);
        var after = Depart(s, ledger, ("West Commonlands", Entered), ("Commonlands", Left));
        Assert.DoesNotContain(after?.Steps ?? [], x => x.Step == NightfallStep);

        // ...and untracking removes the rest, so the notice goes with its last own step.
        ledger.SetTracked(Character, ArmorOfRo, false);
        ledger.SetManual(Character, "Nightfall Giant's Head", 0);
        Assert.Null(Depart(s, ledger, ("West Commonlands", Entered), ("Commonlands", Left)));
    }

    [Fact]
    public void NoEarlierZoneNoPlaceOrARespellingOfWhereYouAreIsNoDeparture()
    {
        var s = Settings();
        var ledger = Tracking();
        Assert.Null(Depart(s, ledger));
        Assert.Null(Depart(s, ledger, ("West Commonlands", Entered)));
        Assert.Null(Depart(s, ledger, ("Various Zones", Entered), ("Commonlands", Left)));
        // SessionStats folds repeats case-insensitively; IdentityKey folds the article too, so
        // a zone the log spelled two ways is one zone and not a departure.
        ledger.SetTracked(Character, "Bard Sky Test: Amulet of the Fae", true);
        Assert.NotNull(WhileHere.For(Inputs("Plane of Sky", s, ledger)).Required.FirstOrDefault());
        Assert.Null(Depart(s, ledger, ("The Plane of Sky", Entered), ("Plane of Sky", Left)));
    }

    [Fact]
    public void OnlyTheLatestMoveIsADepartureAndReEnteringASecondTimeIsANewOne()
    {
        var s = Settings();
        var ledger = Tracking();
        // West → Commonlands → West: the player is back, so West is not "left".
        Assert.Null(Depart(s, ledger, ("West Commonlands", Entered), ("Commonlands", Left),
            ("West Commonlands", Left.AddMinutes(5))));

        // Leaving West twice is two departures with two keys: dismissing the first must not
        // swallow the second.
        var first = Depart(s, ledger, ("West Commonlands", Entered), ("Commonlands", Left))!;
        var second = Depart(s, ledger, ("West Commonlands", Entered), ("Commonlands", Left),
            ("West Commonlands", Left.AddMinutes(5)), ("Commonlands", Left.AddMinutes(9)))!;
        Assert.Equal(first.From, second.From);
        Assert.NotEqual(first.Key, second.Key);
    }

    // ── The standing line and the per-quest arrangement ───────────────────────────────────

    [Fact]
    public void TheStandingLineCountsTheSameStepsTheGroupsList()
    {
        var answer = WhileHere.For(Inputs("West Commonlands", Settings(), Tracking()));
        var own = answer.Required.Count + answer.Relevant.Count;
        Assert.True(own > 0);
        var line = WhileHerePresentation.LeaveLine(answer)!;
        Assert.StartsWith($"{own} open step", line);
        Assert.Contains($"{ArmorOfRo} (", line);
        Assert.Equal("Before you leave West Commonlands", WhileHerePresentation.LeaveHeading(answer.Zone));
    }

    [Fact]
    public void TheClearLineSaysWhatWasCheckedAndNoLineIsDrawnWithoutAPlace()
    {
        var clear = WhileHerePresentation.LeaveLine(
            new WhileHereAnswer("Crushbone", WhileHereState.NothingOpenHere, [], [], [], 2))!;
        // "Placed here in EQBuddy's catalogs": an unplaced tracked step is not in it, and the
        // trailing count beside it says so (§5.9).
        Assert.Contains("placed here in EQBuddy's catalogs", clear);
        Assert.DoesNotContain("complete", clear, StringComparison.OrdinalIgnoreCase);

        Assert.Null(WhileHerePresentation.LeaveLine(WhileHereAnswer.None));
        Assert.Null(WhileHerePresentation.LeaveLine(
            new WhileHereAnswer("Various Zones", WhileHereState.NotAPlace, [], [], [], 0)));
    }

    [Fact]
    public void ThePerQuestLineKeepsStepOrderCountsEachQuestAndSaysWhatItHeldBack()
    {
        static WhileHereStep S(string q, string id) => new(q, id, id, []);
        Assert.Equal("B (2) · A (1)",
            WhileHerePresentation.ByQuest([S("B", "1"), S("A", "2"), S("b", "3")]));
        Assert.Equal("A (1) · B (1) · C (1) · and 1 more quest",
            WhileHerePresentation.ByQuest([S("A", "1"), S("B", "2"), S("C", "3"), S("D", "4")]));
        Assert.EndsWith("and 2 more quests",
            WhileHerePresentation.ByQuest([S("A", "1"), S("B", "2"), S("C", "3"), S("D", "4"), S("E", "5")]));
    }

    [Fact]
    public void TheNoticeIsWordedAsAfterTheMoveAndOffersNoContinueAnyway()
    {
        var left = Depart(Settings(), Tracking(), ("West Commonlands", Entered), ("Commonlands", Left))!;
        var notice = WhileHerePresentation.Departed(left);
        Assert.StartsWith("You left West Commonlands with ", notice);
        Assert.Equal(WhileHerePresentation.ByQuest(left.Steps), WhileHerePresentation.DepartedQuests(left));
        foreach (var s in new[] { notice, WhileHerePresentation.DepartedTip, WhileHerePresentation.DismissOnPc })
            Assert.DoesNotContain("continue anyway", s, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoD2SentenceCallsACampSafeEasyOrSurvivable()
    {
        var left = Depart(Settings(), Tracking(), ("West Commonlands", Entered), ("Commonlands", Left))!;
        var sentences = new[]
        {
            WhileHerePresentation.LeaveHeading("Crushbone"),
            WhileHerePresentation.LeaveLine(new WhileHereAnswer("Crushbone", WhileHereState.NothingOpenHere, [], [], [], 0))!,
            WhileHerePresentation.LeaveLine(WhileHere.For(Inputs("West Commonlands", Settings(), Tracking())))!,
            WhileHerePresentation.Departed(left), WhileHerePresentation.DepartedQuests(left),
            WhileHerePresentation.ShowDeparted, WhileHerePresentation.HideDeparted,
            WhileHerePresentation.DismissDeparted, WhileHerePresentation.DepartedTip,
            WhileHerePresentation.DismissOnPc,
        };
        foreach (var s in sentences)
            foreach (var banned in new[] { "safe", "easy", "surviv" })
                Assert.DoesNotContain(banned, s, StringComparison.OrdinalIgnoreCase);
    }
}
