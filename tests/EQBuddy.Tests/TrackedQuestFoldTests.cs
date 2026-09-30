using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// THE +/− ON A TRACKED QUEST (Founder, 2026-09-29): "show all the information for the
/// tracked quests but let them be expanded or collapsed with a +/-, similar to POS quests.
/// This way I can see all of steps I need to take."
///
/// Every row carries its steps — the ones its own tab draws — and the fold decides whether
/// the panel shows them. The steps are asserted against the TAB's rows (real shipped data
/// for Epic and Sky), because "the peek says what the tab says" is the whole claim (trap 4).
/// </summary>
public class TrackedQuestFoldTests : IDisposable
{
    private const string Character = "dranak_legends";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"track-fold-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.EpicQuestChecklist.AddRange(EpicQuestDefaults.Items());
        s.SkyQuestChecklist.AddRange(SkyChecklistRows.Items.Select(i => i.Clone()));
        return s;
    }

    private static readonly Dictionary<string, int> NoneDone = new(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Set(params string[] items) => new(items, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, QuestLedgerStore.Entry> Owned(params (string Item, int N)[] items)
        => items.ToDictionary(i => i.Item, i => new QuestLedgerStore.Entry { Looted = i.N },
            StringComparer.OrdinalIgnoreCase);

    private static QuestCatalog Catalog() => new()
    {
        Quests =
        [
            new QuestEntry { Name = "Bone Ritual", Items =
                [new QuestItemNeed { Name = "Bone Chips", Qty = 2 },
                 new QuestItemNeed { Name = "Gnoll Fang", Qty = 3 }] },
            new QuestEntry { Name = "Words of the Wise", StartZone = "Qeynos" },
        ],
    };

    // ---- the steps ARE the tab's ----

    /// <summary>
    /// PREDICTION: The Blades with its first two steps ticked has EXACTLY the rows the Epic tab
    /// draws under that heading as its steps, in that order, the first two Done and the rest
    /// Open — and no heading repeats, because a section is one heading.
    /// </summary>
    [Fact]
    public void AnEpicSectionsStepsAreTheRowsTheEpicTabDrawsUnderIt()
    {
        var settings = Settings();
        var stage = GuideCatalog.Default.Find("epic-warrior")!.Stages.Single(s => s.Id == "the-blades");
        foreach (var step in stage.Objectives.Take(2))
            settings.EpicQuestChecklist.Single(i => i.Id == step.Id).Acquired = true;
        var groups = ChecklistGroups.Epic(settings, Store(), Character, ChecklistGroups.EpicRows(settings));
        var drawn = EpicSection.Rows(groups.Single(g => g.GuideId == "epic-warrior"), stage);

        var row = Assert.Single(TrackedQuestsPeek.Build(QuestCatalog.LoadEmbedded(), Owned(), Set(), NoneDone,
            trackedSections: ["epic-warrior/the-blades"], epicGroups: groups).Rows);

        Assert.Equal(drawn.Select(r => r.Title), row.Steps.Select(s => s.Title));
        Assert.Equal([TrackedStepState.Done, TrackedStepState.Done],
            row.Steps.Take(2).Select(s => s.State));
        Assert.All(row.Steps.Skip(2), s => Assert.Equal(TrackedStepState.Open, s.State));
        Assert.All(Enumerable.Range(0, row.Steps.Count),
            i => Assert.False(TrackedQuestsPeek.HeadingBefore(row.Steps, i)));
    }

    /// <summary>A Plane of Sky reward's steps are its group's rows — the Sky tab's pieces.
    /// </summary>
    [Fact]
    public void ASkyRewardsStepsAreItsGroupsRows()
    {
        var settings = Settings();
        var catalog = QuestCatalog.LoadEmbedded();
        var skyGroups = ChecklistGroups.Sky(settings, Store(), Character);
        var group = skyGroups.First(g => g.CompletionKey is { } k
            && catalog.Quests.Any(q => q.Name == SkyTestSplit.QuestNameFor(k)));

        var row = Assert.Single(TrackedQuestsPeek.Build(catalog, Owned(),
            Set(SkyTestSplit.QuestNameFor(group.CompletionKey!)), NoneDone, skyGroups: skyGroups).Rows);

        Assert.Equal(TrackedKind.Sky, row.Kind);
        Assert.Equal(group.Rows.DistinctBy(r => r.Id).Select(r => r.Title), row.Steps.Select(s => s.Title));
        Assert.NotEmpty(row.Steps);
    }

    /// <summary>
    /// An UNGUIDED Quests-tab quest's steps are its turn-in items with have/need — Done only
    /// once the bags hold ENOUGH (2 of 3 fangs is still open). A dialogue quest with no items
    /// and no guide has nothing to unfold, and so no +.
    /// </summary>
    [Fact]
    public void AnUnguidedQuestsStepsAreItsTurnInsWithHaveAndNeed()
    {
        var body = TrackedQuestsPeek.Build(Catalog(), Owned(("Bone Chips", 5), ("Gnoll Fang", 2)),
            Set("Bone Ritual", "Words of the Wise"), NoneDone);

        var ritual = body.Rows.Single(r => r.Name == "Bone Ritual");
        Assert.Equal(
            [new TrackedStep("Bone Chips 2/2", TrackedStepState.Done),
             new TrackedStep("Gnoll Fang 2/3", TrackedStepState.Open)],
            ritual.Steps);
        Assert.Empty(body.Rows.Single(r => r.Name == "Words of the Wise").Steps);
    }

    /// <summary>
    /// A GUIDED Quests-tab quest's steps are its guide's objectives — the detail pane's
    /// projection, handed in — with their stage headings, and the turn-in list is NOT added
    /// beside them (the pane's guide absorbs it). A skipped objective reads Skipped.
    /// </summary>
    [Fact]
    public void AGuidedQuestsStepsAreItsGuidesObjectivesWithTheirStages()
    {
        QuestChecklistGroup Guide(QuestEntry q) => new("", q.Name,
        [
            new QuestChecklistRow("a", "", "Talk to Retlon", "", Acquired: true, Unassigned: false,
                IslandHeading: "Start"),
            new QuestChecklistRow("b", "", "Collect 2 Bone Chips", "", Acquired: false, Unassigned: false,
                IslandHeading: "Turn-in pieces"),
            new QuestChecklistRow("c", "", "Optional detour", "", Acquired: false, Unassigned: false,
                IslandHeading: "Turn-in pieces", IsSkipped: true),
        ]);

        var row = TrackedQuestsPeek.Build(Catalog(), Owned(), Set("Bone Ritual"), NoneDone,
            questGuide: Guide).Rows.Single();

        Assert.Equal(["Talk to Retlon", "Collect 2 Bone Chips", "Optional detour"], row.Steps.Select(s => s.Title));
        Assert.Equal([TrackedStepState.Done, TrackedStepState.Open, TrackedStepState.Skipped],
            row.Steps.Select(s => s.State));
        // Two stages, so each gets its heading once, where it starts.
        Assert.Equal([true, true, false],
            Enumerable.Range(0, row.Steps.Count).Select(i => TrackedQuestsPeek.HeadingBefore(row.Steps, i)));
    }

    // ---- the fold ----

    /// <summary>
    /// A row starts FOLDED (the GuideExpanded idiom: stored as the expanded exception) and
    /// opens when its fold key is in the list. The keys are per LIST: a quest and an Epic
    /// section can never share one.
    /// </summary>
    [Fact]
    public void ARowStartsFoldedAndOpensByItsOwnKey()
    {
        var shut = TrackedQuestsPeek.Build(Catalog(), Owned(), Set("Bone Ritual"), NoneDone).Rows.Single();
        Assert.False(shut.Expanded);
        Assert.Equal("Quest:Bone Ritual", shut.FoldKey);

        var open = TrackedQuestsPeek.Build(Catalog(), Owned(), Set("Bone Ritual"), NoneDone,
            expanded: ["quest:bone ritual"]).Rows.Single();
        Assert.True(open.Expanded);

        var other = TrackedQuestsPeek.Build(Catalog(), Owned(), Set("Bone Ritual"), NoneDone,
            expanded: ["EpicSection:Bone Ritual"]).Rows.Single();
        Assert.False(other.Expanded);
    }

    /// <summary>A row with nothing to unfold never reads open, whatever the list says — a "−"
    /// over nothing is a control that does nothing.</summary>
    [Fact]
    public void ARowWithNoStepsNeverReadsOpen()
    {
        var row = TrackedQuestsPeek.Build(Catalog(), Owned(), Set("Words of the Wise"), NoneDone,
            expanded: ["Quest:Words of the Wise"]).Rows.Single();

        Assert.Empty(row.Steps);
        Assert.False(row.Expanded);
    }

    /// <summary>The one write: open, shut, open — and case-insensitive, so a key the list
    /// holds in another case is shut, not doubled.</summary>
    [Fact]
    public void ToggleFoldOpensAndShutsOneKey()
    {
        var list = new List<string>();

        Assert.True(TrackedQuestsPeek.ToggleFold(list, "Quest:Bone Ritual"));
        Assert.Equal(["Quest:Bone Ritual"], list);
        Assert.False(TrackedQuestsPeek.ToggleFold(list, "quest:BONE RITUAL"));
        Assert.Empty(list);
    }

    /// <summary>
    /// The repaint gate sees the fold AND the steps (trap 72): opening a fold in the float must
    /// repaint the peek, and a step ticked on the tab must repaint an open row even when the
    /// badge's count cannot move (a swap — one ticked, one unticked).
    /// </summary>
    [Fact]
    public void TheSignatureMovesWithTheFoldAndWithAStepSwap()
    {
        var settings = Settings();
        var stage = GuideCatalog.Default.Find("epic-warrior")!.Stages.Single(s => s.Id == "the-blades");
        var ids = stage.Objectives.Select(o => o.Id).ToList();
        TrackedQuestsBody Body(int ticked, bool open)
        {
            foreach (var item in settings.EpicQuestChecklist.Where(i => ids.Contains(i.Id)))
                item.Acquired = item.Id == ids[ticked];
            var groups = ChecklistGroups.Epic(settings, Store(), Character, ChecklistGroups.EpicRows(settings));
            return TrackedQuestsPeek.Build(QuestCatalog.LoadEmbedded(), Owned(), Set(), NoneDone,
                trackedSections: ["epic-warrior/the-blades"], epicGroups: groups,
                expanded: open ? ["EpicSection:epic-warrior/the-blades"] : null);
        }

        var first = Body(0, open: false);
        var second = Body(1, open: false);
        Assert.Equal(first.Rows[0].Badge, second.Rows[0].Badge);   // the premise: count unmoved
        Assert.NotEqual(first.Signature, second.Signature);
        Assert.NotEqual(first.Signature, Body(0, open: true).Signature);
        Assert.Equal(first.Signature, Body(0, open: false).Signature);
    }

    // ---- the float arrives unpinned ----

    /// <summary>
    /// The Tracked quests float is NEW, so it must not appear by itself on the next minimise:
    /// a fresh profile has it in the default off-list, an existing profile gets it added ONCE,
    /// and a player who pins it afterwards keeps it pinned (the migration never runs again).
    /// </summary>
    [Fact]
    public void TheQuestsFloatArrivesUnpinnedOnceAndThePinWinsAfter()
    {
        var fresh = new AppSettings();
        Assert.Contains("Quests", fresh.DisabledBreakouts);
        Assert.False(BreakoutAutoOpen.IsOn(fresh, "Quests"));

        var existing = new AppSettings { DisabledBreakouts = ["Healing"] };
        Assert.True(existing.MigrateQuestsFloatOff(hadFile: true));
        Assert.Equal(["Healing", "Quests"], existing.DisabledBreakouts);

        BreakoutAutoOpen.Set(existing, "Quests", on: true);
        Assert.False(existing.MigrateQuestsFloatOff(hadFile: true));
        Assert.True(BreakoutAutoOpen.IsOn(existing, "Quests"));
    }
}
