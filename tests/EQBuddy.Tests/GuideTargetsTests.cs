using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **MAP TARGETS FROM GUIDE STEPS** (DRA-42 D3, requirements §20) — the reader that asks "does
/// this dot serve an open guide step", over the SHIPPED catalogs for <see cref="WhileHereTests"/>'
/// reason: the join is the item pages' drop zones and droppers, and a hand-made fixture would
/// prove a join the product does not have.
///
/// <para>Every positive has its committed negative beside it (trap 39): the wrong zone, a person
/// rather than a dropper, a near-miss name the fuzzy matcher would have taken, and the layer
/// switched off.</para>
/// </summary>
public sealed class GuideTargetsTests : IDisposable
{
    private const string Character = "dranak_legends";

    /// <summary>The D1 exhibit: its Nightfall Giant's Head drops off <i>a nightfall giant</i> in
    /// West Commonlands ONLY, and its first step is done at Lord Searfire.</summary>
    private const string ArmorOfRo = "Armor of Ro Quests";
    private const string NightfallStep = "Collect Nightfall Giant's Head";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"guide-targets-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private static readonly QuestCatalog Quests = QuestCatalog.LoadEmbedded();

    private QuestLedgerStore Tracked(string quest)
    {
        var ledger = new QuestLedgerStore(_path) { TrackFilter = _ => true };
        ledger.SetTracked(Character, quest, true);
        return ledger;
    }

    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.EpicQuestChecklist.AddRange(EpicQuestDefaults.Items());
        s.SkyQuestChecklist.AddRange(SkyChecklistRows.Items.Select(i => i.Clone()));
        return s;
    }

    private static WhileHereAnswer Ask(string zone, AppSettings s, QuestLedgerStore ledger) =>
        WhileHere.For(new WhileHereInputs(zone, s, ledger, Character, Quests,
            GuideCatalog.Default, ItemCatalog.Default, [], ""));

    [Fact]
    public void ATrackedStepsDropperMarksThePointWhereItWasKilled()
    {
        var answer = Ask("West Commonlands", Settings(), Tracked(ArmorOfRo));

        var here = GuideTargets.Here(answer, "West Commonlands");
        var step = Assert.Single(here, s => s.Step == NightfallStep);
        Assert.True(step.WhoDrops);

        // The ledger's key is the kill line's own spelling; the page's is a wiki title. The
        // strict fold still joins them (the article and the case).
        var hits = GuideTargets.AtPoint(here, ["a nightfall giant"]);
        var hit = Assert.Single(hits, h => h.Step == NightfallStep);
        Assert.Equal(ArmorOfRo, hit.Quest);
        // A point that has only seen something else is not marked.
        Assert.Empty(GuideTargets.AtPoint(here, ["a giant rat"]));
    }

    /// <summary>The map asks about the zone IT shows. An answer about another zone marks nothing
    /// there — exact, then IdentityKey, and nothing looser.</summary>
    [Fact]
    public void AnAnswerAboutAnotherZoneMarksNothingOnThisMap()
    {
        var answer = Ask("West Commonlands", Settings(), Tracked(ArmorOfRo));

        Assert.NotEmpty(GuideTargets.Here(answer, "west commonlands"));
        Assert.Empty(GuideTargets.Here(answer, "Commonlands"));
        Assert.Equal(0, GuideTargets.Unmarkable(answer, "Commonlands"));
        Assert.Empty(GuideTargets.Here(answer, null));
        Assert.Empty(GuideTargets.Here(null, "West Commonlands"));
    }

    /// <summary>A step done at a PERSON is never a mark, even on a point where that person's name
    /// was killed — that point is where somebody killed the one you should be talking to. It is
    /// counted instead, so the panel can say why it wears nothing.</summary>
    [Fact]
    public void AStepAtAPersonIsCountedAndNeverMarked()
    {
        var start = Quests.Quests.Single(q => q.Name == ArmorOfRo).StartZone!;
        var answer = Ask(start, Settings(), Tracked(ArmorOfRo));
        var speak = Assert.Single(answer.Required, s => s.StepId == "start-speak");
        Assert.False(speak.WhoDrops);
        Assert.NotEmpty(speak.Who);

        Assert.DoesNotContain(GuideTargets.Here(answer, start), s => s.StepId == "start-speak");
        Assert.True(GuideTargets.Unmarkable(answer, start) >= 1);
        // Hand the reader the person step directly: a point that has seen the giver still
        // matches nothing.
        Assert.Empty(GuideTargets.AtPoint([speak], speak.Who));
    }

    /// <summary>The match is <c>NameMatches</c>, never its fuzzy sibling: a near-miss the fuzzy
    /// rule would forgive is a mark on a dot that does not serve the step.</summary>
    [Fact]
    public void ANearMissNameIsNotAMatch()
    {
        var step = new WhileHereStep("Q", "s", "Collect a thing", ["a nightfall giant"], WhoDrops: true);

        Assert.Single(GuideTargets.AtPoint([step], ["A Nightfall Giant"]));
        Assert.True(SpawnCatalog.NameMatchesFuzzy("a nightfall giant", "a nightfal giant"));
        Assert.Empty(GuideTargets.AtPoint([step], ["a nightfal giant"]));
    }

    /// <summary>Only the player's OWN work is marked — a quest nobody has started is the Optional
    /// group, by name, and a mark is a place a player walks to.</summary>
    [Fact]
    public void AnUntouchedQuestIsNeverMarked()
    {
        var untracked = new QuestLedgerStore(_path) { TrackFilter = _ => true };
        var answer = Ask("West Commonlands", Settings(), untracked);

        Assert.Contains(ArmorOfRo, answer.Optional);
        Assert.DoesNotContain(GuideTargets.Here(answer, "West Commonlands"), s => s.Quest == ArmorOfRo);
    }

    /// <summary>A done step leaves the map through the same router that takes it off the room's
    /// block — nothing here remembers it.</summary>
    [Fact]
    public void ADoneStepLeavesTheMap()
    {
        var ledger = Tracked(ArmorOfRo);
        ledger.SetManual(Character, "Nightfall Giant's Head", 1);
        var answer = Ask("West Commonlands", Settings(), ledger);

        Assert.DoesNotContain(GuideTargets.Here(answer, "West Commonlands"), s => s.Step == NightfallStep);
    }

    /// <summary>The one switch, in the one place: off never builds the answer and answers the
    /// value every reader already draws nothing on.</summary>
    [Fact]
    public void TheSwitchIsOnByDefaultAndOffAnswersNothingWithoutAsking()
    {
        var s = new AppSettings();
        Assert.True(s.ShowGuideTargetsOnMap);
        Assert.True(s.ShowGearTargetsOnMap);

        var built = 0;
        var answer = new WhileHereAnswer("Z", WhileHereState.Answered, [], [], ["Q"], 0);
        Assert.Same(answer, GuideTargets.Gate(s, () => { built++; return answer; }));

        s.ShowGuideTargetsOnMap = false;
        Assert.Same(WhileHereAnswer.None, GuideTargets.Gate(s, () => { built++; return answer; }));
        Assert.Equal(1, built);
        // The two layers are two switches: hiding guide steps leaves the gear layer on.
        Assert.True(s.ShowGearTargetsOnMap);
    }

    // ── The words ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheRowReadsLikeTheGuideRoomsAndEveryCountKeepsItsDenominator()
    {
        var step = new WhileHereStep("Armor of Ro Quests", "s", NightfallStep,
            ["a", "b", "c", "d"], WhoDrops: true);
        var row = GuideTargetPresentation.StepRow(step);
        Assert.StartsWith(NightfallStep, row);
        Assert.Contains(WhileHerePresentation.StepDetail(step), row);
        Assert.Contains("1 more on its page", row);

        Assert.Contains("of your 7", GuideTargetPresentation.PointsHere(2, 7));
        Assert.Contains("of your 7", GuideTargetPresentation.PointsHere(1, 7));
        Assert.Contains("None of your 7", GuideTargetPresentation.PointsHere(0, 7));
        Assert.Contains("No spawn points archived", GuideTargetPresentation.PointsHere(0, 0));
        Assert.Contains("3 more steps", GuideTargetPresentation.MoreSteps(3));
        Assert.Equal("", GuideTargetPresentation.Unmarkable(0));
        Assert.Contains("2 more of your steps", GuideTargetPresentation.Unmarkable(2));
        Assert.Equal("", GuideTargetPresentation.CircleTip([]));
        Assert.Contains("(a nightfall giant)", GuideTargetPresentation.CircleTip(
            [new GuideTargetHit(ArmorOfRo, NightfallStep, "a nightfall giant")]));
    }

    /// <summary>Two layers, two names: the guide layer's words never reuse the gear layer's, so
    /// a player can tell the switches and the blocks apart.</summary>
    [Fact]
    public void TheGuideLayerIsNamedApartFromTheGearLayer()
    {
        Assert.NotEqual(GearTargetPresentation.ToggleLabel, GuideTargetPresentation.ToggleLabel);
        Assert.NotEqual(GearTargetPresentation.Heading("Z"), GuideTargetPresentation.Heading("Z"));
        Assert.NotEqual(GearTargetPresentation.PointsNote, GuideTargetPresentation.PointsNote);
        Assert.Contains("untracked", GuideTargetPresentation.ToggleTip(true));
        Assert.Contains("untracked", GuideTargetPresentation.ToggleTip(false));
    }

    /// <summary>HOME-006's ban and trap 73's: nothing here calls a place safe or claims to know
    /// where anything spawns.</summary>
    [Fact]
    public void NoSentenceClaimsASpawnOrCallsACampSafe()
    {
        var all = string.Join(" ", GuideTargetPresentation.PointsNote, GuideTargetPresentation.MarkTip,
            GuideTargetPresentation.PointsHere(1, 2), GuideTargetPresentation.PointsHere(0, 2),
            GuideTargetPresentation.Unmarkable(1), GuideTargetPresentation.ToggleTip(true));
        foreach (var banned in new[] { "safe", "easy", "survivable", "spawns here" })
            Assert.DoesNotContain(banned, all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not know where anything spawns", GuideTargetPresentation.PointsNote);
    }
}
