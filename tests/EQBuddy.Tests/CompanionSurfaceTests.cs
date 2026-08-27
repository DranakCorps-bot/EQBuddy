using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The Phase 2 surfaces: what each one projects, that a device only receives
/// what it asked for, and that a section only wakes the devices watching it.</summary>
public class CompanionSurfaceTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 20, 0, 0);

    private static StatsSnapshot Stats(int boneChips = 9) => new()
    {
        Version = 7,
        CurrentZone = "Lower Guk",
        YourKillCount = 42,
        XpPercent = 61.5,
        XpPerHour = 3.5,
        XpPerActiveHour = 4.25,
        HoursToLevel = 2.5,
        AaGained = 3,
        AaTotal = 11,
        AaPerHour = 1.5,
        LastLevel = 34,
        Elapsed = TimeSpan.FromMinutes(90),
        CombatSeconds = 600,
        SessionDps = 55.5,
        Hps = 12,
        PetName = "Gorak",
        LootTotal = 5,
        CraftedTotal = 1,
        Loot = [new LootDetail("Fine Steel Long Sword", 2, "a froglok"), new LootDetail("Bone Chips", boneChips, "a skeleton")],
        Crafted = [new NameCount("Crafted Bracer", 1)],
        DamageBySource = [new SourceDamage("Slash", 40, 4000), new SourceDamage("Kick", 10, 500)],
        HealsBySpell = [new SourceDamage("Superior Healing", 4, 1200)],
        PetAbilities = [new SourceDamage("Bash", 6, 300)],
        Tracked = [new TrackedRuleResult("Rare drops", 3, [new NameCount("Fungi Tunic", 1)], 2.0, 3.0, Now, Now, "Fungi Tunic")],
        LastFight = new LastFightInfo("a froglok shaman", 30, 3000, 400, 200, 100, 6.7, "Killed", false,
            [new SourceDamage("Slash", 20, 2400)],
            [new SourceDamage("Superior Healing", 1, 200)],
            [])
        { PetAbilities = [new SourceDamage("Bash", 2, 120)] },
    };

    private static CompanionSnapshot Build(CompanionInputs? input = null) =>
        CompanionProjection.Build(
            (input ?? new CompanionInputs()) with
            {
                Stats = (input ?? new CompanionInputs()).Stats ?? Stats(),
                Character = "Dranak",
                AppVersion = "1.79.0",
                Offered = input?.Offered ?? CompanionSurfaces.All,
            },
            Now);

    // ---------------- registry ----------------

    [Fact]
    public void EverySurfaceHasALabelAndADescription()
    {
        foreach (var surface in CompanionSurfaces.All)
        {
            Assert.NotEqual(surface, CompanionSurfaces.Label(surface));
            Assert.NotEmpty(CompanionSurfaces.Describe(surface));
        }
        // The page needs a renderer for each; the desktop gate lists each. One list.
        Assert.Equal(CompanionSurfaces.All.Count, CompanionSurfaces.All.Distinct().Count());
    }

    // ---------------- mez ----------------

    [Fact]
    public void MezNumbersOnlyRepeatedNamesAndWarnsInsideTheLastTick()
    {
        var chips = Build(new CompanionInputs
        {
            Mezzes =
            [
                new MezState("a froglok", "Mesmerize", "Shack", Now.AddSeconds(-60), Now.AddSeconds(120)),
                new MezState("a froglok", "Mesmerize", "Shack", Now.AddSeconds(-30), Now.AddSeconds(4)),
                new MezState("a shaman", "Mesmerize", "Shack", Now.AddSeconds(-10), null),
            ],
        }).Mez!.Chips;

        Assert.Equal(["a froglok (1)", "a froglok (2)", "a shaman"], chips.Select(c => c.Name));
        Assert.False(chips[0].Warning);
        Assert.True(chips[1].Warning);          // 4s left, inside the server tick
        Assert.Null(chips[2].RemainingSeconds); // unknown duration keeps its chip
        Assert.Null(chips[2].Fraction);
        // Fraction is the ELAPSED share; the page draws 1 - fraction as a draining bar.
        Assert.Equal(1.0 / 3, chips[0].Fraction!.Value, 3);
    }

    // ---------------- buffs ----------------

    [Fact]
    public void BuffRowsCarryTheStateNotTheColour()
    {
        var buffs = Build(new CompanionInputs
        {
            BuffSets =
            [
                ("Cleric", new List<BuffSetEntryState>
                {
                    new("Symbol of Transal", BuffSetStatus.Active, 1800),
                    new("Aegolism", BuffSetStatus.Expiring, 45),
                    new("Resolution", BuffSetStatus.Missing, null),
                    new("Blessing", BuffSetStatus.NotSeen, null),
                }),
            ],
            BuffLosses = [new BuffLossEntry(Now.AddSeconds(-90), "Aegolism", "lost on death")],
        }).Buffs!;

        Assert.Equal(["active", "expiring", "missing", "notSeen"], buffs.Groups[0].Rows.Select(r => r.Status));
        Assert.Equal("Cleric", buffs.Groups[0].Class);
        Assert.Equal(90, buffs.Lost[0].AgoSeconds, 1);
        Assert.Equal("lost on death", buffs.Lost[0].Cause);
    }

    // ---------------- combat ----------------

    [Fact]
    public void CombatShipsBothScopesSoTheToggleIsInstant()
    {
        var combat = Build().Combat!;
        Assert.Equal(["damage", "healing", "pet"], combat.Boards.Select(b => b.Key));

        var damage = combat.Boards[0];
        Assert.Equal("a froglok shaman · 30s · Killed · 100 dps", damage.FightHeader);
        Assert.StartsWith("Session · 10m in combat · 55.5 dps", damage.SessionHeader);
        Assert.Single(damage.Fight);
        Assert.Equal(2, damage.Session.Count);
        // The value line is the desktop's own, through the shared row builder.
        Assert.StartsWith("4,000 · ×40 · avg 100", damage.Session[0].Value);
        Assert.Equal(1.0, damage.Session[0].Fraction, 3);
        Assert.Equal(88.9, damage.Session[0].Percent, 1);
        Assert.Equal("Pet — Gorak", combat.Boards[2].Label);
    }

    // ---------------- loot ----------------

    [Fact]
    public void LootCarriesItemsCraftedAndWatchCounters()
    {
        var loot = Build().Loot!;
        Assert.Equal(5, loot.Total);
        Assert.Equal(1, loot.CraftedTotal);
        Assert.Equal("Bone Chips", loot.Items[0].Name);   // biggest stack first
        Assert.Equal("Crafted Bracer", loot.Crafted[0].Name);
        Assert.Equal("Rare drops", loot.Watch[0].Name);
        Assert.Equal(3, loot.Watch[0].Total);
        Assert.Equal("Fungi Tunic", loot.Watch[0].LastItem);
    }

    // ---------------- progress ----------------

    [Fact]
    public void ProgressCarriesRatesAndTheLevelUnlocks()
    {
        var unlocks = new LevelUnlockSet(
            [new AaCatalogEntry { Name = "Innate Regeneration", Category = "General" }],
            [new SpellUnlock("Superior Healing", ["Cleric"])]);
        var progress = Build(new CompanionInputs { Level = 34, Unlocks = unlocks }).Progress!;

        Assert.Equal(61.5, progress.XpPercent);
        Assert.Equal(2.5, progress.HoursToLevel);
        Assert.Equal(11, progress.AaTotal);
        Assert.Equal(34, progress.Level);
        Assert.Equal("New at level 34", progress.UnlocksLabel);
        Assert.Equal(["Innate Regeneration", "Superior Healing"], progress.Unlocks.Select(u => u.Name));
    }

    // ---------------- checklists ----------------

    private static AppSettings Checklists()
    {
        var settings = new AppSettings
        {
            EpicQuestClass = "Cleric",
            EpicQuestChecklist =
            [
                new EpicQuestChecklistItem { Id = "e1", ClassName = "Cleric", Section = "Pieces", QuestItem = "Words of Odus", Order = 1, Acquired = true },
                new EpicQuestChecklistItem { Id = "e2", ClassName = "Cleric", Section = "Pieces", QuestItem = "Ruined Wooden Shaft", Order = 2 },
                new EpicQuestChecklistItem { Id = "e3", ClassName = "Paladin", Section = "Pieces", QuestItem = "Not mine", Order = 1 },
            ],
            SkyQuestChecklist =
            [
                new SkyQuestChecklistItem { Id = "s1", ClassName = "Cleric", Reward = "Shield of Rainbow Hues", Npc = "Gorgalosk", QuestItem = "Sky Gem", Acquired = true },
                new SkyQuestChecklistItem { Id = "s2", ClassName = "Cleric", Reward = "Shield of Rainbow Hues", Npc = "Gorgalosk", QuestItem = "Cloud Ruby", Acquired = true },
                new SkyQuestChecklistItem { Id = "s3", ClassName = "Cleric", Reward = "Cape of the Wind", Npc = "Noble Dojorn", QuestItem = "Wind Fragment" },
            ],
            GearChecklist =
            [
                new GearChecklistItem { Slot = "Head", Item = "Crown of Rile", Source = "Lord Nagafen", Acquired = true },
                new GearChecklistItem { Slot = "Chest", Item = "Fungi Tunic" },
            ],
        };
        return settings;
    }

    [Fact]
    public void EpicsHonorTheClassLens()
    {
        var epics = Build(new CompanionInputs { Settings = Checklists() }).Quests!.Epics;
        Assert.Equal(2, epics.Total);          // the Paladin row is another class's
        Assert.Equal(1, epics.Done);
        Assert.Equal("Pieces", epics.Groups[0].Heading);
        Assert.Equal(["Words of Odus", "Ruined Wooden Shaft"], epics.Groups[0].Rows.Select(r => r.Text));
        Assert.True(epics.Groups[0].Rows[0].Done);
    }

    [Fact]
    public void SkyLeadsWithTheReadyRewards()
    {
        var sky = Build(new CompanionInputs { Settings = Checklists() }).Quests!.Sky;
        Assert.Equal("★ Ready 1", sky.Groups[0].Heading);
        Assert.Contains("Shield of Rainbow Hues", sky.Groups[0].Rows[0].Text);

        // Then the per-reward groups, class-prefixed because no Sky class is picked —
        // ordered by ACTIONABILITY since this projection moved onto QuestChecklistLayout,
        // not alphabetically as it was. The Shield (every piece in hand) leads the Cape
        // (nothing in hand), where the alphabet used to put the Cape first. This is the
        // desktop's restored ordering (#205/#209/#210) reaching the phone for free, which
        // is the entire argument for the shared module: mobile and desktop are both
        // first-class and must not drift in either direction (David, 2026-08-18).
        Assert.Equal("Cleric · Shield of Rainbow Hues", sky.Groups[1].Heading);
        Assert.Equal("ready", sky.Groups[1].Note);
        Assert.Equal("Cleric · Cape of the Wind", sky.Groups[2].Heading);
        Assert.Null(sky.Groups[2].Note);

        // The separator is the desktop's "·" now, where this projection had its own em
        // dash. A difference nobody chose is still a difference the player sees.
        Assert.All(sky.Groups.Skip(1), g => Assert.Contains(" · ", g.Heading));
    }

    [Fact]
    public void GearGroupsBySlotAndCarriesATappableId()
    {
        var gear = Build(new CompanionInputs { Settings = Checklists() }).Gear!;
        Assert.Equal(2, gear.Total);
        Assert.Equal(1, gear.Done);
        var row = gear.Groups.SelectMany(g => g.Rows).First(r => r.Text.StartsWith("Fungi"));
        Assert.Equal("Chest|Fungi Tunic", row.Id);
    }

    /// <summary>The phone's half of David's 2026-08-20 ask. A ⧉ copy is a dead end on a
    /// device whose clipboard cannot reach the game on the PC, so he chose selectable text
    /// — but the phone must not be the one surface that names an import and offers
    /// nothing, so the command travels on the wire from GameCommands and the page draws it.
    ///
    /// Asserted on a POPULATED checklist on purpose: the prompt does not belong to the
    /// empty state, it belongs to the surface, and the player whose import has gone stale
    /// is the one holding a full list.</summary>
    [Fact]
    public void GearCarriesTheInGameCommandAndItsOwnEmptyRoute()
    {
        var gear = Build(new CompanionInputs { Settings = Checklists() }).Gear!;
        Assert.NotNull(gear.Prompt);
        Assert.Equal(GameCommands.OutputfileInventory, gear.Prompt!.Command);
        Assert.Equal(CommandPrompts.GearInventory.Lead, gear.Prompt.Lead);
        Assert.NotEmpty(gear.Prompt.Note);
        // The OTHER import — a website export behind a named Options page. Naming one and
        // not the other is what made the old sentence useless on every surface.
        Assert.Equal(GearChecklistPresentation.EmptyRoute, gear.Empty);
    }

    [Fact]
    public void TicksApplyOnlyToTickableSurfacesAndRealRows()
    {
        var settings = Checklists();
        Assert.True(CompanionActions.Apply(settings, new CompanionAction(CompanionSurfaces.Epics, "e2", true)));
        Assert.True(settings.EpicQuestChecklist[1].Acquired);
        // Ticking what's already ticked is not a change — no save, no repaint.
        Assert.False(CompanionActions.Apply(settings, new CompanionAction(CompanionSurfaces.Epics, "e2", true)));
        Assert.True(CompanionActions.Apply(settings, new CompanionAction(CompanionSurfaces.Gear, "Chest|Fungi Tunic", true)));
        Assert.True(settings.GearChecklist[1].Acquired);
        // A stale id from a phone showing an old list changes nothing.
        Assert.False(CompanionActions.Apply(settings, new CompanionAction(CompanionSurfaces.Sky, "gone", true)));
        // And the read-only surfaces accept nothing at all.
        Assert.False(CompanionActions.Apply(settings, new CompanionAction(CompanionSurfaces.Loot, "anything", true)));
        Assert.False(CompanionActions.Apply(settings, new CompanionAction(CompanionSurfaces.Map, "anything", true)));
    }

    [Fact]
    public void UnassignedAutoTicksResolveWhenThePlayerDecides()
    {
        var settings = Checklists();
        settings.EpicQuestChecklist[1].AcquiredUnassigned = true;
        CompanionActions.Apply(settings, new CompanionAction(CompanionSurfaces.Epics, "e2", true));
        Assert.False(settings.EpicQuestChecklist[1].AcquiredUnassigned);
    }

    // ---------------- subscriptions and change detection ----------------

    [Fact]
    public void AMezOnlyDeviceIsSentNothingElse()
    {
        var full = Build(new CompanionInputs
        {
            Settings = Checklists(),
            Mezzes = [new MezState("a froglok", "Mesmerize", "Shack", Now.AddSeconds(-10), Now.AddSeconds(50))],
        });
        var mine = full.ForSubscription([CompanionSurfaces.Mez]);

        Assert.NotNull(mine.Mez);
        Assert.Null(mine.Map);
        Assert.Null(mine.Loot);
        Assert.Null(mine.Quests);
        Assert.Null(mine.Combat);
        var json = mine.ToJson();
        Assert.Contains("\"mez\":{", json);
        Assert.DoesNotContain("\"loot\":{", json);   // "loot" still appears in the OFFER list
        Assert.DoesNotContain("\"geometry\"", json);
    }

    [Fact]
    public void SectionFingerprintsIsolateOneSurfaceFromAnother()
    {
        var settings = Checklists();
        var mez = new List<MezState> { new("a froglok", "Mesmerize", "Shack", Now.AddSeconds(-10), Now.AddSeconds(50)) };
        var baseline = CompanionProjection.SectionFingerprints(
            Build(new CompanionInputs { Settings = settings, Mezzes = mez }));

        // Loot moves; the mez print must not.
        var afterLoot = CompanionProjection.SectionFingerprints(
            Build(new CompanionInputs { Settings = settings, Mezzes = mez, Stats = Stats(boneChips: 10) }));
        Assert.NotEqual(baseline[CompanionSurfaces.Loot], afterLoot[CompanionSurfaces.Loot]);
        Assert.Equal(baseline[CompanionSurfaces.Mez], afterLoot[CompanionSurfaces.Mez]);

        // A mez lands; loot's print must not.
        mez.Add(new MezState("a shaman", "Mesmerize", "Shack", Now, Now.AddSeconds(60)));
        var afterMez = CompanionProjection.SectionFingerprints(
            Build(new CompanionInputs { Settings = settings, Mezzes = mez }));
        Assert.NotEqual(baseline[CompanionSurfaces.Mez], afterMez[CompanionSurfaces.Mez]);
        Assert.Equal(baseline[CompanionSurfaces.Loot], afterMez[CompanionSurfaces.Loot]);
    }

    [Fact]
    public void MapFingerprintMovesWhenACampMarkerDrops()
    {
        var snap = Build(new CompanionInputs
        {
            Map = new CompanionMapSection("Lower Guk", "Lower Guk", "geo-1", null, null, null, [], [], [], []),
        });
        var baseline = CompanionProjection.SectionFingerprints(snap)[CompanionSurfaces.Map];

        var dropped = snap with
        {
            Map = snap.Map! with { Markers = [new CompanionMapPin(10, 20, "camp", 0)] },
        };
        var afterDrop = CompanionProjection.SectionFingerprints(dropped)[CompanionSurfaces.Map];

        Assert.NotEqual(baseline, afterDrop);
    }

    [Fact]
    public void MapFingerprintIgnoresAMarkersAge()
    {
        var snap = Build(new CompanionInputs
        {
            Map = new CompanionMapSection("Lower Guk", "Lower Guk", "geo-1", null, null, null, [], [], [],
                [new CompanionMapPin(10, 20, "camp", 5)]),
        });
        var baseline = CompanionProjection.SectionFingerprints(snap)[CompanionSurfaces.Map];

        var older = snap with
        {
            Map = snap.Map! with { Markers = [new CompanionMapPin(10, 20, "camp", 3600)] },
        };
        var afterTick = CompanionProjection.SectionFingerprints(older)[CompanionSurfaces.Map];

        Assert.Equal(baseline, afterTick);
    }

    [Fact]
    public void FingerprintsSkipTheNumbersThatDriftEveryTick()
    {
        var input = new CompanionInputs
        {
            Settings = Checklists(),
            Mezzes = [new MezState("a froglok", "Mesmerize", "Shack", Now.AddSeconds(-10), Now.AddMinutes(5))],
        };
        var a = CompanionProjection.SectionFingerprints(CompanionProjection.Build(
            input with { Character = "Dranak", Offered = CompanionSurfaces.All }, Now));
        var b = CompanionProjection.SectionFingerprints(CompanionProjection.Build(
            input with { Character = "Dranak", Offered = CompanionSurfaces.All }, Now.AddSeconds(10)));
        Assert.Equal(a[CompanionSurfaces.Mez], b[CompanionSurfaces.Mez]);
        Assert.Equal(a[CompanionSurfaces.Progress], b[CompanionSurfaces.Progress]);
    }

    [Fact]
    public void TheGateStillMeansTheSectionIsNeverBuilt()
    {
        var snap = Build(new CompanionInputs
        {
            Settings = Checklists(),
            Offered = [CompanionSurfaces.Mez],
        });
        Assert.Null(snap.Quests);
        Assert.Null(snap.Loot);
        Assert.Null(snap.Combat);
        Assert.DoesNotContain(CompanionSurfaces.Quests, CompanionProjection.SectionFingerprints(snap).Keys);
    }

    // ---------------- sticky payloads ----------------

    [Fact]
    public void ThemeAndMapGeometryAreSentOncePerDevice()
    {
        var geometry = new CompanionMapGeometry("geo-1", 0, 0, 100, 100,
            [new CompanionMapStroke("#AAAAAA", [0, 0, 10, 10])], [], false);
        var snap = Build(new CompanionInputs
        {
            Theme = CompanionTheme.Project("ParchmentBrass", EQBuddy.UI.Shared.ThemePalettes.For("ParchmentBrass")),
            Map = new CompanionMapSection("Lower Guk", "Lower Guk", "geo-1", geometry, null, null, [], [], [], []),
        });
        var state = new CompanionClientState();

        var first = snap.ForClient([CompanionSurfaces.Map], state);
        Assert.NotNull(first.Theme);
        Assert.NotNull(first.Map!.Geometry);

        var second = snap.ForClient([CompanionSurfaces.Map], state);
        Assert.Null(second.Theme);            // already held
        Assert.Null(second.Map!.Geometry);    // already held
        Assert.Equal("geo-1", second.Map.GeometryStamp);   // but the page still knows which picture

        // A new zone means a new picture, and it goes out again.
        var elsewhere = snap with
        {
            Map = snap.Map! with { Zone = "Befallen", GeometryStamp = "geo-2", Geometry = geometry with { Stamp = "geo-2" } },
        };
        Assert.NotNull(elsewhere.ForClient([CompanionSurfaces.Map], state).Map!.Geometry);

        // A fresh device is told everything, which is what a reconnect must do.
        Assert.NotNull(snap.ForClient([CompanionSurfaces.Map], new CompanionClientState()).Theme);
    }

    // The quest catalog's bug (CompanionQuestsTests, David's phone 2026-08-21) is the
    // map's bug too, and the same rule closes both: the page carries the withheld
    // geometry forward off the PREVIOUS payload, so dropping the map from the picks
    // throws the picture away. Re-adding it in the SAME zone used to be answered with
    // a stamp and no geometry — a map surface that could never draw.
    [Fact]
    public void ReAddingTheMapInTheSameZoneShipsTheGeometryAgain()
    {
        var geometry = new CompanionMapGeometry("geo-1", 0, 0, 100, 100,
            [new CompanionMapStroke("#AAAAAA", [0, 0, 10, 10])], [], false);
        var snap = Build(new CompanionInputs
        {
            Map = new CompanionMapSection("Lower Guk", "Lower Guk", "geo-1", geometry, null, null, [], [], [], []),
        });
        var state = new CompanionClientState();

        Assert.NotNull(snap.ForClient([CompanionSurfaces.Map], state).Map!.Geometry);

        // The map comes off the ⚙ Screens picks: no section, so nothing to carry.
        Assert.Null(snap.ForClient([CompanionSurfaces.Spawns], state).Map);

        // Back on, same zone, and the picture goes out again.
        Assert.NotNull(snap.ForClient([CompanionSurfaces.Map], state).Map!.Geometry);
        Assert.Null(snap.ForClient([CompanionSurfaces.Map], state).Map!.Geometry);  // then held
    }
}
