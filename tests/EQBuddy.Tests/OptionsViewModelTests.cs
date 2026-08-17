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
        Assert.Contains(vm.Cards, c => c.Key == "quests" && c.Title == "Quests");
        Assert.Contains(vm.Cards, c => c.Key == "gear" && c.Title == "Gear");
        // The two cards "quests" replaced are gone from the catalog for good.
        Assert.DoesNotContain(vm.Cards, c => c.Key is "sky" or "epic");

        vm.MoveCard("kills", -1);                        // top can't move up
        Assert.Equal("kills", s.SectionOrder[0]);
        vm.MoveCard("kills", +1);
        Assert.Equal("kills", s.SectionOrder[1]);

        vm.ToggleCard("money");
        Assert.True(vm.Cards.Single(c => c.Key == "money").Hidden);
        vm.ToggleCard("money");
        Assert.False(vm.Cards.Single(c => c.Key == "money").Hidden);
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
    public void SkyQuestSectionSlotsInAfterMotes()
    {
        // Insert-only on purpose: unknown-key cleanup stays the UI layer's job
        // (CardsNormalizeMoveAndToggle above), so Core never carries a section
        // catalog copy. Hidden sections and stray keys pass through untouched.
        var settings = new AppSettings
        {
            SectionOrder = ["combat", "motes", "tracked", "bogus"],
            HiddenSections = ["loot"],
        };

        // The two old quest cards fold onto one, in the EARLIER of their slots — the
        // place the player already looked for quests.
        settings.SectionOrder = ["combat", "motes", "sky", "gear", "epic", "tracked", "bogus"];
        Assert.True(settings.MigrateQuestSections());
        Assert.Equal(["combat", "motes", "quests", "gear", "tracked", "bogus"], settings.SectionOrder);
        Assert.Equal(["loot"], settings.HiddenSections);
        Assert.False(settings.MigrateQuestSections());   // idempotent

        // Hiding BOTH old cards said quests stay off the widget, so the merged card
        // inherits that; the dead keys leave the hidden list either way, because a key
        // matching no catalog entry is invisible in Options and would sit there forever.
        var hidBoth = new AppSettings
        {
            SectionOrder = ["combat", "sky", "epic"],
            HiddenSections = ["sky", "epic"],
        };
        Assert.True(hidBoth.MigrateQuestSections());
        Assert.Equal(["combat", "quests"], hidBoth.SectionOrder);
        Assert.Equal(["quests"], hidBoth.HiddenSections);

        // Keeping either one visible was a statement that quests belong on the widget.
        var hidOne = new AppSettings
        {
            SectionOrder = ["combat", "sky", "epic"],
            HiddenSections = ["epic"],
        };
        Assert.True(hidOne.MigrateQuestSections());
        Assert.Empty(hidOne.HiddenSections);

        // A fresh install's empty order stays empty — the UI appends the catalog.
        Assert.False(new AppSettings().MigrateQuestSections());
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

    [Fact]
    public void GearSectionSlotsInAfterQuests()
    {
        var settings = new AppSettings { SectionOrder = ["combat", "motes", "quests", "tracked"] };

        Assert.True(settings.ApplyDefaultGearSection());
        Assert.Equal(["combat", "motes", "quests", "gear", "tracked"], settings.SectionOrder);
        Assert.False(settings.ApplyDefaultGearSection());

        var noQuests = new AppSettings { SectionOrder = ["combat", "motes", "tracked"] };
        Assert.True(noQuests.ApplyDefaultGearSection());
        Assert.Equal(["combat", "motes", "gear", "tracked"], noQuests.SectionOrder);

        Assert.False(new AppSettings().ApplyDefaultGearSection());
    }

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
