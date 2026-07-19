namespace EQBuddy.Core;

public enum DamageKind { Melee, Spell }

public abstract record GameEvent(DateTime Time);

public record KillEvent(DateTime Time, string Target, string Killer) : GameEvent(Time);
public record DeathEvent(DateTime Time, string Killer) : GameEvent(Time);
/// <summary>IsAux marks automatic damage (damage shields) excluded from hit/accuracy counters.
/// Note is the raw trailing annotation ("Riposte", "Double Bow Shot", …) when present.</summary>
public record DamageDealtEvent(DateTime Time, string Target, int Amount, DamageKind Kind, string Source, bool Critical, bool IsAux = false, string? Note = null) : GameEvent(Time);
public record DamageTakenEvent(DateTime Time, string Attacker, int Amount, bool Melee) : GameEvent(Time);
public record MissEvent(DateTime Time, bool Outgoing) : GameEvent(Time);
public record HealEvent(DateTime Time, string Target, int Amount, string Spell, bool Outgoing, string Healer = "") : GameEvent(Time);
/// <summary>"Your wounds begin to heal." — a regen/hymn tick; the log gives no amount, so we can only count them.</summary>
public record RegenTickEvent(DateTime Time) : GameEvent(Time);
public record LootEvent(DateTime Time, string Item, string Source, string? UpgradeResult) : GameEvent(Time);
/// <summary>Vendor=true means a merchant sale (Item = what was sold); otherwise corpse coin or split.</summary>
public record MoneyEvent(DateTime Time, long Copper, bool Vendor = false, string? Item = null) : GameEvent(Time);
public record XpEvent(DateTime Time, double Percent, bool Party) : GameEvent(Time);
/// <summary>"You have gained an ability point!  You now have N ability points."</summary>
public record AaEvent(DateTime Time, int TotalPoints) : GameEvent(Time);
/// <summary>Loot auto-sold on pickup: counts as loot AND vendor income.</summary>
public record AutoSellEvent(DateTime Time, string Item, int Count, string Source, long Copper) : GameEvent(Time);
public record LevelEvent(DateTime Time, int Level) : GameEvent(Time);
public record SkillUpEvent(DateTime Time, string Skill, int Value) : GameEvent(Time);
public record FactionEvent(DateTime Time, string Faction, int Delta) : GameEvent(Time);
public record ZoneEvent(DateTime Time, string Zone) : GameEvent(Time);
public record CraftEvent(DateTime Time, string Item) : GameEvent(Time);
public record FizzleEvent(DateTime Time) : GameEvent(Time);
/// <summary>The player's pet announced itself ("<Pet> told you, 'Attacking X Master.'").</summary>
public record PetClaimEvent(DateTime Time, string PetName) : GameEvent(Time);
/// <summary>A creature blinked ("an asp blinks.") — the charm-spell tell; treated as a provisional pet claim.</summary>
public record PetBlinkEvent(DateTime Time, string Name) : GameEvent(Time);
/// <summary>Someone other than the player landed a melee hit (may be the player's pet).</summary>
public record ThirdMeleeEvent(DateTime Time, string Attacker, string Target, int Amount) : GameEvent(Time);
/// <summary>Spell/DoT damage from someone other than the player (may be the player's pet).</summary>
public record ThirdDotEvent(DateTime Time, string Caster, string Target, int Amount, string Spell) : GameEvent(Time);
/// <summary>Direct spell hit by someone else: "Jibekn hit orc centurion for 11 points of magic damage by Lifespike."</summary>
public record ThirdSchoolEvent(DateTime Time, string Attacker, string Target, int Amount, string Spell) : GameEvent(Time);
/// <summary>A missed attack between others (combat-clock signal only).</summary>
public record ThirdMissEvent(DateTime Time, string Attacker) : GameEvent(Time);
public record ResistEvent(DateTime Time) : GameEvent(Time);
