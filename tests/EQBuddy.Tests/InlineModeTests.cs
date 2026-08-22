using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Which of a theme's tabs draw their real body inline on the widget, and which glance.
///
/// Bevel's ruling, Helm-signed 2026-08-22, and it lives in Core so that moving a tab
/// between the two is ONE line both desktops follow. These tests are the record of the
/// ruling as much as a guard: if a future change moves a tab, the failure names the tab
/// and whoever moved it has to say why.
/// </summary>
public class InlineModeTests
{
    [Theory]
    [InlineData(ProgressTab.Experience, InlineMode.Full)]
    [InlineData(ProgressTab.Wealth, InlineMode.Full)]     // coin only — Helm's correction
    [InlineData(ProgressTab.Faction, InlineMode.Full)]
    [InlineData(ProgressTab.Raids, InlineMode.Glance)]    // a cleared/total ledger reads as a line
    public void ProgressRooms(ProgressTab tab, InlineMode mode) =>
        Assert.Equal(mode, ProgressSurface.InlineModeFor(tab));

    [Theory]
    [InlineData(QuestTab.Epic, InlineMode.Full)]
    [InlineData(QuestTab.Sky, InlineMode.Full)]
    [InlineData(QuestTab.General, InlineMode.Glance)]     // search over 1,200 + a detail pane
    public void QuestRooms(QuestTab tab, InlineMode mode) =>
        Assert.Equal(mode, QuestSurface.InlineModeFor(tab));

    [Theory]
    [InlineData(CreatureTab.Kills, InlineMode.Full)]
    [InlineData(CreatureTab.Drops, InlineMode.Glance)]    // tallest body in the set, and it fetches
    public void CreatureRooms(CreatureTab tab, InlineMode mode) =>
        Assert.Equal(mode, CreatureSurface.InlineModeFor(tab));

    [Theory]
    [InlineData(LootTab.Loot, InlineMode.Full)]
    [InlineData(LootTab.Gear, InlineMode.Full)]           // "Wishlist"
    [InlineData(LootTab.Inventory, InlineMode.Glance)]    // long list with its own filter bar
    public void LootRooms(LootTab tab, InlineMode mode) =>
        Assert.Equal(mode, LootSurface.InlineModeFor(tab));

    /// <summary>No theme may glance EVERY tab — that would be an expander with nothing
    /// behind it — and every default room must be one the theme draws in FULL, or clicking
    /// a card would expand it onto a single line.
    ///
    /// Only Kills &amp; Drops and Gear &amp; Loot publish a `Hosted` list; Progress and
    /// Quests host their whole enum. Enumerating the enum is therefore the honest check for
    /// those two, and it is stricter: a tab added later is covered without anyone
    /// remembering this file.</summary>
    [Fact]
    public void NoThemeGlancesEveryTabAndEveryDefaultRoomIsAFullOne()
    {
        Assert.Equal(InlineMode.Full, ProgressSurface.InlineModeFor(ProgressSurface.DefaultInlineTab));
        Assert.Equal(InlineMode.Full, CreatureSurface.InlineModeFor(CreatureSurface.DefaultInlineTab));
        Assert.Equal(InlineMode.Full, LootSurface.InlineModeFor(LootSurface.DefaultInlineTab));
        // Quests is the deliberate exception, and it is worth stating rather than hiding:
        // its default room is the General GLANCE, because "3 quests ready to turn in" is
        // the thing a player expands that card to learn (Bevel, Helm-signed 2026-08-22).
        Assert.Equal(InlineMode.Glance, QuestSurface.InlineModeFor(QuestSurface.DefaultInlineTab));

        Assert.Contains(Enum.GetValues<ProgressTab>(), t => ProgressSurface.InlineModeFor(t) == InlineMode.Full);
        Assert.Contains(Enum.GetValues<QuestTab>(), t => QuestSurface.InlineModeFor(t) == InlineMode.Full);
        Assert.Contains(CreatureSurface.Hosted, t => CreatureSurface.InlineModeFor(t) == InlineMode.Full);
        Assert.Contains(LootSurface.Hosted, t => LootSurface.InlineModeFor(t) == InlineMode.Full);
    }
}
