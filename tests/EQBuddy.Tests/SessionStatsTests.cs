using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Replay tests: feed event streams (as raw log lines) through the parser into
/// SessionStats and assert the resulting snapshot — the same path live tailing uses.
/// </summary>
public class SessionStatsTests
{
    /// <summary>Regen estimate (David's live bard test, 2026-08-06): the tick line
    /// "Your wounds begin to heal." names no spell and no amount. Attribution comes from
    /// your own cast/song among the four spells sharing that line; the amount is the
    /// player's Options override when set, else the wiki base. No cast seen = count only.</summary>
    [Fact]
    public void RegenTicksEstimateFromTheAttributedSongAtWikiBase()
    {
        var stats = new SessionStats();
        void Line(int ss, string msg) =>
            stats.Apply(LogParser.Parse($"[Thu Aug 06 14:43:{ss:D2} 2026] {msg}")!);
        Line(24, "You begin singing Hymn of Restoration.");
        Line(30, "Your wounds begin to heal.");
        Line(36, "Your wounds begin to heal.");
        Line(42, "Your wounds begin to heal.");

        var s = stats.Snapshot();
        Assert.Equal(3, s.RegenTicks);
        Assert.Equal("Hymn of Restoration", s.RegenSpell);
        Assert.Equal(27, s.RegenEstimatedHealed);   // 3 × wiki base 9
        Assert.Equal(0, s.HealingDone);              // estimates never join real totals
    }

    /// <summary>Class inference (David, 2026-08-11): class-unique abilities vote,
    /// frequency wins (a clicky's one-off loses to real swings), three sightings
    /// minimum, and a character switch clears the ballot.</summary>
    [Fact]
    public void ClassIsInferredFromMostUsedUniqueSkillsAndClearsOnSwitch()
    {
        var stats = new SessionStats();
        void Line(int ss, string msg) =>
            stats.Apply(LogParser.Parse($"[Mon Aug 10 11:15:{ss:D2} 2026] {msg}")!);

        Line(1, "You backstab orc centurion for 50 points of damage.");
        Line(2, "You backstab orc centurion for 55 points of damage.");
        Assert.Equal("", stats.Snapshot().InferredClass);   // under the floor: no call yet

        Line(3, "You backstab orc centurion for 60 points of damage.");
        Assert.Equal("Rogue", stats.Snapshot().InferredClass);

        stats.ClearCharacterState();
        Assert.Equal("", stats.Snapshot().InferredClass);
    }

    /// <summary>Procs (#85, Kerdude's spellblade snippet): a cast nuke and a weapon proc
    /// print the identical damage line — the missing "You begin casting X." is the only
    /// tell. An item-proc line just before the hit names the vehicle; incoming spell
    /// damage ("hit you ... by Fire Bolt") must never be mistaken for an own proc.</summary>
    [Fact]
    public void UncastSpellDamageCountsAsAProcCastSpellDamageDoesNot()
    {
        var stats = new SessionStats();
        void Line(int mm, int ss, string msg) =>
            stats.Apply(LogParser.Parse($"[Mon Aug 10 11:{mm:D2}:{ss:D2} 2026] {msg}")!);
        Line(15, 1, "You begin casting Burst of Fire.");
        Line(15, 3, "You hit a pledge familiar for 14 points of fire damage by Burst of Fire.");
        Line(15, 3, "a pledge familiar hit you for 70 points of fire damage by Fire Bolt.");
        Line(15, 5, "Your Polished Mithril Mask (Exaltation) feels alive with power.");
        Line(15, 5, "You hit a pledge familiar for 325 points of fire damage by Bolt of Flame.");
        Line(15, 45, "You hit a pledge familiar for 135 points of fire damage by Bolt of Flame.");

        var s = stats.Snapshot();
        Assert.Equal(2, s.Procs.Count);
        var withItem = Assert.Single(s.Procs,
            p => p.Name == "Bolt of Flame · Polished Mithril Mask (Exaltation)");
        Assert.Equal((1, 325L), (withItem.Count, withItem.Damage));
        var bare = Assert.Single(s.Procs, p => p.Name == "Bolt of Flame");
        Assert.Equal((1, 135L), (bare.Count, bare.Damage));
        // The cast nuke and the enemy's own Fire Bolt stay out.
        Assert.DoesNotContain(s.Procs, p => p.Name.Contains("Burst of Fire"));
        Assert.DoesNotContain(s.Procs, p => p.Name.Contains("Fire Bolt"));
    }

    [Fact]
    public void RegenOverrideOutranksTheWikiBaseAndNoCastMeansCountOnly()
    {
        // Instruments raised the real tick past base — the player typed 16 in Options.
        var stats = new SessionStats { RegenPerTickOverride = 16 };
        void Line(int ss, string msg) =>
            stats.Apply(LogParser.Parse($"[Thu Aug 06 14:43:{ss:D2} 2026] {msg}")!);
        Line(24, "You begin singing Hymn of Restoration.");
        Line(30, "Your wounds begin to heal.");
        Line(36, "Your wounds begin to heal.");
        Assert.Equal(32, stats.Snapshot().RegenEstimatedHealed);

        // A buff cast before the log began: ticks arrive with no attributing cast.
        var cold = new SessionStats();
        cold.Apply(LogParser.Parse("[Thu Aug 06 14:43:30 2026] Your wounds begin to heal.")!);
        var s = cold.Snapshot();
        Assert.Equal(1, s.RegenTicks);
        Assert.Equal(0, s.RegenEstimatedHealed);
        Assert.Equal("", s.RegenSpell);
    }

    private static SessionStats Replay(string? characterName, params string[] lines)
    {
        var stats = new SessionStats { CharacterName = characterName };
        foreach (var line in lines)
        {
            var evt = LogParser.Parse(line);
            if (evt is not null) stats.Apply(evt);
        }
        return stats;
    }

    private static string At(int mm, int ss, string msg) =>
        $"[Sat Jul 18 15:{mm:D2}:{ss:D2} 2026] {msg}";

    /// <summary>"You hurt yourself" (HP-cost casting, falls, drowning) counts as damage
    /// taken — but must not open a combat window or an encounter. A necro spending HP on
    /// spells, or a long swim, is not a fight, and treating it as one dilutes DPS with
    /// phantom combat seconds.</summary>
    [Fact]
    public void SelfDamageCountsButDoesNotStartCombat()
    {
        var s = Replay("Dranak",
            At(0, 0, "You hurt yourself for 27 points."),
            At(0, 2, "You hurt yourself for 27 points.")).Snapshot();

        Assert.Equal(54, s.DamageTaken);
        var self = Assert.Single(s.DamageByAttacker);
        Assert.Equal("Yourself", self.Name);
        Assert.Equal(54, self.Total);
        Assert.Equal(0, s.CombatSeconds);
        Assert.Empty(s.RecentEncounters);
    }

    /// <summary>Capped factions ("could not possibly get any better/worse") show up on the
    /// Faction card as capped instead of silently not moving. Capped is sticky: a faction
    /// that climbed earlier in the session and then hit the cap reports both.</summary>
    [Fact]
    public void FactionAtTheCapIsReportedAsCapped()
    {
        var s = Replay("Hugzee",
            At(0, 0, "Your faction standing with Storm Guard has been adjusted by 2."),
            At(0, 5, "Your faction standing with Storm Guard could not possibly get any better."),
            At(0, 6, "Your faction standing with Crushbone Orcs could not possibly get any worse.")).Snapshot();

        var storm = Assert.Single(s.Faction, f => f.Faction == "Storm Guard");
        Assert.Equal((2, 2, true), (storm.Hits, storm.Net, storm.Capped));
        var orcs = Assert.Single(s.Faction, f => f.Faction == "Crushbone Orcs");
        Assert.Equal((1, 0, true), (orcs.Hits, orcs.Net, orcs.Capped));

        // Direction matters (#86, elderbit): the FLOOR is "bottomed", not "maxed" —
        // calling Crushbone Orcs' minimum "maxed" read exactly backwards.
        Assert.Equal("+2 · maxed", EQBuddy.UI.Shared.FactionFormat.Net(storm));
        Assert.Equal("bottomed", EQBuddy.UI.Shared.FactionFormat.Net(orcs));
        Assert.Equal("-1", EQBuddy.UI.Shared.FactionFormat.Net(new FactionDetail("Any", 1, -1)));
        Assert.Equal("-30 · bottomed", EQBuddy.UI.Shared.FactionFormat.Net(
            new FactionDetail("Any", 5, -30, Capped: true, CappedDown: true)));
    }

    /// <summary>HoT ticks land in healing received like any other incoming heal.</summary>
    [Fact]
    public void HealOverTimeCountsAsHealingReceived()
    {
        var s = Replay("Hugzee",
            At(0, 0, "Aenari healed you over time for 8 hit points by Echoing Light."),
            At(0, 6, "Aenari healed you over time for 8 hit points by Echoing Light.")).Snapshot();

        Assert.Equal(16, s.HealingReceived);
    }

    /// <summary>Rune gains show up as healing received under "Rune", and the block
    /// counter tracks how many incoming melee hits the rune ate in a row before one got
    /// through — the streak resets the moment real melee damage lands, and the running
    /// max remembers the best streak of the session.</summary>
    [Fact]
    public void RuneGainsAndBlockStreakAreTracked()
    {
        var s = Replay("Tickel",
            At(0, 0, "You gain a rune for 8 points of absorption."),
            At(0, 1, "You gain a rune for 5 points of absorption."),
            At(0, 2, "A froglok shin knight tries to hit YOU, but YOUR magical skin absorbs the blow!"),
            At(0, 3, "A froglok shin knight tries to bash YOU, but YOUR magical skin absorbs the blow!"),
            At(0, 4, "A froglok shin knight hits YOU for 4 points of damage."),
            At(0, 5, "A froglok shin knight tries to hit YOU, but YOUR magical skin absorbs the blow!")).Snapshot();

        Assert.Equal((2, 13), (s.RuneGainCount, s.RuneGainPoints));
        Assert.Equal(13, s.HealingReceived);
        var rune = Assert.Single(s.HealsByHealer, h => h.Name == "Rune");
        Assert.Equal((2, 13), (rune.Hits, rune.Total));

        Assert.Equal(3, s.RuneBlockCount);
        Assert.Equal(2, s.RuneBlockStreakMax);   // two blocks before the hit landed
        Assert.Equal(1, s.RuneBlockStreak);      // one block since
    }

    [Fact]
    public void PetDamageAndKillsCreditedToPlayer()
    {
        var s = Replay("Kaybek",
            At(0, 0, "Jibekn told you, 'Attacking orc centurion Master.'"),
            At(0, 2, "Jibekn hits orc centurion for 12 points of damage."),
            At(0, 4, "Jibekn hit orc centurion for 11 points of magic damage by Lifespike."),
            At(0, 6, "Orc centurion has been slain by Jibekn!")).Snapshot();

        Assert.Equal(23, s.DamageDealt);
        Assert.Equal(1, s.YourKillCount);
        Assert.Empty(s.PartyKillsByKiller);
        var pet = Assert.Single(s.DamageBySource, d => d.Name == "Pet (Jibekn)");
        Assert.Equal(23, pet.Total);
    }

    /// <summary>The leader response claims the pet before it ever swings — the point of
    /// parsing it, since it can be macro'd into the summon — but it is not a fight, so it
    /// must not open a combat window and dilute DPS with idle seconds.</summary>
    [Fact]
    public void PetLeaderClaimsTheDamageWithoutStartingCombat()
    {
        var stats = Replay("Vataro", At(0, 0, "Genektik says, 'My leader is Vataro.'"));
        Assert.Equal(0, stats.Snapshot().CombatSeconds);   // claimed, but nothing is fighting

        stats.Apply(LogParser.Parse(At(0, 30, "Genektik hits orc centurion for 12 points of damage."))!);
        var s = stats.Snapshot();
        var pet = Assert.Single(s.DamageBySource, d => d.Name == "Pet (Genektik)");
        Assert.Equal(12, pet.Total);
        Assert.Equal(1, s.CombatSeconds);   // the swing, not the half-minute back to the claim
    }

    /// <summary>The leader line rides the broadcast say channel, so a nearby player's pet
    /// answering their own /pet leader lands in our log too — the owner's name is the only
    /// thing separating them. _petName is a single slot: honouring a stranger's line would
    /// swap our pet's damage out for theirs.</summary>
    [Fact]
    public void PetLeaderClaimsOnlyForTheWatchedCharacter()
    {
        var s = Replay("Vataro",
            At(0, 0, "Genektik says, 'My leader is Vataro.'"),
            At(0, 2, "Genektik hits orc centurion for 12 points of damage."),
            At(0, 4, "Xykon says, 'My leader is Kaybek.'"),   // a groupmate's pet
            At(0, 6, "Xykon hits a gnoll for 99 points of damage."),
            At(0, 8, "Genektik hits orc centurion for 8 points of damage.")).Snapshot();

        var pet = Assert.Single(s.DamageBySource, d => d.Name == "Pet (Genektik)");
        Assert.Equal(20, pet.Total);
        Assert.DoesNotContain(s.DamageBySource, d => d.Name.Contains("Xykon"));
        Assert.Equal(20, s.DamageDealt);   // the stranger's 99 stayed out
    }

    /// <summary>Without a character name to check against there is nothing to verify, and
    /// an unverifiable claim is not one we take.</summary>
    [Fact]
    public void PetLeaderIsIgnoredWhenTheCharacterIsUnknown()
    {
        var s = Replay(null,
            At(0, 0, "Genektik says, 'My leader is Vataro.'"),
            At(0, 2, "Genektik hits orc centurion for 12 points of damage.")).Snapshot();

        Assert.DoesNotContain(s.DamageBySource, d => d.Name.Contains("Genektik"));
    }

    [Fact]
    public void CharmBlinkIsProvisionalUntilMasterTellThenMerges()
    {
        var stats = Replay("Douglas",
            At(0, 0, "a puma blinks."),
            At(0, 2, "A puma slashes a ghoul for 11 points of damage."));
        var provisional = stats.Snapshot();
        Assert.Single(provisional.DamageBySource, d => d.Name == "Pet? (Puma)");

        stats.Apply(LogParser.Parse(At(0, 4, "A puma told you, 'Attacking a ghoul Master.'"))!);
        stats.Apply(LogParser.Parse(At(0, 6, "A puma slashes a ghoul for 5 points of damage."))!);
        var confirmed = stats.Snapshot();
        Assert.DoesNotContain(confirmed.DamageBySource, d => d.Name == "Pet? (Puma)");
        var pet = Assert.Single(confirmed.DamageBySource, d => d.Name == "Pet (Puma)");
        Assert.Equal(16, pet.Total);
    }

    [Fact]
    public void CharmBreakStopsCrediting()
    {
        var s = Replay("Douglas",
            At(0, 0, "A puma told you, 'Attacking a ghoul Master.'"),
            At(0, 2, "A puma slashes a ghoul for 10 points of damage."),
            At(0, 4, "A puma slashes YOU for 7 points of damage."),   // charm broke
            At(0, 6, "A puma slashes a ghoul for 99 points of damage.")).Snapshot();

        Assert.Equal(10, s.DamageDealt);   // the 99 after the break is not ours
        Assert.Equal(7, s.DamageTaken);
    }

    [Fact]
    public void SelfHealCountsAsDoneAndReceived()
    {
        var s = Replay("Douglas",
            At(0, 0, "You healed Douglas for 66 hit points by Light Healing.")).Snapshot();
        Assert.Equal(66, s.HealingDone);
        Assert.Equal(66, s.HealingReceived);
        var by = Assert.Single(s.HealsByHealer);
        Assert.Equal("Yourself", by.Name);
        var spell = Assert.Single(s.HealsBySpell);
        Assert.Equal(("Light Healing", 66L), (spell.Name, spell.Total));
    }

    [Fact]
    public void HealOnOthersIsDoneOnly()
    {
        var s = Replay("Caybin",
            At(0, 0, "You healed Douglas for 66 hit points by Light Healing.")).Snapshot();
        Assert.Equal(66, s.HealingDone);
        Assert.Equal(0, s.HealingReceived);
    }

    /// <summary>The stat-block trio (#65, Frankthetankk): zone recorded AT KILL TIME,
    /// coin drops kept as per-kill min/max (the wiki's own low–high format), and
    /// faction hits per creature — including their count against the kill count, so
    /// a confirmed absence is visible too.</summary>
    [Fact]
    public void MobsCarryZoneCoinRangeAndFactionHits()
    {
        var s = Replay("Caybin",
            At(0, 0, "You have entered The Warrens."),
            At(0, 10, "You have slain a kobold looter!"),
            At(0, 11, "You receive 2 silver and 2 copper from the corpse."),
            At(0, 12, "Your faction standing with Kobolds of Fireclaw has been adjusted by -5."),
            // Second kill: richer purse, same faction line.
            At(5, 0, "You have slain a kobold looter!"),
            At(5, 1, "You receive 1 gold from the corpse."),
            At(5, 2, "Your faction standing with Kobolds of Fireclaw has been adjusted by -5."),
            // Zone change after the kills must NOT rewrite where they happened.
            At(6, 0, "You have entered The Northern Desert of Ro.")).Snapshot();

        var mob = Assert.Single(s.Mobs);
        Assert.Equal("The Warrens", mob.Zone);
        Assert.Equal(22, mob.CoinMin);    // 2s 2c in copper
        Assert.Equal(100, mob.CoinMax);   // 1g in copper
        var faction = Assert.Single(mob.Factions);
        Assert.Equal(("Kobolds of Fireclaw", -5, 2), (faction.Faction, faction.Delta, faction.Hits));
    }

    [Fact]
    public void MobsWithNoFactionLinesReportEmptyFactionList()
    {
        var s = Replay("Caybin",
            At(0, 0, "You have entered The Warrens."),
            At(0, 10, "You have slain a kobold looter!")).Snapshot();
        Assert.Empty(Assert.Single(s.Mobs).Factions);
        Assert.Equal(-1, s.Mobs[0].CoinMin);   // no coin seen ≠ zero-coin drops
    }

    /// <summary>The divine invocation heals the party's lowest-health member for the
    /// mana of whatever you cast — a proc whose heal line names no spell, which used
    /// to bucket as "Unknown" in the HPS tracker (David, 2026-08-09). While the divine
    /// invocation is being recited, the unattributed heal belongs to it.</summary>
    [Fact]
    public void UnattributedHealDuringDivineInvocationIsTheInvocations()
    {
        var s = Replay("Caybin",
            At(0, 0, "You begin reciting the divine invocation."),
            At(0, 5, "You healed Douglas for 120 hit points.")).Snapshot();
        var spell = Assert.Single(s.HealsBySpell);
        Assert.Equal(("Divine Invocation", 120L), (spell.Name, spell.Total));
    }

    /// <summary>The same bare heal line without the invocation stays honestly Unknown —
    /// a clicky or another proc must not borrow the invocation's name.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("You begin reciting the empowering invocation.")]
    public void UnattributedHealWithoutDivineInvocationStaysUnknown(string? invocationLine)
    {
        var lines = invocationLine is null
            ? new[] { At(0, 5, "You healed Douglas for 120 hit points.") }
            : [At(0, 0, invocationLine), At(0, 5, "You healed Douglas for 120 hit points.")];
        var s = Replay("Caybin", lines).Snapshot();
        var spell = Assert.Single(s.HealsBySpell);
        Assert.Equal("Unknown", spell.Name);
    }

    [Fact]
    public void CombatWindowNotStartedByBystanders()
    {
        var s = Replay("Kaybek",
            At(0, 0, "Lizzid slashes orc centurion for 4 points of damage."),
            At(0, 5, "Lizzid slashes orc centurion for 4 points of damage.")).Snapshot();
        Assert.Equal(0, s.CombatSeconds);
        Assert.Equal(0, s.SessionDps);
    }

    [Fact]
    public void BystandersExtendOpenWindowWithinGrace()
    {
        // Own hit opens the window; group activity 8s later keeps it alive;
        // our second hit at 16s stays in the same window → 17 combat seconds (0..16).
        var s = Replay("Kaybek",
            At(0, 0, "You slash orc pawn for 10 points of damage."),
            At(0, 8, "Lizzid slashes orc pawn for 4 points of damage."),
            At(0, 16, "You slash orc pawn for 10 points of damage.")).Snapshot();
        Assert.Equal(16, s.CombatSeconds, 0);
        Assert.Equal(20.0 / 16, s.SessionDps, 1);
    }

    [Fact]
    public void QuietGapClosesCombatWindow()
    {
        // Two 1-second fights separated by 5 minutes → 2 combat seconds total.
        var s = Replay("Kaybek",
            At(0, 0, "You slash orc pawn for 10 points of damage."),
            At(0, 1, "You slash orc pawn for 10 points of damage."),
            At(6, 0, "You slash orc pawn for 20 points of damage."),
            At(6, 1, "You slash orc pawn for 20 points of damage.")).Snapshot();
        Assert.Equal(2, s.CombatSeconds, 0);
        Assert.Equal(30, s.SessionDps, 0);
    }

    [Fact]
    public void SessionRollsOverAfterHourGap()
    {
        var stats = Replay("Kaybek", At(0, 0, "You have slain orc pawn!"));
        var rolled = false;
        stats.SessionRolledOver += () => rolled = true;
        stats.Apply(LogParser.Parse("[Sat Jul 18 17:30:00 2026] You have slain orc centurion!")!);
        var s = stats.Snapshot();
        Assert.True(rolled);
        Assert.Equal(1, s.YourKillCount);
        Assert.Equal("Orc centurion", Assert.Single(s.YourKills).Name);
    }

    [Fact]
    public void AvoidanceAndCritRateInputs()
    {
        var s = Replay("Kaybek",
            At(0, 0, "You slash orc pawn for 10 points of damage. (Critical)"),
            At(0, 1, "You slash orc pawn for 10 points of damage."),
            At(0, 2, "Orc pawn hits YOU for 3 points of damage."),
            At(0, 3, "Orc pawn tries to hit YOU, but misses!"),
            At(0, 4, "Orc pawn tries to hit YOU, but YOU dodge!"),
            At(0, 5, "You have taken 2 damage from Rabies by Orc pawn.")).Snapshot();

        Assert.Equal(2, s.HitCount);
        Assert.Equal(1, s.CritCount);
        Assert.Equal(2, s.AvoidedIncoming);
        Assert.Equal(1, s.MeleeHitsTaken);   // spell/DoT damage taken is not an avoidable swing
        Assert.Equal(5, s.DamageTaken);
    }

    [Fact]
    public void AbilityActiveTimeApproximatesPerAbilityRate()
    {
        // Consecutive hits within 10s accumulate real spacing; isolated hits count 2.5s.
        var s = Replay("Kaybek",
            At(0, 0, "You slash orc pawn for 10 points of damage."),
            At(0, 3, "You slash orc pawn for 10 points of damage."),
            At(0, 6, "You slash orc pawn for 10 points of damage."),
            At(0, 10, "You kick orc pawn for 5 points of damage."),
            At(0, 50, "You kick orc pawn for 5 points of damage.")).Snapshot();
        // Slash: 2.5 (first) + 3 + 3 = 8.5s active.
        Assert.Equal(8.5, s.DamageBySource.Single(d => d.Name == "Slash").ActiveSeconds, 3);
        // Kick: two isolated hits = 2.5 + 2.5 = 5s active.
        Assert.Equal(5.0, s.DamageBySource.Single(d => d.Name == "Kick").ActiveSeconds, 3);
    }

    [Fact]
    public void HealsBySpellTrackActiveTime()
    {
        var s = Replay("Douglas",
            At(0, 0, "You healed Zumm for 30 hit points by Light Healing."),
            At(0, 4, "You healed Zumm for 30 hit points by Light Healing.")).Snapshot();
        Assert.Equal(6.5, s.HealsBySpell.Single().ActiveSeconds, 3);   // 2.5 + 4
    }

    [Fact]
    public void DamageBySourceTracksPerSourceCrits()
    {
        var s = Replay("Kaybek",
            At(0, 0, "You slash orc pawn for 10 points of damage. (Critical)"),
            At(0, 1, "You slash orc pawn for 10 points of damage."),
            At(0, 2, "You kick orc pawn for 5 points of damage.")).Snapshot();
        var slash = s.DamageBySource.Single(d => d.Name == "Slash");
        Assert.Equal((2, 1), (slash.Hits, slash.Crits));
        Assert.Equal(0, s.DamageBySource.Single(d => d.Name == "Kick").Crits);
    }

    [Fact]
    public void AutoSellIsVendorIncomeAndAMobDropButNeverSessionLoot()
    {
        // "Looted … and sold it for …" means the pickup was dismissed at the corpse:
        // it never reached the bags, so it is not session loot (LW, 2026-08-17 — a
        // (Sold) tag + show filter was tried first and crowded the views). The coin
        // is vendor income and the creature's drop ledger still records the drop.
        var s = Replay("Douglas",
            At(0, 0, "You looted 2 Spider Silk from a giant spider's corpse and sold it for 2 gold, 8 silver and 6 copper.")).Snapshot();
        Assert.Equal(0, s.LootTotal);
        Assert.Empty(s.Loot);
        Assert.Empty(s.RecentLoot);
        Assert.Equal(286, s.VendorCopper);
        Assert.Equal(286, s.Copper);
        var sold = Assert.Single(s.SoldItems);
        Assert.Equal(("Spider Silk", 2), (sold.Item, sold.Count));
        Assert.Contains(s.Mobs.Single(m => m.Name == "Giant spider").Loot,
            ml => ml.Item == "Spider Silk" && ml.Count == 2);
    }

    [Fact]
    public void RecentLootCarriesEveryKeptAcquisitionKindInArrivalOrder()
    {
        // The recent view must agree with the count view about WHAT counts as loot —
        // kept corpse loot, auto-stored loot, and crafts all ride the timeline; the
        // auto-sold spear is absent from BOTH, not just one (the original bug was the
        // views disagreeing — LW's kobold session, 2026-08-17).
        var s = Replay("Robin",
            At(0, 0, "--You have looted a Death Cap from a greater kobold's corpse.--"),
            At(0, 30, "You looted a Fine Steel Spear from a greater kobold's corpse and sold it for 4 platinum, 5 gold, 7 silver and 1 copper."),
            At(1, 0, "You looted a Mote of Lesser Potential from a greater kobold's corpse and stored it in your currency"),
            At(1, 30, "You have fashioned the items together to create something new: Cloudy Potion.")).Snapshot();

        Assert.Equal(
            ["Cloudy Potion", "Mote of Lesser Potential", "Death Cap"],
            s.RecentLoot.Select(p => p.Item).ToList());
        Assert.DoesNotContain(s.Loot, l => l.Item == "Fine Steel Spear");
        // The dismissal is view-side only — the per-creature drop ledger (wiki packs,
        // target drops) still credits the kobold with the auto-sold drop.
        Assert.Contains(s.Mobs.Single(m => m.Name == "Greater kobold").Loot,
            ml => ml.Item == "Fine Steel Spear");
    }

    [Fact]
    public void LootWindowSaleCountsAsVendorIncomeNamedByDestroyLine()
    {
        // Selling from the advanced loot window logs a destroy line + an anonymous
        // "from that item" money line; the pair must become one named vendor sale.
        var s = Replay("Douglas",
            At(0, 0, "You successfully destroyed 1 Spider Venom Sac."),
            At(0, 0, "You received 3 gold, 5 silver and 7 copper from that item.")).Snapshot();
        Assert.Equal(357, s.VendorCopper);
        Assert.Equal(1, s.SalesCount);
        var sold = Assert.Single(s.SoldItems);
        Assert.Equal("Spider Venom Sac", sold.Item);
        Assert.Equal(1, sold.Count);
        Assert.Equal(357, sold.Copper);
    }

    [Fact]
    public void LootWindowSaleWithoutDestroyLineStillCountsIncome()
    {
        var s = Replay("Douglas",
            At(0, 0, "You received 2 silver from that item.")).Snapshot();
        Assert.Equal(20, s.VendorCopper);
        Assert.Equal("Loot window sale", Assert.Single(s.SoldItems).Item);
    }

    /// <summary>Crafted ("made") items feed the Loot breakout's per-item Made section
    /// (#131, TropicMike): ordered biggest-count-first like the loot rows, totaled for
    /// the "+N made" header — and never mixed into the looted count, because a merge
    /// creates from held items rather than taking from a corpse.</summary>
    [Fact]
    public void MergesCountPerItemOrderedByCountAndStayOutOfLoot()
    {
        var s = Replay("Douglas",
            At(0, 0, "You have successfully merged two items together to create a new item: Crushbone Belt +7"),
            At(1, 0, "You have successfully merged two items together to create a new item: Polished Bone Ring"),
            At(2, 0, "You have successfully merged two items together to create a new item: Crushbone Belt +7"),
            At(3, 0, "--You have looted a Bone Chips from a decaying skeleton's corpse.--")).Snapshot();

        Assert.Equal(3, s.CraftedTotal);
        Assert.Equal(["Crushbone Belt +7", "Polished Bone Ring"],
            s.Crafted.Select(c => c.Name));
        Assert.Equal([2, 1], s.Crafted.Select(c => c.Count));
        Assert.Equal(1, s.LootTotal);   // the merge results never inflate looted items
    }

    [Fact]
    public void DamageShieldExcludedFromAccuracyButCounted()
    {
        var s = Replay("Douglas",
            At(0, 0, "Orc centurion is burned by YOUR flames for 5 points of non-melee damage.")).Snapshot();
        Assert.Equal(5, s.DamageDealt);
        Assert.Equal(0, s.HitCount);
        Assert.Equal(0, s.CritCount);
    }

    [Fact]
    public void XpLevelAndEta()
    {
        var stats = Replay("Caybin",
            At(0, 0, "You gain party experience! (30%)"),
            At(30, 0, "You have gained a level! Welcome to level 6!"),
            At(30, 1, "You gain party experience! (25%)"),
            At(59, 0, "You gain party experience! (25%)"));
        var s = stats.Snapshot();
        Assert.Equal(80, s.XpPercent, 1);
        Assert.Single(s.Levels);
        Assert.Equal(6, s.LastLevel);   // the level-unlock views key off the number
        Assert.NotNull(s.HoursToLevel);
        // 50% into level 6, earning 80% per 59 min → ~0.61h remaining
        Assert.InRange(s.HoursToLevel!.Value, 0.55, 0.68);
    }

    // ---- deaths ----

    /// <summary>Transcribed from eqlog_Hugzee 2026-07-29 15:59:01 — a death by
    /// damage-over-time, where the log names no killer. Whatever last hurt us takes the
    /// blame, which for a DoT death is the caster of the finishing tick.</summary>
    [Fact]
    public void ADeathWithNoNamedKillerBlamesTheLastAttacker()
    {
        var stats = Replay("Hugzee",
            At(59, 0, "Orc oracle crushes YOU for 11 points of damage."),
            At(59, 1, "You have taken 25 damage from Heat Blood by orc oracle."),
            At(59, 1, "You have been knocked unconscious!"),
            At(59, 1, "You died."));

        var death = Assert.Single(stats.Snapshot().Deaths);
        Assert.Equal("Orc oracle", death.Text);
    }

    /// <summary>When the log does name the killer, that wins — no guessing from damage.</summary>
    [Fact]
    public void ANamedKillerIsUsedAsIs()
    {
        var stats = Replay("Dranak",
            At(59, 0, "Orc oracle crushes YOU for 11 points of damage."),
            At(59, 1, "You have been knocked unconscious!"),
            At(59, 1, "You have been slain by Guard Dunil!"));

        Assert.Equal("Guard Dunil", Assert.Single(stats.Snapshot().Deaths).Text);
    }

    /// <summary>Nothing recent to blame — say so rather than showing an empty row.</summary>
    [Fact]
    public void ADeathWithNothingToBlameSaysSomething()
    {
        var stats = Replay("Hugzee",
            At(0, 0, "Orc oracle crushes YOU for 11 points of damage."),
            At(59, 0, "You died."));

        Assert.Equal("Something", Assert.Single(stats.Snapshot().Deaths).Text);
    }

    /// <summary>One death, not two: the unconscious line precedes both death forms.</summary>
    [Fact]
    public void KnockedUnconsciousDoesNotDoubleCount()
    {
        var stats = Replay("Hugzee",
            At(59, 1, "You have been knocked unconscious!"),
            At(59, 1, "You died."));

        Assert.Single(stats.Snapshot().Deaths);
    }

    /// <summary>Review replay is read-only (#74, audit finding 4): with
    /// StoresSuppressed set, the durable ledgers ignore replayed events — the
    /// in-session view still shows them — and lifting the flag resumes recording.
    /// (MainWindow sets the flag for the duration of review; that WPF wiring isn't
    /// hostable here, so the seam it relies on is what's pinned.)</summary>
    [Fact]
    public void SuppressedStoresIgnoreReviewReplay()
    {
        var dir = Directory.CreateTempSubdirectory("eqbuddy-stores-").FullName;
        try
        {
            var store = new AaLedgerStore(Path.Combine(dir, "aa-ledger.json"));
            var stats = new SessionStats
            {
                CharacterName = "Kaybek", ServerName = "freeport",
                AaStore = store, StoresSuppressed = true,
            };
            void Line(int mm, string msg) =>
                stats.Apply(LogParser.Parse($"[Sat Jul 18 15:{mm:D2}:00 2026] {msg}")!);

            Line(0, "You have gained the ability \"Quick Buff\" at a cost of 5 ability points.");
            Assert.Empty(store.For("kaybek_freeport"));   // nothing written through
            Assert.Contains(stats.Snapshot().AaAbilities, a => a.Name == "Quick Buff");

            stats.StoresSuppressed = false;               // back to live
            Line(5, "You have gained the ability \"Combat Fury\" at a cost of 3 ability points.");
            var recorded = store.For("kaybek_freeport");
            Assert.True(recorded.ContainsKey("Combat Fury"));
            Assert.False(recorded.ContainsKey("Quick Buff"));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }
}
