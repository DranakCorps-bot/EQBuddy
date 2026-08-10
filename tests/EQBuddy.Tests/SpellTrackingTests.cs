using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Spell tracking: cast lines, classification, and the charm lifecycle.
///
/// The charm sequences below are transcribed from a real EQ Legends log
/// (eqlog_Douglas_qeynos, 2026-07-20) — both the success path and the interrupted cast
/// that must NOT produce a pet.
/// </summary>
public class SpellTrackingTests
{
    private const string Ts = "[Sat Jul 18 15:39:13 2026] ";

    private static SessionStats Replay(params string[] lines)
    {
        var stats = new SessionStats { CharacterName = "Douglas" };
        foreach (var line in lines)
        {
            var evt = LogParser.Parse(line);
            if (evt is not null) stats.Apply(evt);
        }
        return stats;
    }

    private static string At(int mm, int ss, string msg) =>
        $"[Sat Jul 18 15:{mm:D2}:{ss:D2} 2026] {msg}";

    // ---- parsing ----

    [Theory]
    [InlineData("You begin casting Stinging Swarm.", "Stinging Swarm")]
    [InlineData("You begin casting Befriend Animal.", "Befriend Animal")]
    [InlineData("You begin casting Stinging Swarm V.", "Stinging Swarm V")]
    [InlineData("You begin casting Succor: East Karana.", "Succor: East Karana")]
    [InlineData("You begin singing Chords of Dissonance.", "Chords of Dissonance")]
    public void CastStartParsed(string msg, string spell) =>
        Assert.Equal(spell, Assert.IsType<SpellCastEvent>(LogParser.Parse(Ts + msg)).Spell);

    [Fact]
    public void CastInterruptedParsed() =>
        Assert.Equal("Stinging Swarm", Assert.IsType<SpellInterruptedEvent>(
            LogParser.Parse(Ts + "Your Stinging Swarm spell is interrupted.")).Spell);

    [Fact]
    public void FizzleCarriesSpellName() =>
        Assert.Equal("Befriend Animal", Assert.IsType<FizzleEvent>(
            LogParser.Parse(Ts + "Your Befriend Animal spell fizzles!")).Spell);

    [Fact]
    public void ResistCarriesSpellName() =>
        Assert.Equal("Denon's Disruptive Discord", Assert.IsType<ResistEvent>(
            LogParser.Parse(Ts + "A willowisp resisted your Denon's Disruptive Discord!")).Spell);

    [Fact]
    public void DotTicksAreFlaggedOverTimeAndDirectHitsAreNot()
    {
        Assert.True(Assert.IsType<DamageDealtEvent>(
            LogParser.Parse(Ts + "Orc centurion has taken 10 damage from your Stinging Swarm.")).OverTime);
        Assert.False(Assert.IsType<DamageDealtEvent>(
            LogParser.Parse(Ts + "You hit orc centurion for 13 points of fire damage by Burn.")).OverTime);
    }

    /// <summary>Cast lines for other entities are deliberately not parsed — EQBuddy stays
    /// a single-character tool, so another player's cast line is ignored.
    /// (Names sanitized per CONTRIBUTING — these lines are real in shape only.)</summary>
    [Fact]
    public void OtherEntitiesCastsAreNotOwnCasts()
    {
        // Since the mez tracker these parse as OtherCastEvent (they carry spell + rank,
        // which is what lets a bystander's EQBuddy attribute a group member's mez) —
        // but they must never count toward the PLAYER's cast statistics.
        var other = Assert.IsType<OtherCastEvent>(
            LogParser.Parse(Ts + "Otherchar begins casting Tame Spirit."));
        Assert.Equal("Otherchar", other.Caster);
        Assert.IsType<OtherCastEvent>(
            LogParser.Parse(Ts + "Otherchar`s warder begins casting Minor Healing."));

        var stats = new SessionStats();
        stats.Apply(LogParser.Parse(Ts + "Otherchar begins casting Tame Spirit.")!);
        Assert.Equal(0, stats.Snapshot().CastsStarted);
    }

    /// <summary>Real line from a mage log. Without its own pattern the general worn-off
    /// regex captures the spell as "pet's Tangling Weeds" and, worse, lets the pet's spell
    /// trigger the player's spell-fade rules.</summary>
    [Fact]
    public void ThePetsOwnSpellFadingIsAttributedToThePet()
    {
        var e = Assert.IsType<SpellWornOffEvent>(
            LogParser.Parse(Ts + "Your pet's Tangling Weeds spell has worn off."));
        Assert.True(e.Pet);
        Assert.Equal("Tangling Weeds", e.Spell);

        var yours = Assert.IsType<SpellWornOffEvent>(
            LogParser.Parse(Ts + "Your Befriend Animal spell has worn off of a puma."));
        Assert.False(yours.Pet);
    }

    [Fact]
    public void APetsSpellFadingNeverFiresTheAnySpellRule()
    {
        var rule = new TrackedRule
        {
            Name = "Anything dropped", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.AnySpell,
        };
        var s = Replay(
            At(0, 0, "Your pet's Tangling Weeds spell has worn off."),
            At(0, 5, "Your Befriend Animal spell has worn off of a puma."))
            .Snapshot(recentWindow: null, rules: [rule]);

        var tracked = Assert.Single(s.Tracked);
        Assert.Equal(1, tracked.TotalQuantity);
        Assert.Equal("Befriend Animal (Puma)", tracked.LastItem);
    }

    // ---- classification ----

    [Theory]
    [InlineData("Stinging Swarm V", "Stinging Swarm")]
    [InlineData("Light Healing V", "Light Healing")]
    [InlineData("Heroic Leap I", "Heroic Leap")]
    [InlineData("Befriend Animal", "Befriend Animal")]
    [InlineData("Chords of Dissonance", "Chords of Dissonance")]
    public void RankSuffixesCollapseOntoTheBaseName(string spell, string expected) =>
        Assert.Equal(expected, SpellCatalog.BaseName(spell));

    [Fact]
    public void RankedCharmStillClassifiesAsCharm()
    {
        var catalog = new SpellCatalog();
        Assert.Equal(SpellCategory.Charm, catalog.Classify("Befriend Animal"));
        Assert.Equal(SpellCategory.Charm, catalog.Classify("Befriend Animal III"));
        Assert.True(catalog.IsCrowdControl("Befriend Animal III"));
    }

    [Fact]
    public void UnknownSpellsClassifyAsUnknownRatherThanGuessing() =>
        Assert.Equal(SpellCategory.Unknown, new SpellCatalog().Classify("Tame Spirit"));

    // ---- family fragments ----

    /// <summary>EQ names spells in families, so a fragment covers a whole line including
    /// ranks nobody typed into the seed list.</summary>
    [Theory]
    [InlineData("Engorging Roots", SpellCategory.Root)]
    [InlineData("Ensnaring Roots IV", SpellCategory.Root)]
    [InlineData("Paralyzing Earth", SpellCategory.Root)]
    [InlineData("Cajoling Whispers II", SpellCategory.Charm)]
    [InlineData("Beguile Animals", SpellCategory.Charm)]
    [InlineData("Befriend Beast", SpellCategory.Charm)]
    [InlineData("Enthralling Chant", SpellCategory.Mesmerize)]
    [InlineData("Mesmerizing Gaze", SpellCategory.Mesmerize)]
    [InlineData("Pacify the Wild", SpellCategory.Lull)]
    [InlineData("Soothing Words", SpellCategory.Lull)]
    [InlineData("Stunning Flash", SpellCategory.Stun)]
    public void UnlistedSpellsClassifyByFamily(string spell, SpellCategory expected) =>
        Assert.Equal(expected, new SpellCatalog().Classify(spell));

    /// <summary>The ordering trap: Kelin's Lucid Lullaby is a mez, but "Lullaby" contains
    /// "Lull". If the Lull family were tested first this would silently misclassify, and a
    /// "Any CC" rule would still fire — just under the wrong category.</summary>
    [Fact]
    public void LullabyIsAMezNotALull() =>
        Assert.Equal(SpellCategory.Mesmerize, new SpellCatalog().Classify("Kelin's Lucid Lullaby"));

    [Fact]
    public void FamilyMatchingDoesNotInventCategoriesForOrdinarySpells()
    {
        var catalog = new SpellCatalog();
        foreach (var spell in (string[])["Stinging Swarm", "Chords of Dissonance",
                                         "Light Healing", "Burn", "Succor: East Karana"])
            Assert.Equal(SpellCategory.Unknown, catalog.Classify(spell));
    }

    /// <summary>Observation beats a family guess — otherwise a damage spell whose name
    /// happens to contain a CC fragment would be stuck as CC forever.</summary>
    [Fact]
    public void ObservedBehaviourOverridesAFamilyGuess()
    {
        var catalog = new SpellCatalog();
        Assert.Equal(SpellCategory.Stun, catalog.Classify("Stunning Flash"));
        Assert.True(catalog.Learn("Stunning Flash", SpellCategory.DirectDamage));
        Assert.Equal(SpellCategory.DirectDamage, catalog.Classify("Stunning Flash"));
    }

    /// <summary>Seeded names still win, so a curated entry can't be undone by a fragment.</summary>
    [Fact]
    public void SeededNamesBeatFamilyMatching() =>
        Assert.Equal(SpellCategory.Charm, new SpellCatalog().Classify("Befriend Animal"));

    [Fact]
    public void ObservationCannotReclassifyASeededCrowdControlSpell()
    {
        var catalog = new SpellCatalog();
        Assert.False(catalog.Learn("Befriend Animal", SpellCategory.DirectDamage));
        Assert.Equal(SpellCategory.Charm, catalog.Classify("Befriend Animal"));
    }

    [Fact]
    public void LearnedSpellsAreRankInsensitive()
    {
        var catalog = new SpellCatalog();
        Assert.True(catalog.Learn("Stinging Swarm", SpellCategory.DamageOverTime));
        Assert.Equal(SpellCategory.DamageOverTime, catalog.Classify("Stinging Swarm V"));
    }

    // ---- charm lifecycle (real log sequence) ----

    [Fact]
    public void CharmCastBeforeBlinkConfirmsThePetImmediately()
    {
        // Real sequence: cast at 44:06, blink at 44:10. Because the cast is a known charm
        // the pet is certain, so damage lands under "Pet (…)" with no provisional stage.
        var s = Replay(
            At(44, 6, "You begin casting Befriend Animal."),
            At(44, 10, "a giant spider blinks."),
            At(44, 12, "A giant spider hits orc pawn for 14 points of damage.")).Snapshot();

        var pet = Assert.Single(s.DamageBySource, d => d.Name == "Pet (Giant spider)");
        Assert.Equal(14, pet.Total);
        Assert.DoesNotContain(s.DamageBySource, d => d.Name.StartsWith("Pet?"));
    }

    [Fact]
    public void BlinkWithoutACharmCastStaysProvisional()
    {
        // No cast in flight — fall back to the original blink-only guess so this can never
        // be worse than the previous behavior.
        var s = Replay(
            At(0, 0, "a puma blinks."),
            At(0, 2, "A puma hits orc pawn for 9 points of damage.")).Snapshot();

        Assert.Single(s.DamageBySource, d => d.Name == "Pet? (Puma)");
    }

    [Fact]
    public void AnInterruptedCharmNeverClaimsAPet()
    {
        // Real sequence: cast at 03:47, interrupted at 03:51. Nothing was charmed, so a
        // nearby creature's damage must not be credited to the player.
        var s = Replay(
            At(3, 47, "You begin casting Befriend Animal."),
            At(3, 51, "Your Befriend Animal spell is interrupted."),
            At(3, 55, "A giant spider hits orc pawn for 14 points of damage.")).Snapshot();

        Assert.DoesNotContain(s.DamageBySource, d => d.Name.StartsWith("Pet"));
        Assert.Equal(0, s.DamageDealt);
    }

    [Fact]
    public void CharmWearingOffDropsThePetImmediately()
    {
        // Real sequence: charmed at 44:10, worn off at 46:01. Damage after the break
        // belongs to the creature, not to us.
        var s = Replay(
            At(44, 6, "You begin casting Befriend Animal."),
            At(44, 10, "a giant spider blinks."),
            At(44, 12, "A giant spider hits orc pawn for 14 points of damage."),
            At(46, 1, "Your Befriend Animal spell has worn off of a giant spider."),
            At(46, 5, "A giant spider hits orc pawn for 99 points of damage.")).Snapshot();

        Assert.Equal(14, s.DamageDealt);
    }

    [Fact]
    public void AnUnknownCharmSpellIsLearnedFromTheMasterTell()
    {
        // "Tame Spirit" isn't in the seed table. Cast → blink → "Master" tell proves it is
        // a charm, so the next cast of it confirms a pet with no provisional stage.
        var stats = Replay(
            At(0, 0, "You begin casting Tame Spirit."),
            At(0, 4, "an asp blinks."),
            At(0, 9, "An asp told you, 'Attacking orc pawn Master.'"),
            At(1, 0, "Your Tame Spirit spell has worn off of an asp."),
            At(2, 0, "You begin casting Tame Spirit."),
            At(2, 4, "a puma blinks."),
            At(2, 6, "A puma hits orc pawn for 21 points of damage."));

        var s = stats.Snapshot();
        Assert.Single(s.DamageBySource, d => d.Name == "Pet (Puma)");
    }

    /// <summary>Issue #29's bard half: charm SONGS start with "You begin to sing …",
    /// which was never parsed — a bard's pending cast never existed, so no landing
    /// line could correlate and the pet only ever appeared via the attack-button
    /// tell. Songs now count as casts for correlation (Solon's songs are seeded as
    /// charms from Vellum670's list) but stay out of the cast-completion stats.</summary>
    [Fact]
    public void ABardCharmSongClaimsThePetInstantly()
    {
        var stats = Replay(
            At(0, 0, "You begin to sing Solon's Song of the Sirens."),
            At(0, 3, "a gnoll has been charmed."),
            At(0, 5, "A gnoll hits orc pawn for 9 points of damage."));

        var s = stats.Snapshot();
        Assert.Single(s.DamageBySource, d => d.Name == "Pet (Gnoll)");
        Assert.Equal(0, s.CastsStarted);   // twisting must not swamp cast stats
    }

    /// <summary>The necro charm landing is "X moans." (eqlwiki: all three undead
    /// charms) — never parsed before, so necros were attack-button-only. Weak signal:
    /// it acts only behind our own cast, because moaning is plausible ambient flavor.</summary>
    [Fact]
    public void ANecroCharmClaimsOnTheMoanLine()
    {
        var stats = Replay(
            At(0, 0, "You begin casting Dominate Undead."),
            At(0, 4, "a greater skeleton moans."),
            At(0, 6, "A greater skeleton hits orc pawn for 11 points of damage."));
        Assert.Single(stats.Snapshot().DamageBySource, d => d.Name == "Pet (Greater skeleton)");
    }

    [Fact]
    public void AnAmbientMoanWithNoCastClaimsNothing()
    {
        var stats = Replay(
            At(0, 0, "a decaying zombie moans."),
            At(0, 2, "A decaying zombie hits orc pawn for 11 points of damage."));
        Assert.DoesNotContain(stats.Snapshot().DamageBySource, d => d.Name.StartsWith("Pet"));
    }

    /// <summary>"X's eyes glaze over." lands bard CHARM songs and bard MEZ songs with
    /// the identical message (eqlwiki) — only the pending song disambiguates. A charm
    /// song claims the pet; a mez song must not.</summary>
    [Fact]
    public void TheGlazeLineIsACharmBehindACharmSongAndNotOtherwise()
    {
        var charm = Replay(
            At(0, 0, "You begin to sing Solon's Bravura."),
            At(0, 3, "a gnoll's eyes glaze over."),
            At(0, 5, "A gnoll hits orc pawn for 9 points of damage."));
        Assert.Single(charm.Snapshot().DamageBySource, d => d.Name == "Pet (Gnoll)");

        var mez = Replay(
            At(0, 0, "You begin to sing Crission's Pixie Strike."),
            At(0, 3, "a gnoll's eyes glaze over."),
            At(0, 5, "A gnoll hits orc pawn for 9 points of damage."));
        Assert.DoesNotContain(mez.Snapshot().DamageBySource, d => d.Name.StartsWith("Pet"));
    }

    /// <summary>Befriend Animal's break line names no target — "Your charm spell has
    /// worn off." (eqlwiki; unique among animal charms). It must still drop the pet.</summary>
    [Fact]
    public void ATargetlessCharmFadeDropsThePet()
    {
        var stats = Replay(
            At(0, 0, "You begin casting Befriend Animal."),
            At(0, 4, "a puma blinks."),
            At(0, 6, "A puma hits orc pawn for 8 points of damage."),
            At(1, 0, "Your charm spell has worn off."),
            At(1, 5, "A puma hits orc pawn for 8 points of damage."));   // no longer ours

        var pet = stats.Snapshot().DamageBySource.Single(d => d.Name == "Pet (Puma)");
        Assert.Equal(8, pet.Total);   // only the pre-fade hit is credited
    }

    /// <summary>Issue #29: a client whose charms log "X has been charmed." (no blink)
    /// with a spell outside the catalog never learned it — the learning hook only
    /// existed on the blink path — so EVERY charm waited for the attack button. The
    /// charmed line now records the candidate; the tell teaches; the next charm of the
    /// same spell claims instantly.</summary>
    [Fact]
    public void AnUnknownCharmSpellIsLearnedFromTheCharmedLinePlusTell()
    {
        var stats = Replay(
            At(0, 0, "You begin casting Word of Submission."),   // not in any seed/family
            At(0, 2, "an orc legionnaire has been charmed."),    // records the candidate only
            At(0, 9, "An orc legionnaire told you, 'Attacking gnoll Master.'"),  // teaches
            At(1, 0, "Your Word of Submission spell has worn off of an orc legionnaire."),
            At(2, 0, "You begin casting Word of Submission."),
            At(2, 2, "a scorched zombie has been charmed."),     // now instant — no tell yet
            At(2, 4, "A scorched zombie hits gnoll for 15 points of damage."));

        Assert.Single(stats.Snapshot().DamageBySource, d => d.Name == "Pet (Scorched zombie)");
    }

    /// <summary>The leader response teaches the catalog the same way the attack tell does,
    /// and it arrives without waiting for the attack button — which is the whole point of
    /// parsing it. Naming the watched character is what makes it safe to write something
    /// this durable on a bystander-audible channel.</summary>
    [Fact]
    public void AnUnknownCharmSpellIsLearnedFromTheCharmedLinePlusLeaderResponse()
    {
        var stats = Replay(
            At(0, 0, "You begin casting Word of Submission."),   // not in any seed/family
            At(0, 2, "an orc legionnaire has been charmed."),    // records the candidate only
            At(0, 9, "An orc legionnaire says, 'My leader is Douglas.'"),   // teaches
            At(1, 0, "Your Word of Submission spell has worn off of an orc legionnaire."),
            At(2, 0, "You begin casting Word of Submission."),
            At(2, 2, "a scorched zombie has been charmed."),     // now instant — no tell yet
            At(2, 4, "A scorched zombie hits gnoll for 15 points of damage."));

        Assert.Single(stats.Snapshot().DamageBySource, d => d.Name == "Pet (Scorched zombie)");
    }

    /// <summary>A tell about a DIFFERENT creature proves nothing about the held cast —
    /// without the name match, a bystander's charm near our unknown cast plus our real
    /// (summoned) pet's tell would poison the catalog.</summary>
    [Fact]
    public void ATellNamingADifferentCreatureTeachesNothing()
    {
        var stats = Replay(
            At(0, 0, "You begin casting Heroic Leap I."),
            At(0, 2, "a Teir`Dal rogue has been charmed."),      // bystander's charm
            At(0, 9, "Gonarab told you, 'Attacking gnoll Master.'"),  // our summoned pet
            At(1, 0, "You begin casting Heroic Leap I."),
            At(1, 2, "an orc pawn has been charmed."),           // another bystander charm
            At(1, 4, "An orc pawn slashes gnoll for 9 points of damage."));

        // Heroic Leap was not learned as a charm: the second charmed line claims nothing.
        Assert.DoesNotContain(stats.Snapshot().DamageBySource,
            d => d.Name.Contains("Orc pawn"));
    }

    [Fact]
    public void LearnedCategoriesSurviveThroughTheAttachedStore()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eqbuddy-spells-{Guid.NewGuid():N}.json");
        try
        {
            var first = new SpellCatalog();
            first.AttachStore(path);
            Assert.True(first.Learn("Word of Submission", SpellCategory.Charm));

            var second = new SpellCatalog();          // fresh session
            second.AttachStore(path);
            Assert.Equal(SpellCategory.Charm, second.Classify("Word of Submission III"));

            // No store attached (tests, by design): nothing leaks between instances.
            Assert.Equal(SpellCategory.Unknown, new SpellCatalog().Classify("Word of Submission"));
        }
        finally { File.Delete(path); }
    }

    // ---- generic "Your pet" attribution ----

    /// <summary>
    /// A summoned pet that has never been given an attack order emits no
    /// "Attacking … Master." line, so it was invisible — the bug a beastlord player
    /// reported. When the game names it generically instead, no prior identification is
    /// needed: nothing but your own pet is ever called "Your pet".
    /// </summary>
    [Fact]
    public void TheGenericPetFormIsCreditedWithNoMasterTell()
    {
        var s = Replay(
            At(0, 0, "Your pet hits orc pawn for 12 points of damage."),
            At(0, 2, "Your pet hit orc pawn for 8 points of magic damage by Lifespike.")).Snapshot();

        Assert.Equal(20, s.DamageDealt);
        var pet = Assert.Single(s.DamageBySource, d => d.Name == "Pet");
        Assert.Equal(20, pet.Total);
    }

    [Fact]
    public void AGenericPetKillCountsAsYours()
    {
        var s = Replay(
            At(0, 0, "Your pet hits orc pawn for 12 points of damage."),
            At(0, 4, "Orc pawn has been slain by Your pet!")).Snapshot();

        Assert.Equal(1, s.YourKillCount);
        Assert.Empty(s.PartyKillsByKiller);
    }

    /// <summary>The guard that keeps this safe: only the exact generic phrase counts, so
    /// other people's pets and bystanders are still not credited to you.</summary>
    [Fact]
    public void OtherPeoplesCombatIsStillNotCreditedToYou()
    {
        var s = Replay(
            At(0, 0, "Otherchar hits orc pawn for 50 points of damage."),
            At(0, 2, "A giant spider hits orc pawn for 30 points of damage.")).Snapshot();

        Assert.Equal(0, s.DamageDealt);
        Assert.DoesNotContain(s.DamageBySource, d => d.Name.StartsWith("Pet"));
    }

    /// <summary>Once the pet announces itself, damage lands under its name — the generic
    /// form must not fragment one pet's damage into two rows.</summary>
    [Fact]
    public void ANamedPetStillReportsUnderItsName()
    {
        var s = Replay(
            At(0, 0, "Jibekn told you, 'Attacking orc pawn Master.'"),
            At(0, 2, "Jibekn hits orc pawn for 12 points of damage.")).Snapshot();

        Assert.Single(s.DamageBySource, d => d.Name == "Pet (Jibekn)");
        Assert.DoesNotContain(s.DamageBySource, d => d.Name == "Pet");
    }

    // ---- pet ability breakdown ----

    /// <summary>The pet keeps its single damage row; what it used is broken out beside it,
    /// with the melee verb reduced to the same skill label the player's own hits use.</summary>
    [Fact]
    public void PetDamageIsBrokenOutByAbility()
    {
        var s = Replay(
            At(0, 0, "Jibekn told you, 'Attacking orc pawn Master.'"),
            At(0, 2, "Jibekn hits orc pawn for 12 points of damage."),
            At(0, 4, "Jibekn bashes orc pawn for 6 points of damage."),
            At(0, 6, "Jibekn hits orc pawn for 10 points of damage."),
            At(0, 8, "Jibekn hit orc pawn for 8 points of magic damage by Lifespike."),
            At(0, 10, "Orc pawn has taken 3 damage from Poison Bolt by Jibekn.")).Snapshot();

        var pet = Assert.Single(s.DamageBySource, d => d.Name == "Pet (Jibekn)");
        Assert.Equal(39, pet.Total);
        Assert.Equal(39, s.PetAbilities.Sum(a => a.Total));
        Assert.Equal(["Hit", "Lifespike", "Bash", "Poison Bolt"], s.PetAbilities.Select(a => a.Name));
        var hit = s.PetAbilities.Single(a => a.Name == "Hit");
        Assert.Equal(22, hit.Total);
        Assert.Equal(2, hit.Hits);
    }

    /// <summary>Third-party lines are only broken out when the attacker is our pet —
    /// a bystander's abilities are not ours to report.</summary>
    [Fact]
    public void BystanderAbilitiesAreNotBrokenOut()
    {
        var s = Replay(
            At(0, 0, "Otherchar kicks orc pawn for 50 points of damage."),
            At(0, 2, "Orc pawn has taken 9 damage from Disease Cloud by Otherchar.")).Snapshot();

        Assert.Empty(s.PetAbilities);
    }

    /// <summary>Real necro sequence from eqlog_Dranak_freeport (2026-07-28): the pet melees
    /// and lifetaps, and the trailing "(Critical)" that third-party lines carry is credited
    /// to the pet the same way your own crits are.</summary>
    [Fact]
    public void PetCritsAreCounted()
    {
        var s = Replay(
            At(0, 0, "Lebn told you, 'Attacking a decaying skeleton Master.'"),
            At(0, 2, "Lebn slashes a decaying skeleton for 6 points of damage."),
            At(0, 4, "Lebn slashes a decaying skeleton for 13 points of damage. (Critical)"),
            At(0, 6, "Lebn hit a decaying skeleton for 4 points of magic damage by Lifetap."),
            At(0, 8, "Lebn hit a decaying skeleton for 9 points of magic damage by Lifetap. (Critical)")).Snapshot();

        var pet = Assert.Single(s.DamageBySource, d => d.Name == "Pet (Lebn)");
        Assert.Equal(32, pet.Total);
        Assert.Equal(2, pet.Crits);
        Assert.Equal(1, s.PetAbilities.Single(a => a.Name == "Slash").Crits);
        Assert.Equal(1, s.PetAbilities.Single(a => a.Name == "Lifetap").Crits);
        // A pet swinging is not you swinging: your own accuracy is unaffected.
        Assert.Equal(0, s.HitCount);
        Assert.Equal(0, s.CritCount);
    }

    /// <summary>A group member's crit is still not your damage — the annotation changed, the
    /// attribution rule did not.</summary>
    [Fact]
    public void BystanderCritsAreStillNotYours()
    {
        var s = Replay(At(0, 0, "Lizzid slashes orc centurion for 13 points of damage. (Critical)")).Snapshot();

        Assert.Equal(0, s.DamageDealt);
        Assert.Empty(s.PetAbilities);
    }

    [Theory]
    [InlineData("Jibekn slashes orc pawn for 5 points of damage.", "Slash")]
    [InlineData("Jibekn crushes orc pawn for 5 points of damage.", "Crush")]
    [InlineData("Jibekn punches orc pawn for 5 points of damage.", "Punch")]
    [InlineData("Jibekn bites orc pawn for 5 points of damage.", "Bite")]
    [InlineData("Jibekn backstabs orc pawn for 5 points of damage.", "Backstab")]
    [InlineData("Jibekn shoots orc pawn for 5 points of damage.", "Archery")]
    [InlineData("Jibekn frenzies on orc pawn for 5 points of damage.", "Frenzy")]
    public void ThirdPartyMeleeVerbsMapToSkillNames(string line, string skill) =>
        Assert.Equal(skill, Assert.IsType<ThirdMeleeEvent>(LogParser.Parse(Ts + line)).Skill);

    // ---- cast analytics ----

    [Fact]
    public void CastCompletionCountsInterruptsAndFizzles()
    {
        var s = Replay(
            At(0, 0, "You begin casting Stinging Swarm."),
            At(0, 4, "Orc centurion has taken 10 damage from your Stinging Swarm."),
            At(0, 10, "You begin casting Stinging Swarm."),
            At(0, 14, "Your Stinging Swarm spell is interrupted."),
            At(0, 20, "You begin casting Befriend Animal."),
            At(0, 24, "Your Befriend Animal spell fizzles!"),
            At(0, 30, "You begin casting Stinging Swarm."),
            At(0, 34, "Orc centurion has taken 10 damage from your Stinging Swarm.")).Snapshot();

        Assert.Equal(4, s.CastsStarted);
        Assert.Equal(1, s.CastsInterrupted);
        Assert.Equal(1, s.Fizzles);
        Assert.Equal(0.5, s.CastCompletion);
    }

    [Fact]
    public void CastCompletionIsNullBeforeAnyCast() =>
        Assert.Null(Replay(At(0, 0, "You slash orc pawn for 10 points of damage.")).Snapshot().CastCompletion);

    [Fact]
    public void DamageSplitsIntoDotAndDirect()
    {
        var s = Replay(
            At(0, 0, "Orc centurion has taken 10 damage from your Stinging Swarm."),
            At(0, 2, "Orc centurion has taken 10 damage from your Stinging Swarm."),
            At(0, 4, "You hit orc centurion for 13 points of fire damage by Burn."),
            At(0, 6, "You slash orc centurion for 25 points of damage.")).Snapshot();

        Assert.Equal(20, s.DotDamage);
        Assert.Equal(13, s.DirectSpellDamage);
        Assert.Equal(58, s.DamageDealt);   // melee stays out of both spell buckets
    }

    // ---- area spells ----

    /// <summary>The whole point: one cast hitting four creatures is one cast worth 400,
    /// not four hits worth 100. Per-target figures make an AoE look weaker than a nuke it
    /// actually beats.</summary>
    [Fact]
    public void OneCastHittingSeveralCreaturesIsCountedAsOneCast()
    {
        var s = Replay(
            At(0, 0, "You hit orc pawn for 100 points of fire damage by Rain of Fire."),
            At(0, 0, "You hit orc centurion for 100 points of fire damage by Rain of Fire."),
            At(0, 1, "You hit a giant spider for 100 points of fire damage by Rain of Fire."),
            At(0, 1, "You hit an asp for 100 points of fire damage by Rain of Fire.")).Snapshot();

        var aoe = Assert.Single(s.AreaSpells);
        Assert.Equal("Rain of Fire", aoe.Name);
        Assert.Equal(1, aoe.Casts);
        Assert.Equal(4, aoe.MaxTargets);
        Assert.Equal(4, aoe.AvgTargets);
        Assert.Equal(400, aoe.Damage);
        Assert.Equal(400, aoe.DamagePerCast);
    }

    [Fact]
    public void CastsSeparatedInTimeAreCountedSeparately()
    {
        var s = Replay(
            At(0, 0, "You hit orc pawn for 100 points of fire damage by Rain of Fire."),
            At(0, 0, "You hit orc centurion for 100 points of fire damage by Rain of Fire."),
            // Well past the burst window — a second cast.
            At(0, 30, "You hit a giant spider for 100 points of fire damage by Rain of Fire."),
            At(0, 30, "You hit an asp for 100 points of fire damage by Rain of Fire.")).Snapshot();

        var aoe = Assert.Single(s.AreaSpells);
        Assert.Equal(2, aoe.Casts);
        Assert.Equal(2, aoe.AvgTargets);
        Assert.Equal(200, aoe.DamagePerCast);
    }

    /// <summary>A single-target nuke must never be reported as an area spell, however
    /// often it's cast.</summary>
    [Fact]
    public void SingleTargetSpellsAreNotAreaSpells()
    {
        var s = Replay(
            At(0, 0, "You hit orc pawn for 100 points of fire damage by Burn."),
            At(0, 6, "You hit orc pawn for 100 points of fire damage by Burn."),
            At(0, 12, "You hit orc centurion for 100 points of fire damage by Burn.")).Snapshot();

        Assert.Empty(s.AreaSpells);
    }

    /// <summary>Average below max is the useful signal — it says later pulls were smaller
    /// than the best one, i.e. AoE value left on the table.</summary>
    [Fact]
    public void AverageTargetsPerCastExposesUndersizedPulls()
    {
        var s = Replay(
            At(0, 0, "You hit orc pawn for 100 points of fire damage by Rain of Fire."),
            At(0, 0, "You hit orc centurion for 100 points of fire damage by Rain of Fire."),
            At(0, 0, "You hit a giant spider for 100 points of fire damage by Rain of Fire."),
            At(0, 30, "You hit an asp for 100 points of fire damage by Rain of Fire.")).Snapshot();

        var aoe = Assert.Single(s.AreaSpells);
        Assert.Equal(2, aoe.Casts);
        Assert.Equal(3, aoe.MaxTargets);
        Assert.Equal(2, aoe.AvgTargets);   // (3 + 1) / 2
    }

    /// <summary>Ranks are the same spell, so they must not split into separate rows.</summary>
    [Fact]
    public void RanksOfTheSameAreaSpellAggregateTogether()
    {
        var s = Replay(
            At(0, 0, "You hit orc pawn for 100 points of fire damage by Rain of Fire."),
            At(0, 0, "You hit orc centurion for 100 points of fire damage by Rain of Fire."),
            At(0, 30, "You hit a giant spider for 150 points of fire damage by Rain of Fire II."),
            At(0, 30, "You hit an asp for 150 points of fire damage by Rain of Fire II.")).Snapshot();

        var aoe = Assert.Single(s.AreaSpells);
        Assert.Equal(2, aoe.Casts);
        Assert.Equal(500, aoe.Damage);
    }

    /// <summary>An area spell shows up the moment it lands, without waiting for the next
    /// cast to close the burst out.</summary>
    [Fact]
    public void AnAreaSpellAppearsWhileItsBurstIsStillOpen()
    {
        var s = Replay(
            At(0, 0, "You hit orc pawn for 100 points of fire damage by Rain of Fire."),
            At(0, 0, "You hit orc centurion for 100 points of fire damage by Rain of Fire.")).Snapshot();

        Assert.Single(s.AreaSpells);
        Assert.Equal(1, s.AreaSpells[0].Casts);
    }

    /// <summary>Melee never enters area detection, and neither does a damage shield —
    /// a shield hitting several attackers isn't a cast at all.</summary>
    [Fact]
    public void MeleeAndDamageShieldsAreNeverAreaSpells()
    {
        var s = Replay(
            At(0, 0, "You slash orc pawn for 10 points of damage."),
            At(0, 0, "You slash orc centurion for 10 points of damage."),
            At(0, 1, "Orc pawn is burned by YOUR flames for 5 points of non-melee damage."),
            At(0, 1, "Orc centurion is burned by YOUR flames for 5 points of non-melee damage.")).Snapshot();

        Assert.Empty(s.AreaSpells);
    }

    // ---- crowd-control watch rules ----

    private static readonly string[] FadeLines =
    [
        At(0, 0, "Your Befriend Animal spell has worn off of a puma."),   // charm
        At(0, 5, "Your Mesmerize spell has worn off of an asp."),         // mez
        At(0, 9, "Your Chords of Dissonance spell has worn off of a giant spider."), // damage song
    ];

    [Fact]
    public void AnyCrowdControlFilterNeedsNoMatchTextAndSkipsNonCcSpells()
    {
        var rule = new TrackedRule
        {
            Name = "CC broke", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.AnyCrowdControl,
        };
        var tracked = Assert.Single(Replay(FadeLines).Snapshot(recentWindow: null, rules: [rule]).Tracked);

        Assert.Equal(2, tracked.TotalQuantity);
        Assert.Contains(tracked.Items, i => i.Name == "Befriend Animal (Puma)");
        Assert.Contains(tracked.Items, i => i.Name == "Mesmerize (Asp)");
        Assert.DoesNotContain(tracked.Items, i => i.Name.StartsWith("Chords"));
    }

    /// <summary>rahvynn (#69): "You are no longer stunned." is YOU recovering from an
    /// NPC's stun. The fade catalog maps that flavor line to stun spells, but every CC
    /// filter means "MY control of a MOB ended" — a self-recovery must stay quiet, or
    /// the default CC-broke rule pings every time a mob stuns the player. ByName rules
    /// still hear self-fades: watching a specific spell is exactly their job.</summary>
    [Fact]
    public void SelfFadeLinesNeverFireCcFilters()
    {
        var ccRule = new TrackedRule
        {
            Name = "CC broke", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.AnyCrowdControl,
        };
        var byName = new TrackedRule
        {
            Name = "stun over", Kind = WatchKind.SpellFade, Pattern = "Force", SpellFilter = SpellFilter.ByName,
        };
        var snapshot = Replay([At(0, 0, "You are no longer stunned.")])
            .Snapshot(recentWindow: null, rules: [ccRule, byName]);

        Assert.DoesNotContain(snapshot.Tracked, t => t.Id == ccRule.Id && t.TotalQuantity > 0);
        var named = snapshot.Tracked.Single(t => t.Id == byName.Id);
        Assert.Equal(1, named.TotalQuantity);
    }

    [Fact]
    public void ASingleClassFilterMatchesOnlyThatClass()
    {
        var rule = new TrackedRule
        {
            Name = "Charm broke", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.Charm,
        };
        var tracked = Assert.Single(Replay(FadeLines).Snapshot(recentWindow: null, rules: [rule]).Tracked);

        Assert.Equal(1, tracked.TotalQuantity);
        Assert.Contains(tracked.Items, i => i.Name == "Befriend Animal (Puma)");
    }

    [Fact]
    public void AnySpellFilterCatchesEvenUnclassifiedSpellsLikeBuffs()
    {
        var rule = new TrackedRule
        {
            Name = "Anything dropped", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.AnySpell,
        };
        Assert.Equal(3, Assert.Single(
            Replay(FadeLines).Snapshot(recentWindow: null, rules: [rule]).Tracked).TotalQuantity);
    }

    /// <summary>A HoT teaches the catalog from its own tick line, so the class filter
    /// covers spells no seed list ever heard of — the same observation trick DoTs use.</summary>
    [Fact]
    public void HotFilterMatchesASpellLearnedFromItsOwnTicks()
    {
        var rule = new TrackedRule
        {
            Name = "HoT dropped", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.HealOverTime,
        };
        var tracked = Assert.Single(Replay(
            At(0, 0, "You healed Grimble over time for 12 hit points by Mending Winds."),
            At(0, 18, "Your Mending Winds spell has worn off of Grimble."),
            At(0, 20, "Your Befriend Animal spell has worn off of a puma.")   // charm, not HoT
        ).Snapshot(recentWindow: null, rules: [rule]).Tracked);

        Assert.Equal(1, tracked.TotalQuantity);
        Assert.Contains(tracked.Items, i => i.Name == "Mending Winds (Grimble)");
    }

    /// <summary>Someone else's HoT on you names the spell too — enough to classify it
    /// before you ever cast one yourself.</summary>
    [Fact]
    public void IncomingHotTicksTeachTheCatalog()
    {
        var rule = new TrackedRule
        {
            Name = "HoT dropped", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.HealOverTime,
        };
        var tracked = Assert.Single(Replay(
            At(0, 0, "Aenari healed you over time for 8 hit points by Celestial Elixir."),
            At(0, 24, "Your Celestial Elixir spell has worn off of Douglas.")
        ).Snapshot(recentWindow: null, rules: [rule]).Tracked);

        Assert.Equal(1, tracked.TotalQuantity);
    }

    /// <summary>The seed list covers the cold start: a fade arriving before any tick was
    /// seen still classifies. A plain direct heal never matches the HoT filter.</summary>
    [Fact]
    public void SeededHotMatchesWithoutTicksAndDirectHealsNever()
    {
        var rule = new TrackedRule
        {
            Name = "HoT dropped", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.HealOverTime,
        };
        var tracked = Assert.Single(Replay(
            At(0, 0, "Your Regeneration spell has worn off of Douglas."),      // seeded HoT, no tick seen
            At(0, 2, "You healed Douglas for 50 hit points by Light Healing."), // teaches Heal, not HoT
            At(0, 30, "Your Light Healing spell has worn off of Douglas.")
        ).Snapshot(recentWindow: null, rules: [rule]).Tracked);

        Assert.Equal(1, tracked.TotalQuantity);
        Assert.Contains(tracked.Items, i => i.Name == "Regeneration (Douglas)");
    }

    // ---- the direct charm-success line (eqlog_Hugzee, 2026-08-02) ----

    /// <summary>"X has been charmed." claims the pet immediately — the "Attacking …
    /// Master." tell can trail it by 9+ seconds, and damage in that window used to go
    /// unattributed to the player.</summary>
    [Fact]
    public void TheCharmedLineClaimsThePetBeforeTheMasterTell()
    {
        var s = Replay(
            At(0, 0, "You begin casting Charm."),
            At(0, 2, "a greater skeleton has been charmed."),
            // Damage lands BEFORE any Master tell — must already be credited.
            At(0, 5, "A greater skeleton slashes Footman of V`Zher for 12 points of damage."),
            At(0, 9, "A greater skeleton told you, 'Attacking Footman of V`Zher Master.'")
        ).Snapshot();

        var pet = s.DamageBySource.FirstOrDefault(d => d.Name.StartsWith("Pet ("));
        Assert.NotNull(pet);
        Assert.Equal(12, pet!.Total);
    }

    /// <summary>The charmed line names no caster and is bystander-visible (12 of 43 in
    /// the source log were other players' charms — David's catch): without one of OUR
    /// casts in flight it must claim nothing.</summary>
    [Fact]
    public void SomeoneElsesCharmNeverClaimsAPet()
    {
        var s = Replay(
            At(0, 0, "a Teir`Dal rogue has been charmed."),   // no own cast anywhere
            At(0, 5, "A Teir`Dal rogue slashes a gnoll for 12 points of damage.")
        ).Snapshot();
        Assert.DoesNotContain(s.DamageBySource, d => d.Name.StartsWith("Pet"));

        // Even with an own cast in flight, anything not KNOWN to be a charm doesn't
        // claim — Hugzee spams Heroic Leap (unknown category), and one leap coinciding
        // with a bystander's charm must not steal the pet or poison the catalog.
        var s2 = Replay(
            At(0, 0, "You begin casting Heroic Leap I."),
            At(0, 2, "a Teir`Dal rogue has been charmed."),
            At(0, 5, "A Teir`Dal rogue slashes a gnoll for 12 points of damage.")
        ).Snapshot();
        Assert.DoesNotContain(s2.DamageBySource, d => d.Name.StartsWith("Pet"));
    }

    // ---- buff/HoT wear-off flavor lines (the log names no spell; the catalog does) ----

    /// <summary>The Reddit report that drove this: an enchanter's "Echoing Light" and
    /// "Alacrity" fade rules never fired, because those spells fade with flavor text
    /// ("The echo of healing fades away." / "Your speed returns to normal.") that
    /// names nothing. The catalog maps message → candidate spells, so both ByName and
    /// class-filter rules now fire.</summary>
    [Fact]
    public void HotFlavorFadeFiresTheHotClassFilter()
    {
        var rule = new TrackedRule
        {
            Name = "HoT dropped", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.HealOverTime,
        };
        var tracked = Assert.Single(Replay(
            At(0, 0, "The echo of healing fades away.")
        ).Snapshot(recentWindow: null, rules: [rule]).Tracked);

        Assert.Equal(1, tracked.TotalQuantity);
        Assert.Contains(tracked.Items, i => i.Name == "Echoing Light");
    }

    [Fact]
    public void HasteFlavorFadeFiresAByNameAlacrityRule()
    {
        var rule = new TrackedRule
        {
            Name = "Haste dropped", Pattern = "Alacrity", Kind = WatchKind.SpellFade,
        };
        var tracked = Assert.Single(Replay(
            At(0, 0, "Your speed returns to normal.")
        ).Snapshot(recentWindow: null, rules: [rule]).Tracked);

        Assert.Equal(1, tracked.TotalQuantity);
        // The row shows the shared label — the log can't say WHICH haste it was.
        Assert.Contains(tracked.Items, i => i.Name == "Haste");
    }

    [Fact]
    public void FlavorFadesCountForAnySpellButNotForCcFilters()
    {
        var any = new TrackedRule
        {
            Name = "Anything dropped", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.AnySpell,
        };
        var cc = new TrackedRule
        {
            Name = "CC broke", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.AnyCrowdControl,
        };
        var s = Replay(
            At(0, 0, "The spirit of wolf leaves you."),
            At(0, 3, "Your speed returns to normal.")
        ).Snapshot(recentWindow: null, rules: [any, cc]);

        Assert.Equal(2, s.Tracked.First(t => t.Name == "Anything dropped").TotalQuantity);
        Assert.Equal(0, s.Tracked.First(t => t.Name == "CC broke").TotalQuantity);
    }

    [Fact]
    public void BuffFilterCatchesPumaButNotADebuffFade()
    {
        var buff = new TrackedRule
        {
            Name = "Buff dropped", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.Buff,
        };
        var s = Replay(
            At(0, 0, "The spirit of the puma departs."),
            At(0, 3, "The darkness fades.")
        ).Snapshot(recentWindow: null, rules: [buff]);

        var tracked = Assert.Single(s.Tracked);
        Assert.Equal(1, tracked.TotalQuantity);
        Assert.Contains(tracked.Items, i => i.Name == "Spirit of the Puma");
    }

    [Fact]
    public void ByNameFilterKeepsTheOriginalSubstringBehaviour()
    {
        var rule = new TrackedRule { Name = "Charm only", Pattern = "Befriend", Kind = WatchKind.SpellFade };
        Assert.Equal(SpellFilter.ByName, rule.SpellFilter);   // the default, so old rules are unaffected
        Assert.Equal(1, Assert.Single(
            Replay(FadeLines).Snapshot(recentWindow: null, rules: [rule]).Tracked).TotalQuantity);
    }

    /// <summary>Both UIs map dropdown indexes straight back to enum values, so a label
    /// array that drifts out of sync silently mislabels every rule.</summary>
    [Fact]
    public void DropdownLabelsStayAlignedWithTheirEnums()
    {
        Assert.Equal(Enum.GetValues<WatchKind>().Length,
            EQBuddy.UI.Shared.OptionsViewModel.KindNames.Length);
        Assert.Equal(Enum.GetValues<SpellFilter>().Length,
            EQBuddy.UI.Shared.OptionsViewModel.SpellFilterNames.Length);
    }

    // ---- the built-in CC alert ----

    [Fact]
    public void AFreshInstallShipsWithTheCrowdControlAlertEnabled()
    {
        var settings = new AppSettings();
        Assert.True(settings.ApplyDefaultRules());

        var rule = Assert.Single(settings.TrackedRules);
        Assert.Equal(WatchKind.SpellFade, rule.Kind);
        Assert.Equal(SpellFilter.AnyCrowdControl, rule.SpellFilter);
        Assert.True(rule.Enabled);
        Assert.True(rule.AlertBanner);
        Assert.True(rule.AlertSound);
    }

    /// <summary>The built-in rule is a starting point, not a fixture: every part of it has
    /// to be editable, and edits must survive the next launch's default-rules pass.</summary>
    [Fact]
    public void TheBuiltInRuleStaysFullyEditable()
    {
        var settings = new AppSettings();
        settings.ApplyDefaultRules();
        var rule = settings.TrackedRules[0];

        rule.AlertSound = false;
        rule.AlertBanner = false;
        rule.SpellFilter = SpellFilter.Charm;
        rule.Name = "My charm alarm";
        rule.Enabled = false;

        Assert.False(settings.ApplyDefaultRules());   // no second pass to undo the edits
        var after = Assert.Single(settings.TrackedRules);
        Assert.False(after.AlertSound);
        Assert.False(after.AlertBanner);
        Assert.False(after.Enabled);
        Assert.Equal(SpellFilter.Charm, after.SpellFilter);
        Assert.Equal("My charm alarm", after.Name);
    }

    [Fact]
    public void DefaultRulesAreNotAppliedTwice()
    {
        var settings = new AppSettings();
        settings.ApplyDefaultRules();
        Assert.False(settings.ApplyDefaultRules());
        Assert.Single(settings.TrackedRules);
    }

    /// <summary>Deleting the built-in rule has to stick, or it reappears every launch.</summary>
    [Fact]
    public void ADeletedDefaultRuleStaysDeleted()
    {
        var settings = new AppSettings();
        settings.ApplyDefaultRules();
        settings.TrackedRules.Clear();

        Assert.False(settings.ApplyDefaultRules());
        Assert.Empty(settings.TrackedRules);
    }

    /// <summary>The built-in rule must actually fire end to end, not just exist.</summary>
    [Fact]
    public void TheBuiltInRuleAlertsWhenACharmBreaks()
    {
        var settings = new AppSettings();
        settings.ApplyDefaultRules();

        var tracked = Assert.Single(Replay(
            At(0, 0, "You begin casting Befriend Animal."),
            At(0, 4, "a puma blinks."),
            At(1, 0, "Your Befriend Animal spell has worn off of a puma."))
            .Snapshot(recentWindow: null, rules: settings.TrackedRules).Tracked);

        Assert.Equal(1, tracked.TotalQuantity);
        Assert.Equal("Befriend Animal (Puma)", tracked.LastItem);
    }

    /// <summary>A class-filtered rule carries no match text, so the snapshot's
    /// "skip rules with no pattern" guard must not throw it away.</summary>
    [Fact]
    public void ClassFilteredRulesSurviveTheEmptyPatternGuard()
    {
        var rule = new TrackedRule
        {
            Name = "", Pattern = "", Kind = WatchKind.SpellFade, SpellFilter = SpellFilter.AnyCrowdControl,
        };
        Assert.True(rule.IsMatchAllKind);
        Assert.Equal(2, Assert.Single(
            Replay(FadeLines).Snapshot(recentWindow: null, rules: [rule]).Tracked).TotalQuantity);
    }
}
