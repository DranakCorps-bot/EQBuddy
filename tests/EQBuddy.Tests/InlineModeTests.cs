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

    // QuestRooms WAS HERE, and it went with the Quests CARD on 2026-09-05 (HUD subtraction
    // cut 1). Inline mode is a question only a card asks — which rooms draw a real body
    // under the header and which get one line — and Quests has no header on the widget any
    // more. `QuestSurface.InlineModeFor` was deleted in the same commit rather than left
    // for these rows to keep asserting: a rule about a surface nobody draws passes forever
    // and reads as coverage (trap 34).
    //
    // Nothing about the Quest Tracker's rooms changed. The window and the Evolved shell's
    // Quests room draw all four in full, as they always have.

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
    /// Only Kills &amp; Drops and Gear &amp; Loot publish a `Hosted` list; Progress hosts
    /// its whole enum. Enumerating the enum is therefore the honest check for that one, and
    /// it is stricter: a tab added later is covered without anyone remembering this
    /// file.</summary>
    [Fact]
    public void NoThemeGlancesEveryTabAndEveryDefaultRoomIsAFullOne()
    {
        Assert.Equal(InlineMode.Full, ProgressSurface.InlineModeFor(ProgressSurface.DefaultInlineTab));
        Assert.Equal(InlineMode.Full, CreatureSurface.InlineModeFor(CreatureSurface.DefaultInlineTab));
        Assert.Equal(InlineMode.Full, LootSurface.InlineModeFor(LootSurface.DefaultInlineTab));
        // Quests used to be the deliberate exception here (its default room was the General
        // GLANCE). It has no card since 2026-09-05, so it has no default INLINE room to
        // check — see the note where QuestRooms used to be.

        Assert.Contains(Enum.GetValues<ProgressTab>(), t => ProgressSurface.InlineModeFor(t) == InlineMode.Full);
        Assert.Contains(CreatureSurface.Hosted, t => CreatureSurface.InlineModeFor(t) == InlineMode.Full);
        Assert.Contains(LootSurface.Hosted, t => LootSurface.InlineModeFor(t) == InlineMode.Full);
    }
}
