namespace EQBuddy.Core;

public enum DamageKind { Melee, Spell }

public abstract record GameEvent(DateTime Time);

public record KillEvent(DateTime Time, string Target, string Killer) : GameEvent(Time);
public record DeathEvent(DateTime Time, string Killer) : GameEvent(Time);
/// <summary>IsAux marks automatic damage (damage shields) excluded from hit/accuracy counters.
/// Note is the raw trailing annotation ("Riposte", "Double Bow Shot", …) when present.
/// OverTime marks a damage-over-time tick, which the log distinguishes by line shape
/// ("X has taken N damage from your Y.") rather than by spell name.</summary>
public record DamageDealtEvent(DateTime Time, string Target, int Amount, DamageKind Kind, string Source, bool Critical, bool IsAux = false, string? Note = null, bool OverTime = false) : GameEvent(Time);
/// <param name="Self">"You hurt yourself for N points." — HP-cost spellcasting (a
/// necromancer's bread and butter), falls, drowning. Counts as damage taken, but must not
/// open a combat window or an encounter: hurting yourself is not a fight, and a swim
/// across a lake shouldn't dilute DPS with minutes of "combat".</param>
/// <param name="Ability">What the hit was: the attack verb mapped to the shared skill
/// labels ("Hit", "Slash") for melee, the spell name for nukes/DoTs, "" when the line
/// names neither (the non-melee "YOU are burned…" form).</param>
/// <param name="OverTime">"You have taken N damage from X by Y" — a DoT tick. Ticks from
/// a spell cast BEFORE a mez keep landing while the mob sleeps, so they must not be
/// read as "the attacker is awake" (issue #32: chips vanishing mid-mez).</param>
public record DamageTakenEvent(DateTime Time, string Attacker, int Amount, bool Melee, bool Self = false, string Ability = "", bool OverTime = false) : GameEvent(Time);
public record MissEvent(DateTime Time, bool Outgoing) : GameEvent(Time);
public record HealEvent(DateTime Time, string Target, int Amount, string Spell, bool Outgoing, string Healer = "", bool OverTime = false) : GameEvent(Time);
/// <summary>"X tries to hit YOU, but YOUR magical skin absorbs the blow!" — an incoming
/// melee attack fully absorbed by the player's own rune (not the generic dodge/parry
/// text a plain <see cref="MissEvent"/> carries, and not a mob's OWN rune blocking the
/// player's outgoing attack, which names the mob's skin instead of "YOUR").</summary>
public record RuneBlockEvent(DateTime Time, string Attacker) : GameEvent(Time);
/// <summary>"Your wounds begin to heal." — a regen/hymn tick; the log gives no amount, so we can only count them.</summary>
public record RegenTickEvent(DateTime Time) : GameEvent(Time);
/// <summary>A /consider line — deliberate targeting, so it can drive the target-drops
/// surfaces without a swing being landed first (David, 2026-08-06).</summary>
public record ConsiderEvent(DateTime Time, string Name, int Level) : GameEvent(Time);
/// <param name="Count">Stack size — auto-storage lines ("stored it in your tradeskill
/// depot", issue #39) can carry counts like the auto-sell lines do.</param>
public record LootEvent(DateTime Time, string Item, string Source, string? UpgradeResult, int Count = 1) : GameEvent(Time);
/// <summary>Vendor=true means a merchant sale (Item = what was sold); otherwise corpse coin or split.</summary>
public record MoneyEvent(DateTime Time, long Copper, bool Vendor = false, string? Item = null) : GameEvent(Time);
public record XpEvent(DateTime Time, double Percent, bool Party) : GameEvent(Time);
/// <summary>"You have gained an ability point!  You now have N ability points."</summary>
/// <param name="Points">Points in THIS gain — AA potions grant 2 per level
/// ("You have gained 2 ability point(s)!", issue #37); counting events instead
/// of points undercounted potioned sessions.</param>
public record AaEvent(DateTime Time, int TotalPoints, int Points = 1) : GameEvent(Time);

/// <summary>An AA ability bought or improved: rank 1 arrives as "gained the ability
/// \"X\"", later ranks as "improved X <rank>". Cost 0 = innate grant or free toggle.
/// The ledger of these is what explains duration modifiers (Mez Mastery etc.).</summary>
public record AaPurchaseEvent(DateTime Time, string Ability, int Rank, int Cost) : GameEvent(Time);
/// <summary>Loot auto-sold on pickup: counts as loot AND vendor income.</summary>
public record AutoSellEvent(DateTime Time, string Item, int Count, string Source, long Copper) : GameEvent(Time);
/// <summary>"You successfully destroyed N X." — the advanced loot window's sell/destroy
/// action; when a "received … from that item" money line follows, this names the item.</summary>
public record ItemDestroyedEvent(DateTime Time, string Item, int Count) : GameEvent(Time);
/// <summary>"Your X spell has worn off (of target)." — mez/charm/buff expiry, seen by
/// the caster. Fires whether the spell timed out or broke early.
/// Pet=true is the "Your pet's X spell has worn off." form: the pet's spell, not yours,
/// so it is excluded from spell-fade watch rules.</summary>
/// <summary>"X has been charmed." — the direct charm-success line; a definitive pet
/// claim, unlike the circumstantial blink.</summary>
public record CharmedEvent(DateTime Time, string Name) : GameEvent(Time);
public record SpellWornOffEvent(DateTime Time, string Spell, string Target, bool Pet = false) : GameEvent(Time);
/// <summary>A buff/HoT wear-off flavor line ("The echo of healing fades away.") mapped
/// through <see cref="FadeMessageCatalog"/>: the log names no spell, so the event
/// carries every candidate that shares the message plus a display label.</summary>
public record BuffFadeEvent(DateTime Time, string Label, string[] Spells, string Category = "") : GameEvent(Time);
public record LevelEvent(DateTime Time, int Level) : GameEvent(Time);
public record SkillUpEvent(DateTime Time, string Skill, int Value) : GameEvent(Time);
/// <summary>"You will now use Round Kick instead of Kick while attacking." — an ability that
/// takes over a basic attack. The damage still logs under the old verb ("You kick …"), so
/// this line is the only thing that says the hits are now Round Kick rather than Kick.</summary>
public record SkillSubstitutionEvent(DateTime Time, string Ability, string Replaced) : GameEvent(Time);
/// <param name="Capped">"Your faction standing with X could not possibly get any
/// better/worse." — the standing is pinned at the cap, so the kill changed nothing. Delta
/// is 0, but the event still shows WHY a farmed faction isn't moving.</param>
public record FactionEvent(DateTime Time, string Faction, int Delta, bool Capped = false) : GameEvent(Time);
public record ZoneEvent(DateTime Time, string Zone) : GameEvent(Time);
public record CraftEvent(DateTime Time, string Item) : GameEvent(Time);
public record FizzleEvent(DateTime Time, string Spell = "") : GameEvent(Time);
/// <summary>"You begin casting X." / "You begin singing X." — the player started a cast.
/// Only the player's own casts are parsed; other entities' casts are deliberately ignored.</summary>
/// <param name="Song">"You begin to sing X." — a bard song start. Counts as a cast for
/// charm/mez correlation (bard charms and mezzes are songs; issue #29's missing half:
/// song starts were never parsed, so a bard's _pendingCast never existed and no
/// landing line could ever correlate) but stays OUT of the cast-completion stats —
/// twisting would swamp them.</param>
public record SpellCastEvent(DateTime Time, string Spell, bool Song = false) : GameEvent(Time);
/// <summary>"Your X spell is interrupted." — a started cast that never landed.</summary>
public record SpellInterruptedEvent(DateTime Time, string Spell) : GameEvent(Time);
/// <summary>The player's pet announced itself ("<Pet> told you, 'Attacking X Master.'").</summary>
public record PetClaimEvent(DateTime Time, string PetName) : GameEvent(Time);
/// <summary>A creature blinked ("an asp blinks.") — the charm-spell tell; treated as a provisional pet claim.</summary>
/// <param name="Weak">"X moans." — the necro charm landing (eqlwiki, all three undead
/// charms). Unlike blinks, moaning is plausible ambient flavor, so a weak signal acts
/// ONLY when one of our casts is in flight; it never sets the provisional pet on its
/// own.</param>
public record PetBlinkEvent(DateTime Time, string Name, bool Weak = false) : GameEvent(Time);
/// <summary>Someone other than the player landed a melee hit (may be the player's pet).
/// Skill is the attack verb mapped to the same label the player's own hits use ("bashes" → Bash).
/// Critical comes from the same trailing annotation your own hits carry — third-party lines
/// do report it ("Lizzid slashes orc centurion for 13 points of damage. (Critical)").</summary>
public record ThirdMeleeEvent(DateTime Time, string Attacker, string Target, int Amount, string Skill = "", bool Critical = false) : GameEvent(Time);
/// <summary>Spell/DoT damage from someone other than the player (may be the player's pet).</summary>
public record ThirdDotEvent(DateTime Time, string Caster, string Target, int Amount, string Spell, bool Critical = false) : GameEvent(Time);
/// <summary>Direct spell hit by someone else: "Jibekn hit orc centurion for 11 points of magic damage by Lifespike."</summary>
public record ThirdSchoolEvent(DateTime Time, string Attacker, string Target, int Amount, string Spell, bool Critical = false) : GameEvent(Time);
/// <summary>A missed attack between others (combat-clock signal only).</summary>
public record ThirdMissEvent(DateTime Time, string Attacker) : GameEvent(Time);
public record ResistEvent(DateTime Time, string Spell = "") : GameEvent(Time);
/// <summary>A user-dropped camp/segment marker (hotkey or menu), timestamped with wall clock.</summary>
public record SessionMarkerEvent(DateTime Time, string Label) : GameEvent(Time);
/// <summary>A raw log line (message only, no timestamp prefix) kept because it matched a
/// <see cref="WatchKind.Text"/> rule's text. Only matching lines become events — journaling
/// every line would mean holding the whole log in memory, most of it chat.</summary>
public record RawLineEvent(DateTime Time, string Line) : GameEvent(Time);
/// <summary>"Shack begins casting Shield of Thistles IV." — another player's or an NPC's
/// cast, WITH spell name and rank (verified in eqlog_Hugzee). This is what lets a group
/// member's EQBuddy attribute a mez it merely witnessed: the caster's cast line plus the
/// bystander-visible landing line are both in everyone's log.</summary>
public record OtherCastEvent(DateTime Time, string Caster, string Spell) : GameEvent(Time);
/// <summary>"X has been mesmerized." — mez landing, bystander-visible exactly like
/// "has been charmed." (proven: NPC mezzes on other players appear in Hugzee's log).
/// Names no caster and no spell; correlation with a recent mez cast supplies both.</summary>
public record MezzedEvent(DateTime Time, string Target) : GameEvent(Time);
/// <summary>"You assume a defensive stance." — stance state change (EQL-specific).</summary>
public record StanceEvent(DateTime Time, string Stance) : GameEvent(Time);
/// <summary>"You begin reciting the unyielding invocation." — invocation change
/// (first observed in Hugzee's enchanter respec, 2026-08-03; invocations logged
/// nothing we knew of before that). The preceding "You begin to change your
/// invocation." line is deliberately not parsed — the reciting line names the
/// state, and parsing both would be the unconscious-line mistake again.</summary>
public record InvocationEvent(DateTime Time, string Invocation) : GameEvent(Time);
