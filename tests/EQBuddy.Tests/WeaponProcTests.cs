using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **WHICH WEAPONS PROC, AND THE PROOF THAT SAYING SO COSTS NOTHING** (DRA-241, Helm ruling
/// <c>27302878</c>: report only, never price; annotate, never refuse).
///
/// <para>Two of the tests here matter more than the rest, and they are the two that are hard to
/// write: <see cref="TheProcWeighsNothing"/> proves the reading is in no comparison, and
/// <see cref="TheProcRefusesNothing"/> proves it removes no offer. Every other rule in this file
/// only ever ADDS a sentence, so those two are what bound the whole slice's blast radius.</para>
/// </summary>
public class WeaponProcTests
{
    // ---------------------------------------------------------------- the reading

    [Theory]
    // The wiki's own combat parenthetical, in the spellings the shipped catalog uses.
    [InlineData("Ykesha (Combat, Casting Time: Instant) at Level 37", ItemEffectKind.Combat)]
    [InlineData("Stunning Blow (Combat, Casting Time: Instant)", ItemEffectKind.Combat)]
    [InlineData("Flame of the Efreeti (Combat)", ItemEffectKind.Combat)]
    // The three that are effects and are NOT procs. Each fires for a different reason and
    // none of them is "the weapon hit something".
    [InlineData("Shielding (Must Equip)", ItemEffectKind.MustEquip)]
    [InlineData("Gate (Any Slot/Can Equip, Casting Time: 20.0) at Level 5", ItemEffectKind.AnySlot)]
    [InlineData("Clarity (Any Slot, Casting Time: 7.0)", ItemEffectKind.AnySlot)]
    [InlineData("See Invisible (Worn)", ItemEffectKind.Worn)]
    // Case and whitespace: one side is a committed file, the other a live parse.
    [InlineData("  ykesha (COMBAT, Casting Time: Instant)  ", ItemEffectKind.Combat)]
    // A parenthetical nobody has measured is REPORTED, never bucketed (trap 78).
    [InlineData("Dyn's Dizzying Draught (Proc)", ItemEffectKind.Unadmitted)]
    [InlineData("Stun (Req Level 30)", ItemEffectKind.Unadmitted)]
    [InlineData("Deadly Lifetap (Casting Time: Instant) at Level ?", ItemEffectKind.Unadmitted)]
    // No parenthetical at all, and an unterminated one.
    [InlineData("Ykesha", ItemEffectKind.Unadmitted)]
    [InlineData("Ykesha (Combat", ItemEffectKind.Unadmitted)]
    public void TheParentheticalDecidesTheKind(string effect, ItemEffectKind expected) =>
        Assert.Equal(expected, WeaponProcs.Kind(effect));

    /// <summary>An absent effect line is <see cref="ItemEffectKind.NoEffect"/> — but only when
    /// the block said nothing at all. A block that mentioned an effect this parser could not
    /// read answers <see cref="ItemEffectKind.Unadmitted"/> instead, which is the whole of why
    /// <see cref="ItemStatsBlock.MentionsEffect"/> exists.</summary>
    [Theory]
    [InlineData("", false, ItemEffectKind.NoEffect)]
    [InlineData("   ", false, ItemEffectKind.NoEffect)]
    [InlineData(null, false, ItemEffectKind.NoEffect)]
    [InlineData("", true, ItemEffectKind.Unadmitted)]
    [InlineData(null, true, ItemEffectKind.Unadmitted)]
    public void AnUnreadableEffectIsNeverAnAbsentOne(
        string? effect, bool mentions, ItemEffectKind expected) =>
        Assert.Equal(expected, WeaponProcs.Kind(effect, mentions));

    /// <summary>The first clause is matched WHOLE, so a longer word starting with an admitted
    /// one is not that word — the <c>Tradeskills.Match</c> rule, and for its reason.</summary>
    [Theory]
    [InlineData("X (Combatant)")]
    [InlineData("X (Wornout)")]
    [InlineData("X (Any Slotting)")]
    public void AParentheticalIsMatchedWholeOrNotAtAll(string effect) =>
        Assert.Equal(ItemEffectKind.Unadmitted, WeaponProcs.Kind(effect));

    /// <summary>The spell's name, with the parenthetical and its trailing level cut off. The
    /// LEVEL is dropped deliberately: it is the level the proc fires at, not a requirement on
    /// the item, and a row printing it beside a lower-level character would read as a
    /// restriction that does not exist.</summary>
    [Theory]
    [InlineData("Ykesha (Combat, Casting Time: Instant) at Level 37", "Ykesha")]
    [InlineData("  Ykesha (Combat)", "Ykesha")]
    [InlineData("Flame of the Efreeti (Combat)", "Flame of the Efreeti")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void TheProcNameIsTheSpellAndNothingElse(string? effect, string expected) =>
        Assert.Equal(expected, WeaponProcs.ProcName(effect));

    // ------------------------------------------------- row 2: scope is the WEAPON record

    /// <summary>
    /// **THE SCOPE IS THE WEAPON RECORD, NOT THE EFFECT LINE** (bar row 2).
    ///
    /// <para>The failure this prevents is not hypothetical and it is not small: 66 committed
    /// records carry a <c>(Combat)</c> effect and no damage, and every one of them is a Rogue
    /// poison — a consumable you apply TO a weapon. A reading scoped to the effect line calls
    /// all 66 of them weapon procs.</para>
    /// </summary>
    [Fact]
    public void OnlyAWeaponProcs()
    {
        var weapon = Block("Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 12", "Atk Delay: 26",
                           "Effect: Ykesha (Combat, Casting Time: Instant) at Level 37");
        Assert.Equal("Ykesha", WeaponProcs.Proc(weapon));

        // The same effect line, on a record the block reads no damage on.
        var poison = Block("Effect: Ykesha (Combat, Casting Time: Instant) at Level 37");
        Assert.Equal(ItemEffectKind.Combat, WeaponProcs.Kind(poison));
        Assert.Equal("", WeaponProcs.Proc(poison));

        // And a weapon whose effect is not a proc answers nothing either.
        var clicky = Block("Slot: PRIMARY", "DMG: 9", "Effect: Gate (Any Slot/Can Equip)");
        Assert.Equal("", WeaponProcs.Proc(clicky));

        Assert.Equal("", WeaponProcs.Proc(null));
    }

    /// <summary>
    /// The 66 poisons, named against the committed catalog. Three are named verbatim because a
    /// count alone would not say WHICH class of record the scope rule is holding back.
    /// </summary>
    [Fact]
    public void TheRoguePoisonsAreNotWeaponProcs()
    {
        var poisons = ItemCatalog.Default.All
            .Where(r => r.Dmg is null
                        && WeaponProcs.Kind(r.ToStatsBlock()) == ItemEffectKind.Combat)
            .ToList();

        Assert.True(poisons.Count >= 50,
            $"only {poisons.Count} combat-effect records with no damage — if this has collapsed, "
            + "check whether the DMG scope is still doing anything");
        Assert.All(poisons, r => Assert.Equal("", WeaponProcs.Proc(r.ToStatsBlock())));

        // Named verbatim, because a count alone would not say WHICH class of record the scope
        // rule holds back. The bar's third name was `Deadly Poison`, which is not in the
        // committed catalog under that spelling — `Crookstinger Poison` is one that is, and the
        // substitution is recorded rather than quietly made.
        var names = poisons.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Asp Poison", names);
        Assert.Contains("Basilisk Poison", names);
        Assert.Contains("Crookstinger Poison", names);
    }

    // ------------------------------------------- rows 3 and 4: the committed counts and set

    /// <summary>
    /// **THE SHIPPED CATALOG, COUNTED** (bar row 3) — the measurement the rule was written from,
    /// kept executable so a refresh that moves one says so.
    ///
    /// <para>Floors rather than equalities on the bucket counts, because the catalog is
    /// regenerated weekly and an exact total reddens on churn that says nothing about the rule
    /// (trap 74). <b>The UNADMITTED set is an exact list</b>, which is the opposite choice and
    /// the deliberate one: it is the whole guard against a spelling being silently filed under
    /// one of the other five.</para>
    ///
    /// <para>The DISTINCT-COUNT assertion is the trap-73 telltale. 378 records naming 216
    /// distinct spells is transcription; 378 naming four would be a template, and the day this
    /// reading starts inventing a value is the day that number collapses.</para>
    /// </summary>
    [Fact]
    public void TheCatalogsOwnEffectLinesAreAdmittedOrNamed()
    {
        var weapons = ItemCatalog.Default.All.Where(r => r.ToStatsBlock().Dmg is not null)
                                             .ToList();
        var byKind = new Dictionary<ItemEffectKind, int>();
        var unadmitted = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var record in weapons)
        {
            var kind = WeaponProcs.Kind(record.ToStatsBlock());
            byKind[kind] = byKind.GetValueOrDefault(kind) + 1;
            if (kind == ItemEffectKind.Unadmitted) unadmitted.Add(record.Name);
        }

        // Measured 2026-09-20 against the committed catalog: 11,196 records, 1,648 of them
        // weapons (1,649 since DRA-251 admitted Keg Mallet's `Base Dmg:`), of which 378 Combat / 47 MustEquip / 47 AnySlot / 9 Worn / 7 Unadmitted.
        Assert.True(weapons.Count >= 1_500,
            $"only {weapons.Count} weapon records — the DMG scope has stopped finding them");
        Assert.True(byKind.GetValueOrDefault(ItemEffectKind.Combat) >= 300,
            $"only {byKind.GetValueOrDefault(ItemEffectKind.Combat)} combat procs");
        Assert.True(byKind.GetValueOrDefault(ItemEffectKind.MustEquip) >= 35);
        Assert.True(byKind.GetValueOrDefault(ItemEffectKind.AnySlot) >= 35);
        Assert.True(byKind.GetValueOrDefault(ItemEffectKind.Worn) >= 5);
        Assert.True(byKind.GetValueOrDefault(ItemEffectKind.NoEffect) >= 1_000);

        // **THE EXACT SET** (bar row 4), each with the shape that put it here. The two
        // `Combat Effect:` rows are the ones Helm named: the match is NOT widened to rescue
        // them, so they are expected to sit in this list permanently.
        Assert.Equal(
            [
                "Blam Stick",               // a bare `Effect:` with nothing after it
                "Rod of Understanding",     // `(Proc)`
                "Sabertooth Short Bow",     // `Combat Effect:` — the word LEFT of the colon
                "Sharp Claws",              // `Combat Effect:` — likewise
                "Spiroc Wingblade",         // only a `(Casting Time: …)`
                "TornEar Thumper",          // `(Req Level 30)`
                "Trakanon's Tooth",         // `(Casting Time: …) at Level ?`
            ],
            unadmitted);

        // **UNADMITTED REFUSES NOTHING** — the WeaponHands.Unadmitted rule verbatim. Every one
        // of the seven simply gets no proc sentence; none is removed from anything.
        Assert.All(weapons.Where(r =>
                WeaponProcs.Kind(r.ToStatsBlock()) == ItemEffectKind.Unadmitted),
            r => Assert.Equal("", WeaponProcs.Proc(r.ToStatsBlock())));

        // The trap-73 telltale: this is transcription, not boilerplate.
        var procNames = weapons
            .Select(r => WeaponProcs.Proc(r.ToStatsBlock()))
            .Where(p => p.Length > 0)
            .ToList();
        Assert.True(procNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 150,
            $"{procNames.Count} procs naming only "
            + $"{procNames.Distinct(StringComparer.OrdinalIgnoreCase).Count()} distinct spells — "
            + "that is a template rather than a reading of the pages");
        Assert.DoesNotContain("", procNames);
    }

    /// <summary>The two left-of-colon rows, named and asserted one at a time. They are almost
    /// certainly combat procs and this reading deliberately does not rescue them (trap 66): a
    /// forgiveness rule written for two known rows is a rule about a POSITION, and the next
    /// spelling it sweeps in is one nobody has looked at.</summary>
    [Theory]
    [InlineData("Sabertooth Short Bow")]
    [InlineData("Sharp Claws")]
    public void TheLeftOfColonSpellingIsReportedAndNotRescued(string name)
    {
        var record = ItemCatalog.Default.Find(name);
        Assert.NotNull(record);
        var stats = record!.ToStatsBlock();

        Assert.Contains("Combat Effect:", record.StatsText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", stats.Effect);              // the anchored rule did not read it
        Assert.True(stats.MentionsEffect);           // but the block did say the word
        Assert.Equal(ItemEffectKind.Unadmitted, WeaponProcs.Kind(stats));
        Assert.Equal("", WeaponProcs.Proc(stats));   // and it removes nothing
    }

    /// <summary>The catalog is where this is read from, so the catalog's own path has to carry
    /// it. <c>ToStatsBlock</c> builds from the promoter's COLUMNS and there is no effect column
    /// — if this ever regressed to <see cref="ItemStatsBlock.Parse"/>'s copy only, every one of
    /// the 11,196 records would answer "" and every count above would still be a floor away
    /// from red.</summary>
    [Fact]
    public void TheCatalogsOwnRecordsCarryTheEffect()
    {
        var record = ItemCatalog.Default.Find("Adamantite Club");
        Assert.NotNull(record);
        Assert.Equal("Stunning Blow", WeaponProcs.Proc(record!.ToStatsBlock()));
    }

    // -------------------------------------------------- row 5: PROVE IT WEIGHS NOTHING

    /// <summary>
    /// **PROVE IT WEIGHS NOTHING** (bar row 5) — the executable form of "never price it", and
    /// the single most important assertion in this file.
    ///
    /// <para>The same candidate is compared twice against the same worn item: once with a proc
    /// on its page and once without. <see cref="ItemDominance.MetricPairs"/> must produce the
    /// identical table and <see cref="ItemDominance.Compare"/> the identical verdict — including
    /// in the case where the candidate LOSES, so a proc cannot rescue a worse item any more than
    /// it can promote a better one.</para>
    /// </summary>
    [Fact]
    public void TheProcWeighsNothing()
    {
        var worn = Block("Slot: PRIMARY", "Skill: 1H Blunt", "DMG: 10", "Atk Delay: 30", "AC: 5");
        string[] weapon = ["Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 12", "Atk Delay: 26",
                           "AC: 7"];

        var plain = Block(weapon);
        var procs = Block([.. weapon, "Effect: Ykesha (Combat, Casting Time: Instant) at Level 37"]);

        // The reading did fire — otherwise this test proves nothing (trap 34).
        Assert.Equal("Ykesha", WeaponProcs.Proc(procs));
        Assert.Equal("", WeaponProcs.Proc(plain));

        // The metric table is byte-for-byte the same table.
        Assert.Equal(
            ItemDominance.MetricPairs(plain, worn).ToList(),
            ItemDominance.MetricPairs(procs, worn).ToList());

        // And the verdict does not move, in either direction.
        Assert.Equal(
            ItemDominance.Compare("Candidate", plain, "Worn", worn, [], false),
            ItemDominance.Compare("Candidate", procs, "Worn", worn, [], false));
        Assert.Equal(DominanceVerdict.Yes,
            ItemDominance.Compare("Candidate", procs, "Worn", worn, [], false));

        // The losing case matters just as much: a proc may not rescue a worse item.
        var worse = Block("Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 2", "Atk Delay: 40",
                          "Effect: Ykesha (Combat, Casting Time: Instant)");
        Assert.Equal("Ykesha", WeaponProcs.Proc(worse));
        Assert.Equal(DominanceVerdict.No,
            ItemDominance.Compare("Candidate", worse, "Worn", worn, [], false));

        // And it is in no metric the table prices, under any spelling.
        Assert.DoesNotContain(
            ItemDominance.MetricPairs(procs, worn),
            p => p.Metric.Contains("proc", StringComparison.OrdinalIgnoreCase)
                 || p.Metric.Contains("effect", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The gain a row NAMES is chosen from the metric table, so a proc cannot become
    /// the headline improvement either.</summary>
    [Fact]
    public void TheNamedGainIsNeverTheProc()
    {
        var worn = Block("Slot: PRIMARY", "DMG: 10", "Atk Delay: 30");
        var procs = Block("Slot: PRIMARY", "DMG: 12", "Atk Delay: 30",
                          "Effect: Ykesha (Combat)");

        var gain = ItemDominance.Gain(procs, worn, new HashSet<string>());
        Assert.NotNull(gain);
        Assert.DoesNotContain("proc", gain!.Value.Metric, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------- row 6: PROVE IT REFUSES NOTHING

    /// <summary>
    /// **PROVE IT REFUSES NOTHING** (bar row 6) — "annotate, never refuse", made executable.
    ///
    /// <para>The whole sweep is run twice over the same worn sheet: once against a catalog whose
    /// weapons carry their effect lines, and once against the SAME catalog with every effect
    /// line stripped. The offers must be identical in COUNT and in IDENTITY, and every refusal
    /// counter must be unmoved.</para>
    ///
    /// <para><b>The contrast is the point.</b> D6's off-hand rule DOES refuse, and it may,
    /// because it reads a fact off the player's own dump — their SECONDARY is occupied. A proc
    /// refusal would have no fact behind it, only a price.</para>
    /// </summary>
    [Fact]
    public void TheProcRefusesNothing()
    {
        WornItem[] worn =
        [
            new("Rusty Long Sword", "Rusty Long Sword", "PRIMARY",
                Block("Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 3", "Atk Delay: 40")),
        ];

        var on = GearUpgrades.Sweep(
            GearIntent.ReplaceSlot, worn, [], CatalogWithWeapons(proc: true), [], true);
        var off = GearUpgrades.Sweep(
            GearIntent.ReplaceSlot, worn, [], CatalogWithWeapons(proc: false), [], true);

        // The reading fired on one side and not the other — otherwise this is vacuous.
        Assert.Contains(on.Upgrades, u => u.Proc.Length > 0);
        Assert.All(off.Upgrades, u => Assert.Equal("", u.Proc));

        // Identical in COUNT...
        Assert.Equal(off.Upgrades.Count, on.Upgrades.Count);
        // ...and in IDENTITY, in the same order — a proc moves no row up or down either.
        Assert.Equal(
            off.Upgrades.Select(u => $"{u.Item}|{u.Slot}|{u.Over}|{u.GainMetric}|{u.GainBy}")
                        .ToList(),
            on.Upgrades.Select(u => $"{u.Item}|{u.Slot}|{u.Over}|{u.GainMetric}|{u.GainBy}")
                       .ToList());

        // And every counter the sweep keeps about what it held back is unmoved.
        Assert.Equal(off.Withheld, on.Withheld);
        Assert.Equal(off.NoSource, on.NoSource);
        Assert.Equal(off.QuestOnly, on.QuestOnly);
        Assert.Equal(off.OffHandRefusals, on.OffHandRefusals);
    }

    // ----------------------------------------------------------- row 7: the words

    /// <summary>The row names the proc as its own sentence, and says nothing about what it is
    /// worth — no adverb, no conjunction, no comparison.</summary>
    [Fact]
    public void TheRowReportsTheProcAndPricesIt()
    {
        var line = HelperPresentation.Why(new GearUpgradeFact(
            "Flamberge", "Rusty Long Sword", "PRIMARY", "DMG", 9, [], 0, 0,
            "Flame of the Efreeti"));

        Assert.Contains("It procs Flame of the Efreeti.", line);
        foreach (var valuation in new[] { "also", "even", "better than", "worth", "on top" })
            Assert.DoesNotContain(valuation, line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An upgrade with no proc says nothing at all about procs — an unanswered
    /// question draws nothing (trap 73).</summary>
    [Fact]
    public void ARowWithNoProcSaysNothingAboutProcs()
    {
        var line = HelperPresentation.Why(new GearUpgradeFact(
            "Bronze Helm", "Cloth Cap", "HEAD", "AC", 8, [], 0, 0, ""));

        Assert.DoesNotContain("proc", line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The caveat is said ONCE per block and never per row (trap 73), and it names the
    /// thing it is about without pricing it.</summary>
    [Fact]
    public void TheCaveatIsSaidOnceAndNamesNoPrice()
    {
        var note = HelperPresentation.GearProcNote;
        Assert.Contains("proc", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("counted for nothing", note, StringComparison.OrdinalIgnoreCase);

        // Three rows, three proc sentences, and the caveat is not one of them.
        var rows = new[] { "Ykesha", "Stunning Blow", "Flame of the Efreeti" }
            .Select(p => HelperPresentation.Why(new GearUpgradeFact(
                $"Weapon {p}", "Rusty Long Sword", "PRIMARY", "DMG", 4, [], 0, 0, p)))
            .ToList();

        Assert.All(rows, r => Assert.DoesNotContain(note, r));
        Assert.Equal(3, rows.Count(r => r.Contains("It procs ")));
    }

    // ---------------------------------------------------------------------- helpers

    private static ItemStatsBlock Block(params string[] lines) => ItemStatsBlock.Parse(lines);

    /// <summary>
    /// A small catalog of PRIMARY weapons that all beat the worn sheet above, half of them
    /// carrying a combat proc — or, with <paramref name="proc"/> false, the identical catalog
    /// with every effect line stripped. That pair is what makes "the reading on and off" a
    /// comparison rather than an assertion about one run.
    /// </summary>
    private static ItemCatalog CatalogWithWeapons(bool proc)
    {
        var records = new List<ItemCatalog.Record>();
        for (var i = 0; i < 6; i++)
        {
            var effect = proc && i % 2 == 0
                ? "\nEffect: Ykesha (Combat, Casting Time: Instant) at Level 37"
                : "";
            records.Add(new ItemCatalog.Record
            {
                Name = $"Test Blade {i}",
                Slots = ["PRIMARY"],
                Skill = "1H Slashing",
                Dmg = 10 + i,
                Delay = 30,
                DropZones = ["Test Zone"],
                DropMobs = new Dictionary<string, List<string>>
                {
                    ["Test Zone"] = ["a test creature"],
                },
                StatsText =
                    $"Slot: PRIMARY\nSkill: 1H Slashing\nDMG: {10 + i}\nAtk Delay: 30{effect}",
            });
        }

        return new ItemCatalog(records);
    }
}
