using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Replay tests: feed event streams (as raw log lines) through the parser into
/// SessionStats and assert the resulting snapshot — the same path live tailing uses.
/// </summary>
public class SessionStatsTests
{
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
    public void AutoSellCountsAsLootAndVendorIncome()
    {
        var s = Replay("Douglas",
            At(0, 0, "You looted 2 Spider Silk from a giant spider's corpse and sold it for 2 gold, 8 silver and 6 copper.")).Snapshot();
        Assert.Equal(2, s.LootTotal);
        Assert.Equal(286, s.VendorCopper);
        Assert.Equal(286, s.Copper);
        Assert.Equal(("Spider Silk", 2), (Assert.Single(s.Loot).Item, Assert.Single(s.Loot).Count));
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
        Assert.NotNull(s.HoursToLevel);
        // 50% into level 6, earning 80% per 59 min → ~0.61h remaining
        Assert.InRange(s.HoursToLevel!.Value, 0.55, 0.68);
    }
}
