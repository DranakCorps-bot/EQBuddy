using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>Over the real embedded catalogs — these assert facts the eqlwiki harvests
/// recorded (AAs 2026-08-06, spell levels 2026-08-14), so a regenerated catalog that
/// loses a level requirement, a class column, or a class spelling fails here, not on
/// someone's ding.</summary>
public class LevelUnlocksTests
{
    [Fact]
    public void ClassAbilityAppearsAtItsLevelForItsClassOnly()
    {
        // Lay on Hands: Paladin, level 6 — the archetypal "what did I just unlock".
        Assert.Contains(LevelUnlocks.UnlocksAt(["Paladin"], 6).Aas, a => a.Name == "Lay on Hands");
        Assert.DoesNotContain(LevelUnlocks.UnlocksAt(["Warrior"], 6).Aas, a => a.Name == "Lay on Hands");
        // Case-insensitive like every class list in the app.
        Assert.Contains(LevelUnlocks.UnlocksAt(["paladin"], 6).Aas, a => a.Name == "Lay on Hands");
    }

    [Fact]
    public void SpellAppearsAtItsDocumentedClassAndLevel()
    {
        // Greater Healing: Cleric, level 20 — the core Cleric heal, and the dedup
        // winner (the wiki's "Healing Water" twin page carries stale levels; the
        // spell's own page is authoritative in the promotion).
        var unlocks = LevelUnlocks.UnlocksAt(["Cleric"], 20);
        var row = Assert.Single(unlocks.Spells, sp => sp.Name == "Greater Healing");
        Assert.Equal(["Cleric"], row.Classes);
        Assert.DoesNotContain(LevelUnlocks.UnlocksAt(["Wizard"], 20).Spells,
            sp => sp.Name == "Greater Healing");
        Assert.Contains(LevelUnlocks.UnlocksAt(["cleric"], 20).Spells,
            sp => sp.Name == "Greater Healing");
    }

    [Fact]
    public void MultiClassUnionInterleavesSpellsAndAas()
    {
        // Legends allows up to three active classes; level 12 is the "Unbound" AA
        // wave AND a Cleric spell tier — both groups answer, held apart.
        var unlocks = LevelUnlocks.UnlocksAt(["Warrior", "Cleric"], 12);
        Assert.Contains(unlocks.Aas, a => a.Name == "Heroic Leap");     // Warrior AA
        Assert.Contains(unlocks.Aas, a => a.Name == "Unbound Boon");    // Cleric AA
        Assert.Contains(unlocks.Spells, sp => sp.Name == "Halo of Light");    // Cleric spell
        Assert.Contains(unlocks.Spells, sp => sp.Name == "Sense Summoned");   // Cleric spell
        Assert.DoesNotContain(unlocks.Aas, a => a.Name == "Unbound Nature");  // Druid — not picked
        Assert.Equal(unlocks.Aas.Count + unlocks.Spells.Count, unlocks.Count);
    }

    [Fact]
    public void WikiClassSpellingJoinsTheAppSpelling()
    {
        // Cure Disease's wiki page writes the class as "Shadowknight"; the app's
        // class lists (QuestClassFilter, class inference) say "Shadow Knight". The
        // promotion folds the spelling, so the app's picks must join — and the
        // catalog must echo the app spelling back for display.
        var row = Assert.Single(LevelUnlocks.UnlocksAt(["Shadow Knight"], 19).Spells,
            sp => sp.Name == "Cure Disease");
        Assert.Equal(["Shadow Knight"], row.Classes);
        Assert.Contains(LevelUnlocks.UnlocksAt(["shadow knight"], 19).Spells,
            sp => sp.Name == "Cure Disease");
        Assert.Contains(SpellLevelCatalog.Default.Find("Cure Disease")!.Classes,
            c => c.Class == "Shadow Knight" && c.Level == 19);
    }

    [Fact]
    public void ClassAgnosticCategoriesShowRegardlessOfClasses_ButSpellsNeverDo()
    {
        // Rampage is an Archetype AA row (level 30) with no class column in the wiki —
        // included even with no classes picked, labeled rather than guessed. Spells
        // have no class-agnostic rows: no picked class, no spell unlocks.
        var anonymous = LevelUnlocks.UnlocksAt([], 30);
        Assert.Contains(anonymous.Aas, a => a.Name == "Rampage");
        Assert.Empty(anonymous.Spells);
        var warrior = LevelUnlocks.UnlocksAt(["Warrior"], 30);
        Assert.Contains(warrior.Aas, a => a.Name == "Warrior's Endurance");
        Assert.Contains(warrior.Aas, a => a.Name == "Rampage");
    }

    [Fact]
    public void ClassRowsLeadTheAaList()
    {
        // Monk 15: two Class rows (Dragon Force, Purify Body), then Archetype
        // (Double Riposte) — the ding's headline is "my class got a new button".
        var aas = LevelUnlocks.UnlocksAt(["Monk"], 15).Aas;
        Assert.True(aas.Count >= 3);
        Assert.Equal("Class", aas[0].Category);
        Assert.Equal("Class", aas[1].Category);
        Assert.Contains(aas.Skip(2), a => a.Name == "Double Riposte");
    }

    [Fact]
    public void LevelWithNothingIsEmptyNotPadded()
    {
        // No AA requires level 2 for anyone, and Warriors cast nothing — level 2
        // stays a quiet ding.
        Assert.Equal(0, LevelUnlocks.UnlocksAt(["Warrior"], 2).Count);
    }

    [Fact]
    public void NextIsUsuallyTheVeryNextLevelForACaster()
    {
        // Spell levels are dense where AA levels are sparse: a Cleric gains spells
        // at every level to 60, so the preview after the level-6 ding is 7, not the
        // level-10 AA milestone the AA-only feature jumped to.
        var next = LevelUnlocks.Next(["Cleric"], 6);
        Assert.NotNull(next);
        Assert.Equal(7, next!.Value.Level);
        Assert.Contains(next.Value.Unlocks.Spells, sp => sp.Name == "Root");
        // Even the hybrid Paladin: level 7 brings Cease.
        var pal = LevelUnlocks.Next(["Paladin"], 6);
        Assert.Equal(7, pal!.Value.Level);
        Assert.Contains(pal.Value.Unlocks.Spells, sp => sp.Name == "Cease");
    }

    [Fact]
    public void NextStillJumpsSparseAaLevelsWhenNoSpellsApply()
    {
        // No classes picked = AA class-agnostic categories only: level 8 belongs to
        // Ranger Class rows alone, so the next milestone after 6 is 10 (Exodus,
        // Archetype) — the sparse-jump behavior the AA-only feature shipped with.
        var next = LevelUnlocks.Next([], 6);
        Assert.NotNull(next);
        Assert.Equal(10, next!.Value.Level);
        Assert.Contains(next.Value.Unlocks.Aas, a => a.Name == "Exodus");
        Assert.Empty(next.Value.Unlocks.Spells);
    }

    [Fact]
    public void NextPastTheLastMilestoneIsNull()
    {
        // The AA catalog tops out at 50, spell levels at 60. A spell-less Warrior
        // has nothing past 50; a Cleric now previews spell tiers to 60 and stops.
        Assert.Null(LevelUnlocks.Next(["Warrior"], 50));
        Assert.NotNull(LevelUnlocks.Next(["Cleric"], 50));
        Assert.Null(LevelUnlocks.Next(["Cleric"], 60));
    }

    [Fact]
    public void EmbeddedSpellCatalogIsSaneAndClean()
    {
        var catalog = SpellLevelCatalog.Default;
        Assert.True(catalog.Count > 1000, $"only {catalog.Count} spells");
        // The promotion's row discipline: real classes, real levels, cap 60.
        Assert.All(catalog.All, s =>
        {
            Assert.NotEmpty(s.Classes);
            Assert.All(s.Classes, c =>
            {
                Assert.True(c.Class.Length > 0);
                Assert.InRange(c.Level, 1, 60);
            });
        });
        // A stable anchor fact, and it MOVED on 2026-08-23 for a reason worth recording:
        // it was "Complete Healing", the signature classic Cleric heal, and the class-page
        // re-source (PR 1) removed it. That is not a loss — eqlwiki's Legends-curated
        // Cleric page does not list it at any level; its Level 39 is Promised Renewal,
        // Sacred Word and the rest. The old catalog carried it because SPELL pages name
        // every class that has ever had a spell, which is wider than this game. If this
        // assertion fails again, check the class page before assuming the promotion broke.
        var ch = Assert.Single(catalog.Find("Promised Renewal")!.Classes);
        Assert.Equal(("Cleric", 39), (ch.Class, ch.Level));

        // Provenance is real and bounded: every row says where it came from, and a
        // derived row only exists where the class page has no section for that level.
        Assert.All(catalog.All, s => Assert.All(s.Classes, c =>
            Assert.True(c.Source is SpellClassLevel.ClassPage or SpellClassLevel.SpellPage,
                $"{s.Name}/{c.Class} has source '{c.Source}'")));
        var derived = catalog.All.SelectMany(s => s.Classes).Count(c => c.IsDerived);
        // Every class page stops at 50 against Legends' cap of 60, so derived rows are
        // NORMAL rather than exceptional — but they are a minority, and a promotion that
        // started deriving everything (an empty class-spells.json, say) would fail here.
        Assert.InRange(derived, 100, catalog.All.SelectMany(s => s.Classes).Count() / 2);
        // The three classes eqlwiki gives no spell table are absent entirely, rather than
        // being handed one from the wider spell pages.
        foreach (var none in (string[])["Warrior", "Monk", "Berserker"])
            Assert.DoesNotContain(catalog.All.SelectMany(s => s.Classes),
                c => c.Class.Equals(none, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RowValueNamesTheSourceAndTheRankSpan()
    {
        // Class row, single rank: just the class.
        Assert.Equal("Warrior", LevelUnlockText.RowValue(AaCatalog.Find("Heroic Leap")!));
        // Class row with ranks to buy: class plus rank count.
        Assert.Equal("Paladin · 10 ranks", LevelUnlockText.RowValue(AaCatalog.Find("Lay on Hands")!));
        // Class-agnostic multi-rank row: category plus rank count.
        Assert.Equal("Archetype · 3 ranks", LevelUnlockText.RowValue(AaCatalog.Find("Healing Adept")!));
    }

    [Fact]
    public void SpellRowValueMarksTheSpellApartFromAaRows()
    {
        Assert.Equal("Cleric spell", LevelUnlockText.SpellRowValue(new SpellUnlock("x", ["Cleric"])));
        Assert.Equal("Druid/Ranger spell",
            LevelUnlockText.SpellRowValue(new SpellUnlock("x", ["Druid", "Ranger"])));
        // A row the class page never listed says so rather than passing as curated
        // (David's ruling: flagged, not filtered; Bevel: do not silently pad).
        Assert.Equal("Cleric spell · from its spell page",
            LevelUnlockText.SpellRowValue(new SpellUnlock("x", ["Cleric"], Derived: true)));
    }

    [Fact]
    public void SpellTooltipListsCatalogClassesAndNothingInvented()
    {
        // What it DOES leads, the class levels follow (David, 2026-08-23: "have mouse over
        // give the skill/spell description"). Before this the hover was the levels alone,
        // while AA rows had shown the wiki's effect text since the ledger existed.
        var promised = SpellLevelCatalog.Default.Find("Promised Renewal")!;
        // The wording moved SOURCE on 2026-08-31, not meaning: eqlwiki's class-row template
        // became KhazamSpellRow and dropped its `description` field, so this prose now comes
        // from the spell PAGE ("after 18s" where the class row said "after 0:00:18"). Same
        // fact, same wiki, different page — and the StartsWith below is the assertion that
        // actually guards the rule, so this literal is a canary for the source moving again.
        Assert.Equal("Imbues your target with life after 18s, healing for 5000. · Cleric 39",
            LevelUnlockText.SpellTooltip(promised));
        // One LINE, deliberately: both widgets render a tooltip containing a newline in
        // monospace (it is how stat blocks keep their columns), and wiki prose is not a
        // stat block. Nothing else can catch that — a tooltip is not in a screenshot and
        // no test reads a font — so the rule is pinned here.
        Assert.DoesNotContain("\n", LevelUnlockText.SpellTooltip(promised));
        // NOTHING INVENTED, and this is the assertion that keeps that true: the first line
        // is the catalog's own string, character for character, not prose composed here.
        Assert.StartsWith(promised.Description, LevelUnlockText.SpellTooltip(promised));
        // Multi-class spells list every class with its own level.
        var tip = LevelUnlockText.SpellTooltip(SpellLevelCatalog.Default.Find("Greater Healing"))!;
        Assert.Contains("Cleric 20", tip);
        Assert.Contains("Druid 29", tip);
        Assert.Null(LevelUnlockText.SpellTooltip(null));

        // A spell the wiki describes with nothing hovers its levels rather than an empty
        // box — and a spell with no levels at all still hovers its description. Neither
        // case is reachable from the SHIPPED catalog (the assertion at the end of this
        // test is that every entry has prose), so both are pinned against hand-built
        // entries: the behaviour has to survive whatever the catalog happens to contain.
        Assert.Equal("Cleric 39", LevelUnlockText.SpellTooltip(new SpellLevelEntry
        {
            Name = "x", Classes = [new SpellClassLevel { Class = "Cleric", Level = 39 }],
        }));
        Assert.Equal("Heals a bit", LevelUnlockText.SpellTooltip(new SpellLevelEntry
        {
            Name = "x", Description = "Heals a bit",
        }));

        // Every shipped entry carries the wiki's prose, so the hover is never bare.
        //
        // **This stays at 100% with NO exemption list, and that was a decision.** The
        // 2026-08-31 KhazamSpellRow rename left 24 entries description-less and the
        // proposed unblock was a curated known-gaps list ("no eqlwiki prose") in the
        // shape of DeadSettingTests.Known. The premise turned out to be false: all 24
        // HAVE prose on eqlwiki, on their own spell page, and were missed only because
        // the promote looked them up by the page's `spellname` field — a copy-paste
        // artefact the harvest's own docstring warns is not a canonical name. Keying the
        // fallback on the page TITLE recovered all 24, so there is nothing to exempt.
        // An exemption list with no entries to justify is a hole waiting for the next
        // harvest regression to be waved through it.
        Assert.All(SpellLevelCatalog.Default.All, e =>
            Assert.False(string.IsNullOrWhiteSpace(e.Description), $"{e.Name} has no description"));
    }

    [Fact]
    public void NextLabelFoldsAndCountsBothGroups()
    {
        Assert.Equal("▸ At level 35: 2 new AA abilities",
            LevelUnlockText.NextLabel(35, 2, 0, expanded: false));
        Assert.Equal("▾ At level 6: 1 new AA ability, 3 new spells",
            LevelUnlockText.NextLabel(6, 1, 3, expanded: true));
        Assert.Equal("▸ At level 7: 1 new spell",
            LevelUnlockText.NextLabel(7, 0, 1, expanded: false));
    }
}
