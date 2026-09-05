using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

public sealed class OptionsViewModelTests
{
    private static (OptionsViewModel Vm, AppSettings Settings, Counter Persists) Create(AppSettings? settings = null)
    {
        var s = settings ?? new AppSettings();
        var counter = new Counter();
        return (new OptionsViewModel(s, () => counter.Value++), s, counter);
    }

    private sealed class Counter { public int Value; }

    [Fact]
    public void RecentWindowIndexRoundTrips()
    {
        var (vm, s, persists) = Create();
        Assert.Equal(1, vm.RecentWindowIndex);          // default 15 min
        vm.RecentWindowIndex = 0;
        Assert.Equal(5, s.RecentWindowMinutes);
        vm.RecentWindowIndex = 2;
        Assert.Equal(30, s.RecentWindowMinutes);
        Assert.Equal(2, persists.Value);
    }

    [Fact]
    public void SoundSelectionHandlesLegacyNamesAndCustomPaths()
    {
        // Index 0 is now the "(Disabled)" slot, so built-ins sit one higher and the custom
        // slot is Names.Length + 1.
        var (vm, s, _) = Create(new AppSettings { AlertSound = "Question" });
        Assert.Equal(Array.IndexOf(AlertSoundCatalog.Names, "Notify") + 1, vm.SoundIndex);   // legacy maps
        Assert.Equal("", vm.SoundFileNote);

        vm.SetCustomSound(@"C:\sounds\gong.wav");
        Assert.Equal(AlertSoundCatalog.Names.Length + 1, vm.SoundIndex);                     // custom slot
        Assert.Contains("gong.wav", vm.SoundFileNote);

        vm.SelectNamedSound(1);                                                              // first built-in
        Assert.Equal(AlertSoundCatalog.Names[0], s.AlertSound);
        Assert.True(vm.IsCustomSoundIndex(AlertSoundCatalog.Names.Length + 1));
    }

    [Fact]
    public void DisabledSoundChoiceSilencesAlerts()
    {
        var (vm, s, _) = Create(new AppSettings { AlertSound = "Ding" });

        vm.SelectNamedSound(0);   // the "(Disabled)" slot
        Assert.Equal(AlertSoundCatalog.OffChoice, s.AlertSound);
        Assert.Equal(0, vm.SoundIndex);
        Assert.True(vm.IsDisabledSoundIndex(0));

        // And a settings.json already holding "Off" lands back on the Disabled slot.
        var (vm2, _, _) = Create(new AppSettings { AlertSound = "Off" });
        Assert.Equal(0, vm2.SoundIndex);
    }

    [Fact]
    public void CardsNormalizeMoveAndToggle()
    {
        var settings = new AppSettings { SectionOrder = ["kills", "bogus"] };
        var (vm, s, _) = Create(settings);

        // Unknown keys dropped, missing keys appended in default order, kills stays first.
        Assert.Equal("kills", s.SectionOrder[0]);
        Assert.Equal(OverlaySections.Catalog.Length, s.SectionOrder.Count);
        Assert.DoesNotContain("bogus", s.SectionOrder);
        // QUESTS IS NOT A CARD ANY MORE (HUD subtraction cut 1, 2026-09-05) — the first v1
        // card retired to the Evolved shell. It is asserted ABSENT rather than simply not
        // mentioned, because Options is the only place a player can see the card list and
        // a key with no catalog row throws in `Cards`'s own First(...). The two cards it
        // had itself replaced went with it, as they already had.
        Assert.DoesNotContain(vm.Cards, c => c.Key is "quests" or "sky" or "epic");
        // And the one "loot" replaced — the GEAR & LOOT theme (docs/Themes.md). The card
        // KEEPS the "loot" key and takes a new title: a player who dragged Loot somewhere
        // keeps that slot through the fold (LootSurface.ThemeCardKey).
        Assert.Contains(vm.Cards, c => c.Key == "loot" && c.Title == "Gear & Loot");
        Assert.DoesNotContain(vm.Cards, c => c.Key == "gear");
        // And the four "progress" replaced, the same way — the PROGRESS THEME
        // (docs/Themes.md). Options is the only place a player can see the card list, so
        // a folded key left in it would offer a card that no longer exists.
        Assert.Contains(vm.Cards, c => c.Key == "progress" && c.Title == "Progress");
        Assert.DoesNotContain(vm.Cards, c => c.Key is "money" or "faction" or "raids");
        // MOTES IS THE EXCEPTION, and it came BACK on 2026-08-21 (David, answering #228 and
        // Scribe's item). It is a card again and therefore listed here again - which is the
        // whole point: Options is the only place a player can see the card list, so a card
        // that exists and is not listed is a card that cannot be switched on.
        //
        // It arrives HIDDEN for existing profiles (AppSettings.MigrateMotesCard), so being
        // in this list is exactly the difference between "off" and "gone".
        Assert.Contains(vm.Cards, c => c.Key == "motes" && c.Title == "Motes");
        // WORLD IS NOT A CARD ANY MORE either (HUD subtraction cut 2, 2026-09-05), and the
        // key it leaves behind is "misc" — the old Travels & Deaths card's own settings
        // key, which every 1.x profile carries. Asserted ABSENT for the same reason as
        // "quests": a key with no catalog row throws in `Cards`'s own First(...).
        Assert.DoesNotContain(vm.Cards, c => c.Key == "misc");

        vm.MoveCard("kills", -1);                        // top can't move up
        Assert.Equal("kills", s.SectionOrder[0]);
        vm.MoveCard("kills", +1);
        Assert.Equal("kills", s.SectionOrder[1]);

        vm.ToggleCard("progress");
        Assert.True(vm.Cards.Single(c => c.Key == "progress").Hidden);
        vm.ToggleCard("progress");
        Assert.False(vm.Cards.Single(c => c.Key == "progress").Hidden);

        // A folded card says where its cards went, ON THIS SCREEN — #219
        // (typical-usual-chaos) went to Options looking for Motes, found no row, and
        // reasonably concluded the feature had been deleted. A fold is invisible by
        // construction: the row that would explain it is the row that was removed.
        var progress = vm.Cards.Single(c => c.Key == "progress").Absorbed;
        Assert.NotNull(progress);
        // By the words a player is scanning for, not by key.
        foreach (var gone in new[] { "Money", "Faction", "Raids" })
            Assert.Contains(gone, progress);
        // MOTES IS NOT IN THAT SENTENCE ANY MORE, and this is the assertion for why: it
        // came back as a card on 2026-08-21, so it is two rows above this note in the same
        // list. The note answers "where did the card I am hunting for go" — naming a card
        // that is right there sends someone into the window looking for it, which is worse
        // than saying nothing. It is still a Wealth-tab block too; being in two places is
        // not the same as having moved.
        Assert.DoesNotContain("Motes", progress);
        // The "Sky Quest · Epics are tabs in here now" note went with the Quests card on
        // 2026-09-05 — a note is keyed by the SURVIVING card, and there is none. That the
        // three names now have no row on this screen at all is the gap this cut leaves
        // knowingly; it is written into HELM-FEEDBACK.md rather than faked here.
        // The GEAR & LOOT theme absorbs exactly ONE card, which is the case the sentence
        // was never written for: it read "Gear are tabs in here now" until 2026-08-20.
        // A line whose whole job is to be read by someone hunting a vanished card cannot
        // afford to look wrong.
        Assert.Equal("Gear is a tab in here now",
            vm.Cards.Single(c => c.Key == "loot").Absorbed);
        // A card that never absorbed anything still says nothing.
        Assert.Null(vm.Cards.Single(c => c.Key == "combat").Absorbed);
    }

    [Fact]
    public void RulesAddAndRemovePersist()
    {
        var (vm, s, persists) = Create();
        var rule = vm.AddRule();
        Assert.Single(s.TrackedRules);
        vm.RemoveRule(rule);
        Assert.Empty(s.TrackedRules);
        Assert.Equal(2, persists.Value);
    }

    [Fact]
    public void SliderLabelsAndClamping()
    {
        var (vm, s, _) = Create();
        vm.UiScale = 9;                                  // clamps
        Assert.Equal(2.0, s.UiScale);
        Assert.Equal("200%", vm.ScaleLabel);
        vm.BackgroundOpacity = 0.0;
        Assert.Equal(0.15, s.BackgroundOpacity, 3);
        Assert.Equal(1.0, s.ChipScale);                  // default: chips at 100%
        vm.ChipScale = 9;                                // clamps like UiScale
        Assert.Equal(2.0, s.ChipScale);
        Assert.Equal("200%", vm.ChipScaleLabel);
        vm.ChipScale = 0.1;
        Assert.Equal(0.5, s.ChipScale);
    }

    [Fact]
    public void QuestSectionsLeaveTheWidgetEntirely()
    {
        // This test used to be SkyQuestSectionSlotsInAfterMotes and asserted a FOLD: sky +
        // epic became "quests", in the earlier of their two slots. On 2026-09-05 the Quests
        // card itself left OverlaySections.Catalog (HUD subtraction cut 1), so all three
        // keys are now keys that can never draw anything and the migration removes them.
        //
        // Removing "quests" is the half that matters, and it is not tidiness: EVERY 1.x
        // profile carries that key in SectionOrder. A key with no catalog row is exactly
        // what #252 was made of — OptionsViewModel.Cards looks each one up with First(...),
        // and a fold that is handed a phantom key chews on it every launch.
        var settings = new AppSettings
        {
            SectionOrder = ["combat", "motes", "sky", "gear", "epic", "tracked", "bogus"],
            HiddenSections = ["loot"],
        };
        Assert.True(settings.MigrateQuestSections());
        Assert.Equal(["combat", "motes", "gear", "tracked", "bogus"], settings.SectionOrder);
        Assert.Equal(["loot"], settings.HiddenSections);
        Assert.False(settings.MigrateQuestSections());   // idempotent

        // The ordinary 1.99.x profile: the folded card, present and possibly hidden. Both
        // lists lose it, and the second launch reports no change — which is what keeps
        // Load from rewriting settings.json on every start (trap 13).
        var folded = new AppSettings
        {
            SectionOrder = ["combat", "quests", "tracked"],
            HiddenSections = ["quests"],
        };
        Assert.True(folded.MigrateQuestSections());
        Assert.Equal(["combat", "tracked"], folded.SectionOrder);
        Assert.Empty(folded.HiddenSections);
        Assert.False(folded.MigrateQuestSections());

        // Unknown-key cleanup is still the UI layer's job (CardsNormalizeMoveAndToggle
        // above), so Core never carries a section catalog copy: "bogus" and "gear" above
        // pass through untouched, and only the three quest keys are named here.
        var hidBoth = new AppSettings
        {
            SectionOrder = ["combat", "sky", "epic"],
            HiddenSections = ["sky", "epic"],
        };
        Assert.True(hidBoth.MigrateQuestSections());
        Assert.Equal(["combat"], hidBoth.SectionOrder);
        Assert.Empty(hidBoth.HiddenSections);

        // A fresh install's empty order stays empty — the UI appends the catalog.
        Assert.False(new AppSettings().MigrateQuestSections());
    }

    /// <summary>
    /// HUD subtraction cut 2 (2026-09-05): the World card leaves, and its key goes with it.
    ///
    /// **"misc" is not a 1.99-era key — it is the oldest one in the file.** The card has
    /// been in `SectionOrder` under that name since before it was called World (it was
    /// Travels &amp; Deaths, and `misc` is a name from before it had a proper one), and the
    /// World fold kept the key precisely so nobody's slot moved. So this migration runs on
    /// essentially every profile in existence, and it has to clear BOTH lists: a key left
    /// in `HiddenSections` with no catalog row is exactly what #252 was made of.
    ///
    /// The other half is the one a subtraction can get wrong quietly — that it removes only
    /// what it names. `map`, `spawns` and `travel` were never cards (they were menu-only
    /// windows), so nothing about them is in these lists to remove; `motes` sits beside
    /// `misc` here to prove a neighbour survives.
    /// </summary>
    [Fact]
    public void TheWorldCardsKeyLeavesTheWidgetEntirely()
    {
        var settings = new AppSettings
        {
            SectionOrder = ["combat", "misc", "motes", "tracked", "bogus"],
            HiddenSections = ["misc", "motes"],
        };

        Assert.True(settings.MigrateWorldSections());
        Assert.Equal(["combat", "motes", "tracked", "bogus"], settings.SectionOrder);
        Assert.Equal(["motes"], settings.HiddenSections);
        Assert.False(settings.MigrateWorldSections());   // idempotent — trap 13

        // A profile that had the card visible loses the key just the same: the question the
        // removal answers is "is this a card", not "did you hide it".
        var visible = new AppSettings { SectionOrder = ["combat", "misc"] };
        Assert.True(visible.MigrateWorldSections());
        Assert.Equal(["combat"], visible.SectionOrder);

        // And a fresh install's empty order stays empty.
        Assert.False(new AppSettings().MigrateWorldSections());
    }

    [Fact]
    public void SkyQuestDefaultsMergeOnce()
    {
        var settings = new AppSettings();

        Assert.True(settings.ApplyDefaultSkyQuestChecklist());
        Assert.Contains(settings.SkyQuestChecklist, i => i.ClassName == "Monk" && i.Reward == "Wu's Fist of Mastery");
        Assert.Contains(settings.SkyQuestChecklist, i => i.ClassName == "Shaman" && i.QuestItem == "Efreeti War Club");
        Assert.Contains(settings.SkyQuestChecklist, i => i.ClassName == "Shadow Knight" && i.Reward == "Pearlescent Pauldrons");
        Assert.Contains(settings.SkyQuestChecklist, i => i.ClassName == "Shadow Knight" && i.Npc == "Sarkis Ebonblade");
        Assert.All(settings.SkyQuestChecklist.GroupBy(i => i.ClassName),
            classGroup => Assert.Contains(classGroup, i => i.Npc.Length > 0));
        var count = settings.SkyQuestChecklist.Count;

        settings.SkyQuestChecklist[0].Acquired = true;
        Assert.False(settings.ApplyDefaultSkyQuestChecklist());
        Assert.Equal(count, settings.SkyQuestChecklist.Count);
        Assert.True(settings.SkyQuestChecklist[0].Acquired);
    }

    [Fact]
    public void BardMaskAndMantleMatchTheWiki()
    {
        // Crossed in v1.79.0 on a first-hand report (#139), reported wrong again by a
        // second player (#150), and now aligned with the wiki per David's standing rule:
        // when a conflict cannot be resolved, match the community's own reference. Being
        // wrong the same way is recoverable; being uniquely wrong is what costs trust.
        // Sources travel with the ITEMS, not the quests.
        var settings = new AppSettings();
        settings.ApplyDefaultSkyQuestChecklist();

        var mask = settings.SkyQuestChecklist.Single(
            i => i.ClassName == "Bard" && i.Reward == "Mask of Song" && i.QuestItem.StartsWith("Light Woolen"));
        Assert.Equal("Light Woolen Mask", mask.QuestItem);
        Assert.Equal("Isle 3: Gorgalosk", mask.Source);

        var mantle = settings.SkyQuestChecklist.Single(
            i => i.ClassName == "Bard" && i.Reward == "Mantle of the Songweaver" && i.QuestItem.StartsWith("Light Woolen"));
        Assert.Equal("Light Woolen Mantle", mantle.QuestItem);
        Assert.Equal("Isle 4: Keeper of Souls", mantle.Source);
    }

    [Fact]
    public void SkyQuestRefreshCorrectsMetadataButKeepsTicks()
    {
        // A settings file saved while the crossed version shipped (v1.79.0-v1.82.0)
        // still carries that text: the Id-keyed refresh corrects the row in place and the
        // player's tick survives, so a correction never costs anyone their progress.
        // This is the mechanism that lets us change our mind about quest data at all.
        var settings = new AppSettings();
        settings.ApplyDefaultSkyQuestChecklist();
        var row = settings.SkyQuestChecklist.Single(i => i.Id == "sky-005");
        row.QuestItem = "Light Woolen Mantle";
        row.Source = "Isle 4: Keeper of Souls";
        row.Acquired = true;

        Assert.True(settings.ApplyDefaultSkyQuestChecklist());
        Assert.Equal("Light Woolen Mask", row.QuestItem);
        Assert.Equal("Isle 3: Gorgalosk", row.Source);
        Assert.True(row.Acquired);
        Assert.False(settings.ApplyDefaultSkyQuestChecklist());   // refreshed once, then quiet
    }

    // GearSectionSlotsInAfterQuests was DELETED with ApplyDefaultGearSection on 2026-09-04
    // (#252). It asserted that the migration inserts a "gear" key — which stopped being a
    // card at the 2026-08-20 Gear & Loot fold, so the test was pinning the creation of a key
    // no surface could draw and the loot fold re-absorbed on every launch. See
    // SectionFoldIdempotenceTests for what that cost a player, and for the guards that
    // replace this one.

    [Fact]
    public void EpicQuestDefaultsMergeOnce()
    {
        var settings = new AppSettings();

        Assert.True(settings.ApplyDefaultEpicQuestChecklist());
        Assert.Contains(settings.EpicQuestChecklist, i => i.ClassName == "Monk" && i.Reward.Contains("Celestial Fists"));
        var count = settings.EpicQuestChecklist.Count;

        settings.EpicQuestChecklist[0].Acquired = true;
        Assert.False(settings.ApplyDefaultEpicQuestChecklist());
        Assert.Equal(count, settings.EpicQuestChecklist.Count);
        Assert.True(settings.EpicQuestChecklist[0].Acquired);
    }

    [Fact]
    public void EpicQuestDefaultsRefreshExistingChecklistRows()
    {
        var item = EpicQuestDefaults.Items().Single(i =>
            i.ClassName == "Shadow Knight" &&
            i.QuestItem.Contains("Cell Key") &&
            i.QuestItem.Contains("Caradon"));
        var settings = new AppSettings
        {
            EpicQuestChecklist =
            [
                item.Clone()
            ],
        };
        settings.EpicQuestChecklist[0].Section = "old section";
        settings.EpicQuestChecklist[0].QuestItem = "old text";
        settings.EpicQuestChecklist[0].Order = 9999;
        settings.EpicQuestChecklist[0].AvailableInClassic = !item.AvailableInClassic;
        settings.EpicQuestChecklist[0].Acquired = true;

        Assert.True(settings.ApplyDefaultEpicQuestChecklist());

        var refreshed = settings.EpicQuestChecklist.Single(i => i.Id == item.Id);
        Assert.True(refreshed.Acquired);
        Assert.Equal(item.Section, refreshed.Section);
        Assert.Equal(item.QuestItem, refreshed.QuestItem);
        Assert.Equal(item.Order, refreshed.Order);
        Assert.Equal(item.AvailableInClassic, refreshed.AvailableInClassic);
    }
}
