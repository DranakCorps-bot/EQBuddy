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
        settings.DisabledBreakouts = ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
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

    [Fact]
    public void WithoutTheStarThereIsNoQuestsChipEvenWithAQuestTracked()
    {
        using var app = new AppHarness(s => Bar(s, "kills", "dps", "xp"));
        app.SeedQuestLedger(tracked: [ShippedQuest]);
        app.Launch();

        app.WaitForDump("hudCells", 4, "the trio and the kills cell, and no quests chip");
    }
}
