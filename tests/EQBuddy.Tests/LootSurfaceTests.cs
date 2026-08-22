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
        // Inventory joined on 2026-08-20 — David: "we should at least put our gear locker
        // (what we're wearing) into this window so Gear and Loot can complete a theme",
        // then "maybe we can merge Locker and Inventory into the tab Inventory". One tab,
        // two pivots, because both read the same dump. Drops and Items stay unhosted.
        Assert.Equal([LootTab.Loot, LootTab.Gear, LootTab.Inventory], LootSurface.Hosted);
        // "Wishlist", not "Gear". David, 2026-08-20: "I guess I figured Gear would show
        // me what gear I had." The KEY is still "gear" (asserted below) so no saved tab
        // choice or card position breaks — only the word the player reads changed.
        Assert.Equal(["Loot", "Wishlist", "Inventory"], LootSurface.Tabs().Select(t => t.Label));
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

    /// <summary>MOTES CAME BACK as a card on 2026-08-21 (David, answering #228 and Scribe's
    /// item), and it has to arrive HIDDEN or every player who never asked for it gets a
    /// taller widget on update — which is the opposite of the complaint.
    ///
    /// No new preference: HiddenSections IS the setting and Options is where it lives. This
    /// pins the two halves that make that work — it hides exactly once, and a player who
    /// then SHOWS the card is never quietly re-hidden on the next launch.</summary>
    [Fact]
    public void TheMotesCardComesBackHiddenAndStaysHowThePlayerLeavesIt()
    {
        var s = new AppSettings { SectionOrder = ["combat", "progress"] };

        // An existing profile: hide it, and say so, because that needs writing down.
        Assert.True(s.MigrateMotesCard(hadFile: true));
        Assert.Contains("motes", s.HiddenSections);
        Assert.True(s.MotesCardOffered);

        // The player shows it. The next launch must LEAVE IT ALONE — a migration that
        // re-hides is the "my tick-boxes won't stay ticked" shape (#169) with the app
        // itself as the second writer.
        s.HiddenSections.Remove("motes");
        Assert.False(s.MigrateMotesCard(hadFile: true));
        Assert.DoesNotContain("motes", s.HiddenSections);
    }

    /// <summary>A BRAND-NEW profile gets the same hidden card and NO write.
    ///
    /// The write is what mattered: returning true here made every fresh AppSettings.Load()
    /// a file writer, and SettingsClobberTests — which deletes settings.json and asserts
    /// nothing else touches it — began failing intermittently, a different one of its four
    /// cases each run. The state is identical either way; only the save is skipped.</summary>
    [Fact]
    public void ABrandNewProfileHidesMotesWithoutForcingASave()
    {
        var s = new AppSettings();

        Assert.False(s.MigrateMotesCard(hadFile: false));
        Assert.Contains("motes", s.HiddenSections);
        Assert.True(s.MotesCardOffered);
    }

    /// <summary>
    /// #228, Helm's ruling 2026-08-22: *"Default-off still hides existing motes... The fix
    /// is a restore change, not a reply."*
    ///
    /// A player who was watching motes lost the card to the 2026-08-19 fold and got it back
    /// switched OFF on 2026-08-21 — so from their side motes simply never came back. The
    /// evidence has to be the mini-dashboard star, because the fold removed every absorbed
    /// key from SectionOrder AND HiddenSections: "did they have the card showing" is a
    /// question their profile can no longer answer.
    /// </summary>
    [Fact]
    public void AProfileThatWasWatchingMotesGetsTheCardBackVisible()
    {
        var s = new AppSettings
        {
            SectionOrder = ["combat", "progress"],
            MiniStats = ["kills", "dps", "motes"],
        };

        Assert.True(s.MigrateMotesCard(hadFile: true));
        Assert.DoesNotContain("motes", s.HiddenSections);
    }

    /// <summary>The population that matters most: profiles that already took the blanket
    /// hide, between the card's return and this fix. <c>MotesCardOffered</c> being true is
    /// exactly what would have stopped them ever being looked at again, which is why the
    /// restore is a SECOND one-shot flag rather than a reset of the first.</summary>
    [Fact]
    public void AProfileAlreadyHiddenByTheBlanketPassIsCorrectedOnce()
    {
        var s = new AppSettings
        {
            SectionOrder = ["combat", "progress"],
            MiniStats = ["kills", "motes"],
            MotesCardOffered = true,
            HiddenSections = ["motes"],
        };

        Assert.True(s.MigrateMotesCard(hadFile: true));
        Assert.DoesNotContain("motes", s.HiddenSections);
        Assert.True(s.MotesCardRestored);

        // ONCE. The player hides it again; the next launch leaves it alone. Without this
        // the restore is the "my tick-boxes won't stay ticked" bug (#169) pointing the
        // other way — the app as the second writer, every launch.
        s.HiddenSections.Add("motes");
        Assert.False(s.MigrateMotesCard(hadFile: true));
        Assert.Contains("motes", s.HiddenSections);
    }

    /// <summary>The negatives, without which the two above would pass on a migration that
    /// simply never hides anything (trap 39: an assertion that cannot fail reads as
    /// coverage). No star means no evidence, and no evidence means the card still arrives
    /// hidden — the restore under-reaches on purpose rather than showing it to everybody.
    /// </summary>
    [Fact]
    public void WithoutTheStarTheCardStillArrivesHiddenAndTheFlagStillPersists()
    {
        var s = new AppSettings { SectionOrder = ["combat", "progress"] };

        Assert.True(s.MigrateMotesCard(hadFile: true));
        Assert.Contains("motes", s.HiddenSections);
        // The flag persists even though nothing moved. A restore that decided "no
        // evidence" and did not write that down would re-decide every launch, and would
        // then un-hide the card under a player who stars motes next week.
        Assert.True(s.MotesCardRestored);

        s.MiniStats.Add("motes");
        Assert.False(s.MigrateMotesCard(hadFile: true));
        Assert.Contains("motes", s.HiddenSections);
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
