using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The KILLS &amp; DROPS theme's vocabulary, pinned before any window exists —
/// step 1 of the recipe, the same way <see cref="LootSurfaceTests"/> pinned Gear &amp;
/// Loot's. A key renamed after a release is a saved tab choice broken after a release.</summary>
public class CreatureSurfaceTests
{
    /// <summary>Both tabs are real from the start. Gear &amp; Loot had to name four and
    /// host two, because two of its surfaces did not exist yet; here Kills is already an
    /// IWidgetCard and Drops is an existing window, so there is nothing to defer.</summary>
    [Fact]
    public void BothTabsAreHostedAndNamedForWhatTheyAnswer()
    {
        Assert.Equal([CreatureTab.Kills, CreatureTab.Drops], CreatureSurface.Hosted);
        Assert.Equal(["Kills", "Drops"], CreatureSurface.Tabs().Select(t => t.Label));
    }

    /// <summary>Not cosmetic: the fold preserves a player's card position by REUSING the
    /// Kills key, and SectionOrder / HiddenSections / MiniStats / EQBUDDY_EXPAND all speak
    /// it. Drops has never been a card, only a menu entry, which is why the theme takes
    /// the Kills slot rather than inventing one.</summary>
    [Fact]
    public void TheThemeTakesTheKillsCardSlot()
    {
        Assert.Equal("kills", CreatureSurface.KeyFor(CreatureTab.Kills));
        Assert.Equal("drops", CreatureSurface.KeyFor(CreatureTab.Drops));
        Assert.Equal(CreatureSurface.ThemeCardKey, CreatureSurface.KeyFor(CreatureTab.Kills));
        Assert.Equal(["kills"], CreatureSurface.AbsorbedCardKeys);
    }

    [Theory]
    [InlineData("kills", CreatureTab.Kills)]
    [InlineData(" DROPS ", CreatureTab.Drops)]
    // The EQBUDDY_DROPS hook and the old menu wording both still land.
    [InlineData("targetdrops", CreatureTab.Drops)]
    [InlineData("dropsbycreature", CreatureTab.Drops)]
    [InlineData("nonsense", null)]
    [InlineData(null, null)]
    public void EveryWordTheseSurfacesHaveBeenCalledStillResolves(string? key, CreatureTab? expected) =>
        Assert.Equal(expected, CreatureSurface.TabForKey(key));

    /// <summary>The launcher line replaces the Kills card's header, so it has to carry what
    /// that header carried. #219 is the release where a fold trimmed a number out of a
    /// summary and the player who used it arrived within the hour — so the rate stays.</summary>
    [Fact]
    public void TheLauncherCarriesWhatMovesWhileYouPlay()
    {
        Assert.Equal("142 kills · 38.5/hr · 9 types",
            CreatureSurface.LauncherSummary(kills: 142, killsPerHour: 38.5, creatureTypes: 9));
        // A part with nothing to say is omitted, not printed as a zero — this is the line
        // a brand new character sees, and "0 kills · 0/hr" reads as a broken widget.
        Assert.Equal("1 kill", CreatureSurface.LauncherSummary(kills: 1));
        Assert.Equal("nothing killed yet", CreatureSurface.LauncherSummary());
    }

    /// <summary>Drops LEFT Gear &amp; Loot in the same change. It sat in LootTab
    /// named-but-unhosted since that theme shipped, and it never belonged: Gear &amp; Loot
    /// is about your bags, Drops is about the mob. David's grouping made that obvious.</summary>
    [Fact]
    public void DropsIsNoLongerAGearAndLootTab()
    {
        Assert.DoesNotContain(LootSurface.Hosted, t => LootSurface.KeyFor(t) == "drops");
        // And the key routes to THIS theme now, so an old "drops" string cannot open a tab
        // that no longer exists there.
        Assert.Null(LootSurface.TabForKey("drops"));
        Assert.Equal(CreatureTab.Drops, CreatureSurface.TabForKey("drops"));
    }
}
