using EQBuddy.Core;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// The minimized bar's TRACKED QUESTS chip (Founder, 2026-09-29), through the real app: the
/// ★ puts the chip on the bar, and its panel draws the quests the player tracked — or the
/// "No quests being tracked – View Quests" empty state when there are none.
///
/// Asserted off the dump, never the screen: `hudCells` for the chip, `hudExpandBody` for
/// WHICH surface the panel drew, `hudExpandRows`/`hudExpandEmpty` for what it drew. The
/// controls themselves (Untrack, the link) are mouse gestures this suite does not drive;
/// what they call is unit-tested in `TrackedQuestsPeekTests`.
/// </summary>
public class TrackedQuestsChipTests
{
    /// <summary>A quest the shipped catalog really has — the tracked set is a list of NAMES,
    /// and a made-up one would draw as "no longer in EQBuddy's quest list" instead.</summary>
    private static string ShippedQuest { get; } =
        QuestCatalog.LoadEmbedded().Quests
            .Where(q => q.Items.Count > 0 && !q.Collection)
            .OrderBy(q => q.Name, StringComparer.Ordinal)
            .Select(q => q.Name)
            .FirstOrDefault()
        ?? throw new InvalidOperationException("the shipped quest catalog is empty");

    private static void Bar(AppSettings settings, params string[] stars)
    {
        settings.Minimized = true;
        settings.MiniStats = [.. stars];
        settings.DisabledBreakouts = ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs", "Quests"];
        settings.DefaultRulesVersion = int.MaxValue;
        settings.TrackedRules.Clear();
    }

    /// <summary>
    /// PREDICTION: one tracked quest, its panel opened through <c>EQBUDDY_HUDEXPAND</c>:
    /// body "quests", ONE row, no empty state.
    /// </summary>
    [Fact]
    public void ATrackedQuestIsARowOnTheQuestsPanel()
    {
        using var app = new AppHarness(s => Bar(s, "kills", "quests", "dps", "xp"),
            new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "quests" });
        app.SeedQuestLedger(tracked: [ShippedQuest]);
        app.Launch();

        app.WaitForDump("hudExpand", "quests", "the quests chip's panel to be the one showing");
        app.WaitForDump("hudExpandBody", "quests", "the panel's rows to be the tracked quests");
        app.WaitForDump("hudExpandRows", 1, $"one row, for {ShippedQuest}");
        app.WaitForDump("hudExpandEmpty", "none", "and no empty state beside it");
    }

    /// <summary>
    /// The other half, and the Founder's empty state: nothing tracked is a panel that SAYS so
    /// (with the link), not a blank one and not a missing chip.
    /// </summary>
    [Fact]
    public void NothingTrackedDrawsTheEmptyState()
    {
        using var app = new AppHarness(s => Bar(s, "kills", "quests", "dps", "xp"),
            new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "quests" });
        app.SeedQuestLedger(tracked: []);
        app.Launch();

        app.WaitForDump("hudExpandBody", "quests", "the quests panel to be the one showing");
        app.WaitForDump("hudExpandRows", 0, "no rows — nothing is tracked");
        app.WaitForDump("hudExpandEmpty", "empty", "the \"No quests being tracked\" line instead");
    }

    /// <summary>
    /// ALL THREE quest types (Founder smoke, 2026-09-29): a Quests-tab quest, a Plane of Sky
    /// reward (tracked by its catalog quest name) and an Epic SECTION (its own list) are each
    /// a row on the panel. PREDICTION: three rows — and the chip reads the same three.
    /// </summary>
    [Fact]
    public void AQuestASkyRewardAndAnEpicSectionAreEachARow()
    {
        var sky = QuestCatalog.LoadEmbedded().Quests
            .Select(q => q.Name)
            .First(n => SkyTestSplit.RewardKeyFor(n).Length > 0);
        using var app = new AppHarness(s => Bar(s, "kills", "quests", "dps", "xp"),
            new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "quests" });
        app.SeedQuestLedger(tracked: [ShippedQuest, sky],
            trackedSections: ["epic-warrior/the-blades"]);
        app.Launch();

        app.WaitForDump("hudExpandBody", "quests", "the quests panel to be the one showing");
        app.WaitForDump("hudExpandRows", 3, "a row for the quest, the Sky reward and the Epic section");
        app.WaitForDump("hudExpandEmpty", "none", "and no empty state beside them");
    }

    /// <summary>
    /// The ★ is what puts the chip on the bar, and a pair so the count fails in either
    /// direction (the buffs chip's precedent): the trio, kills and quests is 5; without the
    /// ★ it is 4 — even with a quest tracked, because tracking a quest from the phone must
    /// not grow a chip on a bar whose owner never asked for one.
    /// </summary>
    [Fact]
    public void TheQuestsStarPutsTheChipOnTheBar()
    {
        using var app = new AppHarness(s => Bar(s, "kills", "quests", "dps", "xp"));
        app.SeedQuestLedger(tracked: [ShippedQuest]);
        app.Launch();

        app.WaitForDump("hudCells", 5, "the trio, the kills cell and the quests chip");
    }

    /// <summary>A shipped quest with turn-in items and NO guide, so its steps are exactly its
    /// distinct turn-in items — a number this test can predict (trap 23).</summary>
    private static QuestEntry UnguidedQuest { get; } =
        QuestCatalog.LoadEmbedded().Quests
            .Where(q => q.Items.Count > 1 && !q.Collection
                && UI.Shared.GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, q.Name) is null)
            .OrderBy(q => q.Name, StringComparer.Ordinal)
            .FirstOrDefault()
        ?? throw new InvalidOperationException("no unguided multi-item quest in the shipped catalog");

    private static int StepsOf(QuestEntry q) =>
        q.Items.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>
    /// THE +/− (Founder, 2026-09-29). PREDICTION: the same tracked quest draws ZERO step lines
    /// folded and EXACTLY its distinct turn-in items unfolded — a pair, so the count fails in
    /// either direction and an "unfolded" that drew nothing cannot pass.
    /// </summary>
    [Fact]
    public void AnUnfoldedQuestDrawsEveryStepAndAFoldedOneNone()
    {
        using (var shut = new AppHarness(s => Bar(s, "kills", "quests", "dps", "xp"),
                   new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "quests" }))
        {
            shut.SeedQuestLedger(tracked: [UnguidedQuest.Name]);
            shut.Launch();
            shut.WaitForDump("hudExpandRows", 1, $"one row, for {UnguidedQuest.Name}");
            shut.WaitForDump("hudExpandSteps", 0, "folded: the row alone, no steps");
        }

        using var open = new AppHarness(s =>
            {
                Bar(s, "kills", "quests", "dps", "xp");
                s.TrackedQuestsExpanded = [$"Quest:{UnguidedQuest.Name}"];
            },
            new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "quests" });
        open.SeedQuestLedger(tracked: [UnguidedQuest.Name]);
        open.Launch();
        open.WaitForDump("hudExpandRows", 1, $"one row, for {UnguidedQuest.Name}");
        open.WaitForDump("hudExpandSteps", StepsOf(UnguidedQuest),
            $"unfolded: every turn-in item of {UnguidedQuest.Name}");
    }

    /// <summary>
    /// THE ⧉ (Founder, 2026-09-29: "pop out the mini window and move it, as we can with others
    /// on the bar"). PREDICTION: pressing the panel's ⧉ puts the Tracked quests FLOAT on
    /// screen with both tracked rows, uncapped — and its fold state is the panel's, so the
    /// unfolded quest draws the same step count there. Before this, ⧉ navigated to the Guide
    /// and `questsFloat` could not become 1.
    /// </summary>
    [Fact]
    public void ThePanelsPopOutOpensTheTrackedQuestsFloat()
    {
        using var app = new AppHarness(s =>
            {
                Bar(s, "kills", "quests", "dps", "xp");
                s.TrackedQuestsExpanded = [$"Quest:{UnguidedQuest.Name}"];
            },
            new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "quests:popout" });
        app.SeedQuestLedger(tracked: [UnguidedQuest.Name, ShippedQuest]);
        app.Launch();

        app.WaitForDump("questsFloat", 1, "the ⧉ to have popped the list out to its float");
        app.WaitForDump("questsFloatRows", 2, "both tracked quests, uncapped, on the float");
        app.WaitForDump("questsFloatSteps", StepsOf(UnguidedQuest),
            "the unfolded quest's steps, drawn on the float from the same fold list");
    }

    /// <summary>
    /// The float is NEW, so it does not open by itself. The seeded off-list is the
    /// pre-2026-09-29 one (six kinds, no "Quests"), so the positive event is the migration
    /// adding the seventh — and only after that is "no float" a claim about the gate rather
    /// than about a tick that has not run yet (trap 62).
    /// </summary>
    [Fact]
    public void TheQuestsFloatDoesNotOpenByItselfOnAnExistingProfile()
    {
        using var app = new AppHarness(s =>
        {
            Bar(s, "kills", "quests", "dps", "xp");
            // The pre-2026-09-29 profile: six kinds off, and the pass not yet run.
            s.DisabledBreakouts = ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            s.QuestsFloatDefaulted = false;
        });
        app.SeedQuestLedger(tracked: [ShippedQuest]);
        app.Launch();

        app.WaitForDump("breakoutsDisabled", 7, "the migration to have added Quests to the six seeded");
        app.WaitForDump("hudCells", 5, "the bar to be up (the trio, kills and the quests chip)");
        app.WaitForDump("questsFloat", 0, "no Tracked quests float nobody asked for");
    }

    [Fact]
    public void WithoutTheStarThereIsNoQuestsChipEvenWithAQuestTracked()
    {
        using var app = new AppHarness(s => Bar(s, "kills", "dps", "xp"));
        app.SeedQuestLedger(tracked: [ShippedQuest]);
        app.Launch();

        app.WaitForDump("hudCells", 4, "the trio and the kills cell, and no quests chip");
    }
}
