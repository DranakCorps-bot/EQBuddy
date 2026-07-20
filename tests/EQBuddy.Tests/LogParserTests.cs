using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Fixture lines are real (sanitized) EverQuest Legends log lines gathered during
/// development. Per TEST-005 every test validates the parsed fields, not just a match.
/// Every parser bug fix must add its triggering line here first.
/// </summary>
public class LogParserTests
{
    private const string Ts = "[Sat Jul 18 15:39:13 2026] ";
    private static readonly DateTime Time0 = new(2026, 7, 18, 15, 39, 13);

    private static T Parse<T>(string msg) where T : GameEvent
    {
        var evt = LogParser.Parse(Ts + msg);
        Assert.NotNull(evt);
        var typed = Assert.IsType<T>(evt);
        Assert.Equal(Time0, typed.Time);
        return typed;
    }

    private static void AssertIgnored(string msg) =>
        Assert.Null(LogParser.Parse(Ts + msg));

    // ---- melee out ----

    [Theory]
    [InlineData("You slash orc pawn for 10 points of damage.", "Slash", "Orc pawn", 10, false)]
    [InlineData("You kick orc pawn for 1 point of damage.", "Kick", "Orc pawn", 1, false)]
    [InlineData("You slash orc centurion for 25 points of damage. (Critical)", "Slash", "Orc centurion", 25, true)]
    [InlineData("You cleave orc centurion for 12 points of damage.", "Cleave", "Orc centurion", 12, false)]
    [InlineData("You shoot a rattlesnake for 5 points of damage.", "Archery", "Rattlesnake", 5, false)]
    [InlineData("You shoot orc centurion for 18 points of damage. (Double Bow Shot)", "Archery", "Orc centurion", 18, false)]
    [InlineData("You bash an orc legionnaire for 5 points of damage. (Riposte Critical)", "Bash", "Orc legionnaire", 5, true)]
    [InlineData("You crush Asaka L`Rei for 34 points of damage.", "Crush", "Asaka L`Rei", 34, false)]
    public void MeleeOut(string msg, string source, string target, int amount, bool crit)
    {
        var e = Parse<DamageDealtEvent>(msg);
        Assert.Equal(source, e.Source);
        Assert.Equal(target, e.Target);
        Assert.Equal(amount, e.Amount);
        Assert.Equal(crit, e.Critical);
        Assert.Equal(DamageKind.Melee, e.Kind);
        Assert.False(e.IsAux);
    }

    // ---- spells out ----

    [Fact]
    public void SchoolNuke()
    {
        var e = Parse<DamageDealtEvent>("You hit orc centurion for 13 points of fire damage by Burn.");
        Assert.Equal(("Burn", "Orc centurion", 13, DamageKind.Spell, false), (e.Source, e.Target, e.Amount, e.Kind, e.Critical));
    }

    [Fact]
    public void ClassicNonMeleeNuke()
    {
        var e = Parse<DamageDealtEvent>("You hit orc pawn for 20 points of non-melee damage.");
        Assert.Equal(("Direct spell", 20, DamageKind.Spell), (e.Source, e.Amount, e.Kind));
    }

    [Fact]
    public void DotTickIncludingBardSong()
    {
        var e = Parse<DamageDealtEvent>("Orc centurion has taken 3 damage from your Chords of Dissonance.");
        Assert.Equal(("Chords of Dissonance", "Orc centurion", 3, DamageKind.Spell), (e.Source, e.Target, e.Amount, e.Kind));
    }

    [Fact]
    public void DamageShieldIsAuxDamage()
    {
        var e = Parse<DamageDealtEvent>("Orc centurion is burned by YOUR flames for 5 points of non-melee damage.");
        Assert.Equal(("Damage shield", "Orc centurion", 5, true), (e.Source, e.Target, e.Amount, e.IsAux));
    }

    // ---- damage taken ----

    [Theory]
    [InlineData("Orc centurion hits YOU for 4 points of damage.", "Orc centurion", 4, true)]
    [InlineData("A puma slashes YOU for 7 points of damage.", "Puma", 7, true)]
    [InlineData("ice boned skeleton hit you for 20 points of cold damage by Ice Bone Frost Burst.", "Ice boned skeleton", 20, false)]
    [InlineData("YOU are burned by orc centurion's flames for 6 points of non-melee damage!", "Burned by orc centurion's flames", 6, false)]
    [InlineData("You have taken 1 damage from Rabies by Gynok Moltor.", "Gynok Moltor", 1, false)]
    public void DamageTaken(string msg, string attacker, int amount, bool melee)
    {
        var e = Parse<DamageTakenEvent>(msg);
        Assert.Equal(attacker, e.Attacker);
        Assert.Equal(amount, e.Amount);
        Assert.Equal(melee, e.Melee);
    }

    // ---- misses ----

    [Theory]
    [InlineData("You try to slash orc pawn, but miss!", true)]
    [InlineData("You try to shoot an asp, but miss! (Riposte)", true)]
    [InlineData("Orc centurion tries to hit YOU, but misses!", false)]
    [InlineData("Orc centurion tries to hit YOU, but YOU dodge! (Riposte)", false)]
    public void Misses(string msg, bool outgoing)
    {
        var e = Parse<MissEvent>(msg);
        Assert.Equal(outgoing, e.Outgoing);
    }

    [Fact]
    public void ThirdPartyMissIsCombatSignal()
    {
        var e = Parse<ThirdMissEvent>("A puma tries to slash a ghoul, but misses!");
        Assert.Equal("A puma", e.Attacker);
    }

    // ---- pets ----

    [Theory]
    [InlineData("Jibekn told you, 'Attacking orc centurion Master.'", "Jibekn")]
    [InlineData("A puma told you, 'Attacking a ghoul Master.'", "A puma")]
    public void PetClaim(string msg, string pet)
    {
        var e = Parse<PetClaimEvent>(msg);
        Assert.Equal(pet, e.PetName);
    }

    [Fact]
    public void CharmBlink()
    {
        var e = Parse<PetBlinkEvent>("an asp blinks.");
        Assert.Equal("an asp", e.Name);
    }

    [Fact]
    public void ThirdPartyMelee()
    {
        var e = Parse<ThirdMeleeEvent>("A puma slashes a ghoul for 11 points of damage.");
        Assert.Equal(("A puma", "Ghoul", 11), (e.Attacker, e.Target, e.Amount));
    }

    [Fact]
    public void ThirdPartySchoolSpell()
    {
        var e = Parse<ThirdSchoolEvent>("Jibekn hit orc centurion for 11 points of magic damage by Lifespike.");
        Assert.Equal(("Jibekn", "Orc centurion", 11, "Lifespike"), (e.Attacker, e.Target, e.Amount, e.Spell));
    }

    // ---- healing ----

    [Fact]
    public void HealCast()
    {
        var e = Parse<HealEvent>("You healed Douglas for 66 hit points by Light Healing.");
        Assert.Equal(("Douglas", 66, "Light Healing", true), (e.Target, e.Amount, e.Spell, e.Outgoing));
    }

    [Fact]
    public void HealReceived()
    {
        var e = Parse<HealEvent>("Aamilea healed you for 56 hit points by Light Healing.");
        Assert.Equal((56, "Light Healing", false, "Aamilea"), (e.Amount, e.Spell, e.Outgoing, e.Healer));
    }

    [Fact]
    public void RegenTickHasNoAmount() =>
        Parse<RegenTickEvent>("Your wounds begin to heal.");

    [Fact]
    public void ThirdPartyHealsIgnored() =>
        AssertIgnored("Guard Meadom healed Guard Legver for 0 (63) hit points by Center.");

    // ---- kills and deaths ----

    [Theory]
    [InlineData("You have slain orc pawn!", "Orc pawn", "You")]
    [InlineData("Orc centurion has been slain by Lizzid!", "Orc centurion", "Lizzid")]
    public void Kills(string msg, string target, string killer)
    {
        var e = Parse<KillEvent>(msg);
        Assert.Equal(target, e.Target);
        Assert.Equal(killer, e.Killer);
    }

    [Fact]
    public void Death()
    {
        var e = Parse<DeathEvent>("You have been slain by an orc thaumaturgist pet!");
        Assert.Equal("an orc thaumaturgist pet", e.Killer);
    }

    // ---- loot, money, crafting ----

    [Fact]
    public void CorpseLoot()
    {
        var e = Parse<LootEvent>("--You have looted a Mote of Infinitesimal Potential from orc centurion's corpse.--");
        Assert.Equal(("Mote of Infinitesimal Potential", "Orc centurion", null), (e.Item, e.Source, e.UpgradeResult));
    }

    [Fact]
    public void LootWithAutoUpgrade()
    {
        var e = Parse<LootEvent>("You looted a Crushbone Belt +2 from orc centurion's corpse to create a Crushbone Belt +5");
        Assert.Equal(("Crushbone Belt +2", "Crushbone Belt +5"), (e.Item, e.UpgradeResult));
    }

    [Theory]
    [InlineData("You looted a Snake Egg from an asp's corpse and sold it for 4 copper.", "Snake Egg", 1, 4)]
    [InlineData("You looted 2 Spider Silk from a giant spider's corpse and sold it for 2 gold, 8 silver and 6 copper.", "Spider Silk", 2, 286)]
    public void AutoSoldLoot(string msg, string item, int count, long copper)
    {
        var e = Parse<AutoSellEvent>(msg);
        Assert.Equal((item, count, copper), (e.Item, e.Count, e.Copper));
    }

    [Theory]
    [InlineData("You receive 7 copper from the corpse.", 7, false)]
    [InlineData("You receive 3 platinum 2 gold 6 silver 7 copper from Lanadin for the Bronze Rapier +2(s).", 3267, true)]
    public void Money(string msg, long copper, bool vendor)
    {
        var e = Parse<MoneyEvent>(msg);
        Assert.Equal(copper, e.Copper);
        Assert.Equal(vendor, e.Vendor);
    }

    [Fact]
    public void ItemMerge()
    {
        var e = Parse<CraftEvent>("You have successfully merged two items together to create a new item: Crushbone Belt +7");
        Assert.Equal("Crushbone Belt +7", e.Item);
    }

    // ---- progression ----

    [Fact]
    public void PartyXp()
    {
        var e = Parse<XpEvent>("You gain party experience! (0.081%)");
        Assert.Equal(0.081, e.Percent, 3);
        Assert.True(e.Party);
    }

    [Fact]
    public void LevelUp()
    {
        var e = Parse<LevelEvent>("You have gained a level! Welcome to level 7!");
        Assert.Equal(7, e.Level);
    }

    [Fact]
    public void AaPoint()
    {
        var e = Parse<AaEvent>("You have gained an ability point!  You now have 6 ability points.");
        Assert.Equal(6, e.TotalPoints);
    }

    [Fact]
    public void SkillUp()
    {
        var e = Parse<SkillUpEvent>("You have become better at 1H Slashing! (53)");
        Assert.Equal(("1H Slashing", 53), (e.Skill, e.Value));
    }

    [Fact]
    public void Faction()
    {
        var e = Parse<FactionEvent>("Your faction standing with Crushbone Orcs has been adjusted by -1.");
        Assert.Equal(("Crushbone Orcs", -1), (e.Faction, e.Delta));
    }

    [Fact]
    public void Zone()
    {
        var e = Parse<ZoneEvent>("You have entered Clan Crushbone.");
        Assert.Equal("Clan Crushbone", e.Zone);
    }

    // ---- resists / fizzles ----

    [Theory]
    [InlineData("Your target resisted the Poison Bolt spell.")]
    [InlineData("A willowisp resisted your Denon's Disruptive Discord!")]
    public void Resists(string msg) => Parse<ResistEvent>(msg);

    [Fact]
    public void Fizzle() => Parse<FizzleEvent>("Your Disease Cloud spell fizzles!");

    // ---- noise stays noise ----

    [Theory]
    [InlineData("Sneaky tells General:2, 'but daddy I love hiiiim'")]
    [InlineData("Auto attack is on.")]
    [InlineData("Your target is too far away, get closer!")]
    [InlineData("Orc centurion says, 'Hail, Emperor Crush!'")]
    [InlineData("You begin casting Poison Bolt.")]
    [InlineData("a hardened skeleton winces.")]
    public void ChatAndFlavorIgnored(string msg) => AssertIgnored(msg);
}
