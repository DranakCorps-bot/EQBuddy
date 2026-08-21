using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The Loot &amp; Items theme's Core definition and its settings fold — steps 1 and 5 of
/// docs/Themes.md's recipe, tested before any UI exists to draw them.
///
/// The fold is where a consolidation loses things silently, which is why it gets more
/// tests than the tab strip does. The plan says so in as many words: *"do not consolidate
/// on the way to a release — each of these touches settings migration, and the
/// fold-the-old-keys step is where silent data loss lives."*
/// </summary>
public class LootSurfaceTests
{
    // ---- step 1: the tabs ----

    [Fact]
    public void OnlyTabsWithSomethingBehindThemAreHosted()
    {
        // Drops, Items, Locker and Bags are named in the enum so the vocabulary is
        // settled once, but they are still separate windows — a tab with nothing behind
        // it reads as broken rather than as not-yet-arrived.
        // Locker joined on 2026-08-20 — David: "we should at least put our gear locker
        // (what we're wearing) into this window so Gear and Loot can complete a theme."
        // Bags, Drops and Items are still named-but-unhosted.
        Assert.Equal([LootTab.Loot, LootTab.Gear, LootTab.Locker], LootSurface.Hosted);
        // "Wishlist", not "Gear". David, 2026-08-20: "I guess I figured Gear would show
        // me what gear I had." The KEY is still "gear" (asserted below) so no saved tab
        // choice or card position breaks — only the word the player reads changed.
        Assert.Equal(["Loot", "Wishlist", "Locker"], LootSurface.Tabs().Select(t => t.Label));
    }

    [Fact]
    public void TabKeysAreTheCardKeysTheyReplace()
    {
        // Not cosmetic: the fold below preserves a player's card position by REUSING one
        // of these, and EQBUDDY_EXPAND / HiddenSections / SectionOrder all speak them.
        Assert.Equal("loot", LootSurface.KeyFor(LootTab.Loot));
        Assert.Equal("gear", LootSurface.KeyFor(LootTab.Gear));
        Assert.Equal(LootSurface.ThemeCardKey, LootSurface.KeyFor(LootTab.Loot));
    }

    [Theory]
    [InlineData("loot", LootTab.Loot)]
    [InlineData("session", LootTab.Loot)]
    [InlineData("GEAR", LootTab.Gear)]
    [InlineData(" drops ", LootTab.Drops)]
    [InlineData("lookup", LootTab.Items)]
    public void ASavedTabChoiceResolves(string key, LootTab expected) =>
        Assert.Equal(expected, LootSurface.TabForKey(key));

    [Fact]
    public void AnUnknownTabChoiceResolvesToNothingRatherThanGuessing() =>
        Assert.Null(LootSurface.TabForKey("motes"));

    // ---- step 3: the launcher line ----

    [Fact]
    public void TheLauncherCarriesWhatMovesWhileYouPlay()
    {
        Assert.Equal("39 items · +4 made · gear 3/12",
            LootSurface.LauncherSummary(items: 39, crafted: 4, gearTotal: 12, gearAcquired: 3));
    }

    /// <summary>A part with nothing to say is omitted, not printed as a zero — otherwise a
    /// fresh character reads three zeros, and a fresh character is exactly who is looking
    /// at a fresh widget.</summary>
    [Fact]
    public void EmptyPartsAreOmittedAndAnEmptyLineStillSaysSomething()
    {
        Assert.Equal("12 items", LootSurface.LauncherSummary(items: 12));
        Assert.Equal("gear 0/8", LootSurface.LauncherSummary(gearTotal: 8));
        Assert.Equal("nothing looted yet", LootSurface.LauncherSummary());
    }

    [Fact]
    public void OneItemIsNotOneItems() =>
        Assert.Equal("1 item", LootSurface.LauncherSummary(items: 1));

    // ---- step 5: the fold ----
    //
    // Written and tested here, and deliberately NOT yet called from AppSettings.Load:
    // the window it folds into does not exist, so running it today would move a player's
    // Gear card out of the slot they put it in and append it at the bottom in exchange
    // for nothing. Cards missing from SectionOrder are appended rather than hidden, so
    // this would not have LOST the card — it would just have quietly rearranged the
    // widget for everyone, one release early.

    [Fact]
    public void TheThemeTakesTheFirstSlotEitherCardOccupied()
    {
        var s = new AppSettings { SectionOrder = ["combat", "gear", "kills", "loot", "buffs"] };

        Assert.True(s.MigrateLootSections());

        // Gear was ahead of loot, so the theme lands where GEAR was — a player who
        // dragged one of these up keeps the position they chose.
        Assert.Equal(["combat", "loot", "kills", "buffs"], s.SectionOrder);
    }

    [Fact]
    public void TheThemeIsHiddenOnlyIfBothCardsWere()
    {
        var both = new AppSettings
        {
            SectionOrder = ["loot", "gear"],
            HiddenSections = ["loot", "gear"],
        };
        Assert.True(both.MigrateLootSections());
        Assert.Contains("loot", both.HiddenSections);

        // One of two hidden: showing a card someone hid is one click to undo; hiding one
        // they wanted is invisible and they would have to suspect the update to find it.
        var one = new AppSettings
        {
            SectionOrder = ["loot", "gear"],
            HiddenSections = ["gear"],
        };
        Assert.True(one.MigrateLootSections());
        Assert.DoesNotContain("loot", one.HiddenSections);
        Assert.DoesNotContain("gear", one.HiddenSections);
    }

    /// <summary>The idempotence trap, and it is not tidiness. The theme key is itself one
    /// of the absorbed keys, so a fold that does not check first reports a change on every
    /// load — which forces a settings SAVE each launch, and a save rewrites the whole file
    /// from the startup snapshot (trap 13).</summary>
    [Fact]
    public void AnAlreadyFoldedProfileReportsNoChange()
    {
        var s = new AppSettings { SectionOrder = ["combat", "loot", "kills"] };

        Assert.False(s.MigrateLootSections());
        Assert.False(s.MigrateLootSections());
        Assert.Equal(["combat", "loot", "kills"], s.SectionOrder);
    }

    /// <summary>A profile that never had these cards does not acquire a hidden one.</summary>
    [Fact]
    public void AProfileWithNeitherCardIsLeftAlone()
    {
        var s = new AppSettings { SectionOrder = ["combat", "kills"] };

        Assert.False(s.MigrateLootSections());
        Assert.Equal(["combat", "kills"], s.SectionOrder);
        Assert.Empty(s.HiddenSections);
    }

    /// <summary>The two folds share one implementation now, so the Progress theme has to
    /// keep behaving exactly as it did — its own tests cover it, and this is the reminder
    /// that they are testing shared code.</summary>
    [Fact]
    public void TheProgressFoldStillFoldsItsOwnFiveCards()
    {
        var s = new AppSettings { SectionOrder = ["combat", "money", "loot", "faction"] };

        Assert.True(s.MigrateProgressSections());

        Assert.Equal(["combat", "progress", "loot"], s.SectionOrder);
    }
}
