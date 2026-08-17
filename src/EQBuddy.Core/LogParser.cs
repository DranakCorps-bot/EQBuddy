using System.Globalization;
using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>Parses raw EverQuest Legends log lines into typed game events.</summary>
public static partial class LogParser
{
    // [Fri Jul 17 18:41:14 2026] message
    [GeneratedRegex(@"^\[(?<ts>[A-Za-z]{3} [A-Za-z]{3} +\d{1,2} \d{2}:\d{2}:\d{2} \d{4})\] (?<msg>.*)$")]
    private static partial Regex LineRx();

    [GeneratedRegex(@"^You have slain (?<target>.+)!$")]
    private static partial Regex YouSlainRx();

    [GeneratedRegex(@"^(?<target>.+) has been slain by (?<killer>.+)!$")]
    private static partial Regex OtherSlainRx();

    [GeneratedRegex(@"^You have been slain by (?<killer>.+)!$")]
    private static partial Regex YouDiedRx();

    // "You died." — the killer-less death form; see the note where this is matched.
    [GeneratedRegex(@"^You died\.$")]
    private static partial Regex YouDiedPlainRx();

    // "You will now use Round Kick instead of Kick while attacking."
    // "You will now use Slam instead of Bash while attacking."
    // The substituted attack keeps logging under the original verb, so this line is the only
    // evidence that "You kick …" now means Round Kick.
    [GeneratedRegex(@"^You will now use (?<ability>.+?) instead of (?<replaced>.+?) while attacking\.$")]
    private static partial Regex SkillSubstitutionRx();

    // You slash orc pawn for 10 points of damage. (Critical) / (Double Bow Shot) / etc.
    // "reave"/"smite" found 2026-08-01 by unmatched-line analysis (third-party form,
    // eqlog_Hugzee: "Lizzid reaves orc legionnaire for 7 points of damage.", 1,381 lines).
    [GeneratedRegex(@"^You (?<verb>slash|hit|kick|bash|pierce|crush|punch|backstab|bite|claw|maul|gore|sting|strike|slice|cleave|smash|rend|slam|shoot|reave|smite|frenzy on|frenzies on) (?<target>.+?) for (?<dmg>\d+) points? of damage\.(?: \((?<note>[^)]+)\))?$")]
    private static partial Regex MeleeOutRx();

    // You try to slash orc pawn, but miss! / but orc pawn dodges! (Riposte)
    [GeneratedRegex(@"^You try to (?<verb>\w+)(?: on)? (?<target>.+?), but (?<reason>.+)!(?: \([^)]+\))?$")]
    private static partial Regex MeleeMissRx();

    // Orc centurion has taken 10 damage from your Poison Bolt.
    [GeneratedRegex(@"^(?<target>.+?) has taken (?<dmg>\d+) damage from your (?<spell>.+?)\.(?: \((?<note>[^)]+)\))?$")]
    private static partial Regex DotOutRx();

    // You hit orc pawn for 20 points of non-melee damage.
    [GeneratedRegex(@"^You hit (?<target>.+?) for (?<dmg>\d+) points? of non-melee damage\.(?: \((?<note>[^)]+)\))?$")]
    private static partial Regex NukeOutRx();

    // You hit orc centurion for 13 points of fire damage by Burn.
    [GeneratedRegex(@"^You hit (?<target>.+?) for (?<dmg>\d+) points? of \w+ damage by (?<spell>.+?)\.(?: \((?<note>[^)]+)\))?$")]
    private static partial Regex SchoolNukeOutRx();

    // Your Polished Mithril Mask (Exaltation) feels alive with power.  — an item proc
    // firing (Kerdude's spellblade snippet, #85); the damage line follows within a beat.
    [GeneratedRegex(@"^Your (?<item>.+?) feels alive with power\.$")]
    private static partial Regex ItemProcRx();

    // ice boned skeleton hit you for 20 points of cold damage by Ice Bone Frost Burst.
    [GeneratedRegex(@"^(?<attacker>.+?) hit you for (?<dmg>\d+) points? of \w+ damage by (?<spell>.+?)\.$", RegexOptions.IgnoreCase)]
    private static partial Regex SchoolHitInRx();

    // You hurt yourself for 12 points.  — HP-cost casting (348 lines in one Dranak necro
    // session), falls, drowning. Always "points", even for 1.
    [GeneratedRegex(@"^You hurt yourself for (?<dmg>\d+) points?\.$")]
    private static partial Regex SelfHurtRx();

    // You have taken 1 damage from Rabies by Gynok Moltor.
    [GeneratedRegex(@"^You have taken (?<dmg>\d+) damage from (?<spell>.+?) by (?<attacker>.+?)\.$")]
    private static partial Regex DotInRx();

    // Orc centurion is burned by YOUR flames for 5 points of non-melee damage.
    [GeneratedRegex(@"^(?<target>.+?) is \w+ by YOUR .+? for (?<dmg>\d+) points? of non-melee damage\.$")]
    private static partial Regex DamageShieldRx();

    // Jibekn hit orc centurion for 11 points of magic damage by Lifespike.
    // Xabeker hit a Deathfist Pawn for 6 points of magic damage by Harm Touch.
    [GeneratedRegex(@"^(?<attacker>.+?) hit (?<target>.+?) for (?<dmg>\d+) points? of \w+ damage by (?<spell>.+?)\.(?: \((?<note>[^)]+)\))?$")]
    private static partial Regex ThirdSchoolRx();

    // Orc centurion hits YOU for 4 points of damage.
    [GeneratedRegex(@"^(?<attacker>.+?) (?<verb>hits|slashes|kicks|bashes|pierces|crushes|punches|backstabs|bites|claws|mauls|gores|stings|strikes|slices|cleaves|smashes|rends|slams|shoots|reaves|smites|frenzies on) YOU for (?<dmg>\d+) points? of damage\.(?: \([^)]+\))?$")]
    private static partial Regex MeleeInRx();

    // Orc centurion tries to hit YOU, but misses! / but YOU dodge! (Riposte)
    [GeneratedRegex(@"^(?<attacker>.+?) tries to (?:\w+)(?: on)? YOU, but (?<reason>.+)!(?: \([^)]+\))?$")]
    private static partial Regex MeleeInMissRx();

    // A froglok shin knight tries to hit YOU, but YOUR magical skin absorbs the blow!
    // Checked ahead of MeleeInMissRx: "YOUR magical skin absorbs" is the player's own
    // rune blocking an incoming hit, distinct from a mob's OWN skin absorbing the
    // player's outgoing attack ("... but a froglok tactician's magical skin absorbs
    // the blow!", which MeleeMissRx already covers as an ordinary outgoing miss).
    [GeneratedRegex(@"^(?<attacker>.+?) tries to \w+(?: on)? YOU, but YOUR magical skin absorbs the blow!(?: \([^)]+\))?$")]
    private static partial Regex RuneBlockInRx();

    // YOU are burned by orc centurion's flames for 6 points of non-melee damage!
    [GeneratedRegex(@"^YOU are (?<how>.+?) for (?<dmg>\d+) points? of non-melee damage!$")]
    private static partial Regex NonMeleeInRx();

    // The creature inside the "how" phrase: "pierced by The Spiroc Lord's thorns"
    // → "The Spiroc Lord" (#133, bjstrange: the whole phrase used to become the
    // attacker, opening a phantom fight the Loot card then showed as a mob).
    // Greedy attacker so the LAST possessive wins for names that contain one.
    [GeneratedRegex(@"^\w+ by (?<attacker>.+)'s \w+$")]
    private static partial Regex DamageShieldOwnerRx();

    // You healed Kaybek for 10 hit points by Lifespike. / You healed Kaybek for 7 (10) hit points by Lifespike.
    // Heal-over-time ticks say "healed X over time for N" (eqlog_Hugzee: "Xephira healed
    // Spamwagon over time for 11 hit points by Budding Heal.") — same event, one extra phrase.
    [GeneratedRegex(@"^You healed (?<target>.+?)(?<hot> over time)? for (?<amount>\d+)(?: \((?<attempted>\d+)\))? hit points(?: by (?<spell>.+?))?\.$")]
    private static partial Regex HealOutRx();

    // You have been healed for 30 hit points. / Someone healed you...
    [GeneratedRegex(@"^You have been healed for (?<amount>\d+) (?:hit )?points?(?: of damage)?\.?$")]
    private static partial Regex HealInRx();

    // --You have looted a Mote of Infinitesimal Potential from orc centurion's corpse.--
    // --You have looted 2 Bone Chips from a decaying skeleton's corpse.-- (#80: the
    // quantity form was invisible for a YEAR of "a/an" — stacked drops counted zero,
    // which is how 25 bone chips became 13. Same shape as AutoSellRx/AutoStoreRx.)
    [GeneratedRegex(@"^--You have looted (?:(?<n>\d+)|an?) (?<item>.+?) from (?<source>.+?)'s corpse\.--$")]
    private static partial Regex LootRx();

    // You have scrounged up a Ration. — Forage (#163, wizen). It is an acquisition
    // like any other, so it becomes a LootEvent with "Forage" as its source: the loot
    // card, the raw drop list, and every quest/gear auto-checker then see it for free,
    // which is the whole reason to route it here rather than invent a second path.
    // The failure line ("You fail to locate any food nearby.") is deliberately ignored
    // — a miss is not an event anyone wants counted.
    [GeneratedRegex(@"^You have scrounged up an? (?<item>.+?)\.$")]
    private static partial Regex ForageRx();

    // You looted a Crushbone Belt +2 from orc centurion's corpse to create a Crushbone Belt +5
    [GeneratedRegex(@"^You looted an? (?<item>.+?) from (?<source>.+?)'s corpse to create an? (?<result>.+?)\.?$")]
    private static partial Regex LootUpgradeRx();

    // You receive 2 silver and 2 copper from the corpse. | ... as your split.
    [GeneratedRegex(@"^You receive (?<coins>.+?) (?:from the corpse|as your split)\.$")]
    private static partial Regex MoneyRx();

    // You receive 7 gold 2 silver 3 copper from Lanadin for the Raw-Hide Sleeves +2(s).
    [GeneratedRegex(@"^You receive (?<coins>.+?) from .+? for the (?<item>.+?)\(s\)\.$")]
    private static partial Regex VendorSaleRx();

    // Aamilea healed you for 56 hit points by Light Healing.
    // HoT ticks: "Aenari healed you over time for 8 hit points by Echoing Light." — 223
    // such lines in one week of eqlog_Hugzee were invisible, undercounting healing received.
    [GeneratedRegex(@"^(?<healer>.+?) healed you(?<hot> over time)? for (?<amount>\d+)(?: \((?<attempted>\d+)\))? hit points(?: by (?<spell>.+?))?\.$")]
    private static partial Regex HealInByRx();

    // A willowisp resisted your Denon's Disruptive Discord! The target is captured
    // too: a resist proves an AWAKE creature of that name is standing there (#122).
    [GeneratedRegex(@"^(?<target>.+?) resisted your (?<spell>.+?)!$")]
    private static partial Regex ResistAltRx();

    // "An ashenbone drake has been awakened by Terrak." — the explicit mez-break
    // line (bystander-visible, names the waker; Snagglefern's PoH log, #122).
    [GeneratedRegex(@"^(?<target>.+?) has been awakened by (?<waker>.+?)\.$")]
    private static partial Regex MezAwakenedRx();

    [GeneratedRegex(@"^Your wounds begin to heal\.$")]
    private static partial Regex RegenTickRx();

    // Orc pawn scowls at you, ready to attack -- looks like a reasonably safe opponent. (Lvl: 3)
    // (Hugzee's log, 2026-08-06 — the only faction phrase observed in Legends so far;
    // the classic-EQ phrase family fills the alternation, anchored by the Legends-only
    // "(Lvl: N)" tail so a chat line can't satisfy it.)
    [GeneratedRegex(@"^(?<name>.+?) (?:scowls at you|regards you|glares at you|glowers at you|judges you|kindly considers you|looks upon you|looks your way).*\(Lvl: (?<level>\d+)\)$")]
    private static partial Regex ConsiderRx();

    // You gain a rune for 8 points of absorption. — a berserker/rune buff building its
    // absorption pool; functions as self-mitigation, so it's tracked as a self-heal.
    [GeneratedRegex(@"^You gain a rune for (?<amount>\d+) points? of absorption\.$")]
    private static partial Regex RuneAbsorbRx();

    // You assume a ranged stance. / You assume an offensive stance.
    [GeneratedRegex(@"^You assume an? (?<stance>.+?) stance\.$")]
    private static partial Regex StanceRx();

    // You begin reciting the empowering invocation. (observed: empowering,
    // unyielding, recovery — parsed open so new ones just work)
    [GeneratedRegex(@"^You begin reciting the (?<invocation>.+?) invocation\.$")]
    private static partial Regex InvocationRx();

    [GeneratedRegex(@"(?<n>\d+) (?<unit>platinum|gold|silver|copper)")]
    private static partial Regex CoinPartRx();

    // You gain party experience! (0.019%)  |  You gain experience! (0.5%)
    [GeneratedRegex(@"^You gain (?<party>party )?experience!(?: \((?<pct>[\d.]+)%\))?$")]
    private static partial Regex XpRx();

    // You have gained an ability point!  You now have 6 ability points.
    // You have gained 2 ability point(s)!  You now have 10 ability point(s).
    // The second form appears with AA potions (issue #37, twill713's verbatim line):
    // a digit count and a literal "(s)" parenthetical.
    [GeneratedRegex(@"^You have gained (?:an|(?<n>\d+)) ability point(?:s|\(s\))?! +You now have (?<total>\d+) ability point(?:s|\(s\))?\.$")]
    private static partial Regex AaRx();

    // You have gained the ability "Quick Buff" at a cost of 5 ability points.
    // You have improved Combat Fury 3 at a cost of 3 ability points.
    // You have improved Symphonic Aura: Enabled 4 at a cost of 0 ability points.
    // (Dranak/Hugzee logs, 2026-08-06.) "gained the ability" is the rank-1 purchase with the
    // name quoted; "improved <name> <rank>" is every later rank, unquoted with a trailing
    // rank number. Cost 0 marks innate grants and free toggle flips — parsed, not skipped:
    // the AA ledger wants those too (an innate can still change durations).
    [GeneratedRegex(@"^You have gained the ability ""(?<ability>.+?)"" at a cost of (?<cost>\d+) ability points?\.$")]
    private static partial Regex AaPurchaseRx();

    [GeneratedRegex(@"^You have improved (?<ability>.+?) (?<rank>\d+) at a cost of (?<cost>\d+) ability points?\.$")]
    private static partial Regex AaImproveRx();

    // You looted a Snake Egg from an asp's corpse and sold it for 4 copper.
    // You looted 2 Spider Silk from a giant spider's corpse and sold it for 2 gold, 8 silver and 6 copper.
    [GeneratedRegex(@"^You looted (?:(?<n>\d+)|an?) (?<item>.+?) from (?<source>.+?)'s corpse and sold it for (?<coins>.+?)\.$")]
    private static partial Regex AutoSellRx();

    // You looted a Mote of Major Potential from a spite golem's corpse and stored it in your currency
    // You looted a High Quality Bear Skin from a kodiak's corpse and stored it in your tradeskill depot
    // Auto-storage routing (issue #39, verbatim from joeymavity and shururuun): loot
    // that goes to currency / the tradeskill depot / the dragon hoard skips every
    // other loot line — this one has NO trailing period. For years the lore said
    // currency-routed motes wrote nothing; the game (now) says otherwise.
    [GeneratedRegex(@"^You looted (?:(?<n>\d+)|an?) (?<item>.+?) from (?<source>.+?)'s corpse and stored it in your (?<where>.+?)\.?$")]
    private static partial Regex AutoStoreRx();

    // You successfully destroyed 1 Spider Venom Sac.
    [GeneratedRegex(@"^You successfully destroyed (?<n>\d+) (?<item>.+?)\.$")]
    private static partial Regex DestroyedRx();

    // You received 3 gold, 5 silver and 7 copper from that item.  (advanced loot window sale)
    [GeneratedRegex(@"^You received (?<coins>.+?) from that item\.$")]
    private static partial Regex LootWindowSaleRx();

    // Your Befriend Animal spell has worn off of a puma. / Your Root spell has worn off.
    [GeneratedRegex(@"^Your (?<spell>.+?) spell has worn off(?: of (?<target>.+?))?\.$")]
    private static partial Regex SpellWornOffRx();

    // Your pet's Tangling Weeds spell has worn off.  — the PET's spell, not the player's.
    // Must be tested before SpellWornOffRx, which would otherwise capture the spell name
    // as "pet's Tangling Weeds" and let it match the player's spell-fade rules.
    [GeneratedRegex(@"^Your pet's (?<spell>.+?) spell has worn off(?: of (?<target>.+?))?\.$")]
    private static partial Regex PetSpellWornOffRx();

    [GeneratedRegex(@"^You have gained a level! Welcome to level (?<level>\d+)!$")]
    private static partial Regex LevelRx();

    [GeneratedRegex(@"^You have become better at (?<skill>.+?)! \((?<value>\d+)\)$")]
    private static partial Regex SkillUpRx();

    [GeneratedRegex(@"^Your faction standing with (?<faction>.+?) has been adjusted by (?<delta>-?\d+)\.$")]
    private static partial Regex FactionRx();

    // Your faction standing with Emerald Warriors could not possibly get any better.
    // The at-the-cap form — thousands of these in family logs, where farmed factions
    // "stopped moving" with no explanation on the Faction card.
    [GeneratedRegex(@"^Your faction standing with (?<faction>.+?) could not possibly get any (?<dir>better|worse)\.$")]
    private static partial Regex FactionCappedRx();

    [GeneratedRegex(@"^You have entered (?<zone>.+)\.$")]
    private static partial Regex ZoneRx();

    // Your Location is -1085.05, -675.53, 3.75   (Y first — EQ's axis order)
    [GeneratedRegex(@"^Your Location is (?<y>-?[\d.]+), (?<x>-?[\d.]+), (?<z>-?[\d.]+)$")]
    private static partial Regex LocationRx();

    // You have successfully merged two items together to create a new item: Crushbone Belt +5
    [GeneratedRegex(@"^You have successfully merged two items together to create a new item: (?<item>.+?)\.?$")]
    private static partial Regex MergeRx();

    [GeneratedRegex(@"^Your (?<spell>.+?) spell fizzles!$")]
    private static partial Regex FizzleRx();

    // You begin casting Stinging Swarm. / You begin to sing Solon's Song of the Sirens.
    // ("to sing" is the Legends form; "singing" kept in case older builds used it.)
    [GeneratedRegex(@"^You begin (?<how>casting|singing|to sing) (?<spell>.+?)\.$")]
    private static partial Regex CastStartRx();

    // Your Stinging Swarm spell is interrupted.
    [GeneratedRegex(@"^Your (?<spell>.+?) spell is interrupted\.$")]
    private static partial Regex CastInterruptedRx();

    // Your Chloroplast spell did not take hold. (Blocked by Regrowth.)
    // Your Chloroplast spell did not take hold.
    // The cast COMPLETED — this is neither an interrupt nor a fizzle — but the buff
    // never landed: another buff owns its stacking slot. Line shape documented in the
    // wild by EQ Legends Companion's research corpus (fact, not code). The parenthetical
    // is optional because the game sometimes omits the blocker; both names can carry
    // apostrophes, colons, and hyphens, so the captures are lazy up to literal tails.
    [GeneratedRegex(@"^Your (?<spell>.+?) spell did not take hold\.(?: \(Blocked by (?<blocker>.+?)\.\))?$")]
    private static partial Regex SpellBlockedRx();

    // Lines we suspect are meaningful but whose exact EQ Legends wording we haven't seen.
    // Rather than ship guessed regexes that silently never match, unmatched lines that
    // look interesting go to the debug sink during play, so the real text can be captured
    // and turned into a proper pattern with a fixture behind it.
    //
    //  - crowd-control landing lines ("X is mesmerized")
    //  - pet chatter other than "Attacking … Master.", which is the only pet-identifying
    //    line we currently parse. A summoned pet that is never given an attack order emits
    //    it, so its damage goes uncredited — every other pet command response is a
    //    candidate for closing that gap.
    [GeneratedRegex(@"mesmeriz|stunned|rooted|charmed|enthrall|entranc|pacif|lulled|snared|Master|your pet|my pet",
        RegexOptions.IgnoreCase)]
    private static partial Regex UnmatchedCandidateRx();

    /// <summary>Opt-in sink for unmatched lines that look like crowd-control effects or
    /// pet chatter. Wired up by the host only when debug capture is enabled; null in
    /// normal runs.</summary>
    public static Action<string>? UnmatchedCandidateSink { get; set; }

    // Third-party combat (group members, the player's pet, nearby fights):
    // "Orc centurion hits Lizzid for 4 points of damage." / "Lizzid tries to frenzy on orc centurion, but misses!"
    // "Orc centurion has taken 1 damage from Disease Cloud by Lizzid."
    // The trailing note is the same annotation your own hits carry — "Lizzid slashes orc
    // centurion for 13 points of damage. (Critical)" — so pets DO report crits.
    [GeneratedRegex(@"^(?<attacker>.+?) (?<verb>hits|slashes|kicks|bashes|pierces|crushes|punches|backstabs|bites|claws|mauls|gores|stings|strikes|slices|cleaves|smashes|rends|slams|shoots|reaves|smites|frenzies on) (?<target>.+?) for (?<dmg>\d+) points? of damage\.(?: \((?<note>[^)]+)\))?$")]
    private static partial Regex ThirdMeleeRx();

    [GeneratedRegex(@"^(?<attacker>.+?) tries to \w+(?: on)? .+?, but .+!(?: \([^)]+\))?$")]
    private static partial Regex ThirdMissRx();

    [GeneratedRegex(@"^(?<target>.+?) has taken (?<dmg>\d+) damage from (?<spell>.+?) by (?<caster>.+?)\.(?: \((?<note>[^)]+)\))?$")]
    private static partial Regex ThirdDotRx();

    // Pet responses split across two channels, and the split is by which response it is,
    // not by content (David's log, 2026-08-10): the attack order comes back as a directed
    // tell, EVERY other pet response comes back on say. Only two of them can prove the pet
    // is ours, and only those two are parsed:
    //
    //   - the attack order is a tell, addressed to us — a bystander's pet never tells us
    //     anything, so the speaker is ours by construction;
    //   - the leader response names its owner outright, which holds up even on say.
    //
    // "Following you, Master." is deliberately NOT parsed. It would be the most useful of
    // the three (out of combat, macro-able into the summon) but it names nobody and rides
    // the broadcast channel, so a nearby player's pet ordered to follow is indistinguishable
    // from our own — and _petName is a single slot, so honouring it could swap our pet's
    // damage out for a stranger's.
    //
    // Jibekn told you, 'Attacking orc centurion Master.'
    // A puma told you, 'Attacking a ghoul Master.'   (charmed creatures have multi-word names)
    [GeneratedRegex(@"^(?<pet>.+?) (?:tells|told) you, 'Attacking .+ Master\.'$")]
    private static partial Regex PetClaimRx();

    // Genektik says, 'My leader is Vataro.'
    // Both channels parse — the log writes this one on say, but a client that tells it
    // instead is no less ours. Leader is a player name: one word, no spaces. SessionStats
    // claims only when that name is the watched character.
    [GeneratedRegex(@"^(?<pet>.+?) (?:(?:tells|told) you|says), 'My leader is (?<leader>[A-Za-z]+)\.'$")]
    private static partial Regex PetLeaderRx();

    // Bzzazzt says, 'Now holding, Master.  I will not start attacks until ordered.'
    // Bzzazzt says, 'No longer holding, Master.'
    // The pet's OWN reply to /pet hold, so it names the pet — the player-side "The pet
    // hold mode has been set to on." carries no name and cannot say whose. A held pet
    // does not start attacks, which is what makes this worth parsing (#135, charm6.txt).
    [GeneratedRegex(@"^(?<pet>.+?) (?:(?:tells|told) you|says), '(?<no>No longer )?(?:Now )?[Hh]olding, Master")]
    private static partial Regex PetHoldRx();

    // "an asp blinks." — the landing line for every druid/shaman animal charm AND
    // Beguile Plants (eqlwiki msg_cast_on_other, verified 2026-08-04); provisional
    // pet signal.
    [GeneratedRegex(@"^(?<name>.+?) blinks\.$")]
    private static partial Regex PetBlinkRx();

    // "a skeleton moans." — the landing line for every NECRO undead charm (Dominate/
    // Beguile/Cajole Undead, Thrall of Bones, Enslave Death — eqlwiki). Weak: moaning
    // could be ambient flavor, so it only acts behind one of our own casts.
    [GeneratedRegex(@"^(?<name>.+?) moans\.$")]
    private static partial Regex MoanRx();

    // "a greater skeleton has been charmed." — the DIRECT charm-success line (found in
    // eqlog_Hugzee 2026-08-02, a Charm session in Befallen). Definitive where the blink
    // is circumstantial, and it beats the "Attacking … Master." tell by up to 9 s —
    // damage in that window used to go unattributed.
    [GeneratedRegex(@"^(?<name>.+?) has been charmed\.$")]
    private static partial Regex CharmedRx();

    // "ice boned skeleton has been mesmerized." — mez landing, bystander-visible
    // (field-verified in eqlog_Hugzee, incl. NPC mezzes on other players). The verb
    // set and the exotic phrasings come from eqlwiki's per-spell msg_cast_on_other
    // fields (researched 2026-08-04): Glamour/Melodious append "by …", Rapture swoons,
    // Screaming Terror screams, the bard songs nod/glaze/stumble.
    [GeneratedRegex(
        @"^(?<target>.+?)(?: has been (?:mesmerized|enthralled|entranced|fascinated)(?: by .+)?" +
        @"| swoons in raptured bliss| begins to scream| gawks at the glowing lights" +
        @"| stumbles toward you|'s (?:head nods|eyes glaze over))\.$")]
    private static partial Regex MezzedRx();

    // "Shack begins casting Shield of Thistles IV." — other players' and NPCs' casts,
    // with spell name and rank. ("You begin casting …" is the separate own-cast line.)
    [GeneratedRegex(@"^(?<caster>.+?) begins casting (?<spell>.+?)\.$")]
    private static partial Regex OtherCastRx();

    [GeneratedRegex(@"^Your target resisted the (?<spell>.+?) spell\.$")]
    private static partial Regex ResistRx();

    private const string TsFormat = "ddd MMM d HH:mm:ss yyyy";

    public static bool TryParseTimestamp(string line, out DateTime ts)
    {
        ts = default;
        var m = LineRx().Match(line);
        if (!m.Success) return false;
        var raw = Regex.Replace(m.Groups["ts"].Value, @" {2,}", " ");
        return DateTime.TryParseExact(raw, TsFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out ts);
    }

    /// <summary>Split a log line into its timestamp and message, without interpreting the
    /// message. For consumers that want the raw text — <see cref="WatchKind.Text"/> watch
    /// rules — rather than a parsed event.</summary>
    public static bool TrySplitLine(string line, out DateTime ts, out string message)
    {
        ts = default;
        message = "";
        var m = LineRx().Match(line);
        if (!m.Success) return false;
        // Single-digit days pad with a double space; only pay the regex when present
        // (the same guard Parse always had — perf audit #13's split-once path makes
        // this THE timestamp parse for every line, so it must be just as cheap).
        var raw = m.Groups["ts"].Value;
        if (raw.Contains("  ")) raw = Regex.Replace(raw, @" {2,}", " ");
        if (!DateTime.TryParseExact(raw, TsFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out ts))
            return false;
        message = m.Groups["msg"].Value;
        return true;
    }

    /// <summary>Parse one full log line. Returns null for lines we don't track.</summary>
    public static GameEvent? Parse(string line) =>
        TrySplitLine(line, out var ts, out var msg) ? Parse(ts, msg) : null;

    /// <summary>Parse an already-split line (perf audit #13: LogWatcher splits each
    /// line ONCE and hands the parts to both this and ObserveRawLine, instead of both
    /// re-running the line regex + timestamp parse per line).</summary>
    public static GameEvent? Parse(DateTime ts, string msg)
    {
        Match r;

        // Fast path for /loc: if players take the overlapping-keybind route (the
        // social bound to a movement key, David 2026-08-10), this becomes one of
        // the most frequent lines in the log — and its literal prefix is unambiguous,
        // so it never needs to pay for the cascade below.
        if (msg.StartsWith("Your Location is ", StringComparison.Ordinal)
            && (r = LocationRx().Match(msg)).Success)
            return new LocationEvent(ts,
                double.Parse(r.Groups["y"].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(r.Groups["x"].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(r.Groups["z"].Value, System.Globalization.CultureInfo.InvariantCulture));

        if ((r = YouDiedRx().Match(msg)).Success)
            return new DeathEvent(ts, r.Groups["killer"].Value);

        // EQ Legends logs two shapes for your own death, both preceded by "You have been
        // knocked unconscious!":
        //   "You have been slain by Guard Dunil!"  — a direct attack landed the killing blow
        //   "You died."                            — no killer named
        // Observed in real logs: the plain form is what a damage-over-time tick produces
        // (eqlog_Hugzee 2026-07-29 15:59:01, four DoTs landing in the same second). Parsing
        // only the first form meant DoT deaths went uncounted entirely. The unconscious line
        // is deliberately NOT parsed — it precedes both, and would double-count.
        if (YouDiedPlainRx().IsMatch(msg))
            return new DeathEvent(ts, "");

        // The RAW target decides ProperName — Normalize is about to destroy the article
        // that answers it (#185).
        if ((r = YouSlainRx().Match(msg)).Success)
            return new KillEvent(ts, Normalize(r.Groups["target"].Value), "You",
                NamedMobHeuristic.LooksProperName(r.Groups["target"].Value));

        if ((r = OtherSlainRx().Match(msg)).Success)
            return new KillEvent(ts, Normalize(r.Groups["target"].Value), r.Groups["killer"].Value,
                NamedMobHeuristic.LooksProperName(r.Groups["target"].Value));

        if ((r = MeleeInRx().Match(msg)).Success)
            return new DamageTakenEvent(ts, Normalize(r.Groups["attacker"].Value),
                int.Parse(r.Groups["dmg"].Value), Melee: true,
                Ability: ThirdVerbToSkill(r.Groups["verb"].Value));

        if ((r = NonMeleeInRx().Match(msg)).Success)
        {
            // The capture is HOW you were hurt; the creature is the possessive
            // owner inside it. Falls back to the whole phrase for ownerless
            // forms — same as before, but named forms now credit the real mob.
            var how = r.Groups["how"].Value;
            var owner = DamageShieldOwnerRx().Match(how);
            return new DamageTakenEvent(ts,
                Normalize(owner.Success ? owner.Groups["attacker"].Value : how),
                int.Parse(r.Groups["dmg"].Value), Melee: false);
        }

        if ((r = SchoolHitInRx().Match(msg)).Success)
            return new DamageTakenEvent(ts, Normalize(r.Groups["attacker"].Value),
                int.Parse(r.Groups["dmg"].Value), Melee: false,
                Ability: r.Groups["spell"].Value);

        if ((r = DotInRx().Match(msg)).Success)
            return new DamageTakenEvent(ts, Normalize(r.Groups["attacker"].Value),
                int.Parse(r.Groups["dmg"].Value), Melee: false,
                Ability: r.Groups["spell"].Value, OverTime: true);

        // Perf audit #5: in a full group, other people's swings and misses are the
        // most frequent lines in the log — and they used to fail thirty-odd
        // first-person patterns on the way to the bottom of this cascade (16–18 µs
        // per line, the dominant cost of big replays). Hoisted here: above every
        // ^.+?-prefixed pattern they were paying for, and guarded away from the
        // first-person shapes — lines starting "You…" are yours, and lines carrying
        // uppercase "YOU" are aimed at you (incoming hits, misses, rune blocks all
        // still live further down, unshadowed).
        if (!msg.StartsWith("You", StringComparison.Ordinal) &&
            !msg.Contains("YOU", StringComparison.Ordinal))
        {
            if ((r = ThirdMeleeRx().Match(msg)).Success)
                return new ThirdMeleeEvent(ts, r.Groups["attacker"].Value.Trim(),
                    Normalize(r.Groups["target"].Value), int.Parse(r.Groups["dmg"].Value),
                    ThirdVerbToSkill(r.Groups["verb"].Value), IsCritNote(r));

            if ((r = ThirdMissRx().Match(msg)).Success)
                return new ThirdMissEvent(ts, r.Groups["attacker"].Value.Trim());
        }

        if ((r = SelfHurtRx().Match(msg)).Success)
            return new DamageTakenEvent(ts, "Yourself",
                int.Parse(r.Groups["dmg"].Value), Melee: false, Self: true);

        if ((r = DamageShieldRx().Match(msg)).Success)
            return new DamageDealtEvent(ts, Normalize(r.Groups["target"].Value),
                int.Parse(r.Groups["dmg"].Value), DamageKind.Spell, "Damage shield",
                Critical: false, IsAux: true);

        if ((r = SchoolNukeOutRx().Match(msg)).Success)
            return new DamageDealtEvent(ts, Normalize(r.Groups["target"].Value),
                int.Parse(r.Groups["dmg"].Value), DamageKind.Spell, r.Groups["spell"].Value,
                IsCritNote(r));

        if ((r = NukeOutRx().Match(msg)).Success)
            return new DamageDealtEvent(ts, Normalize(r.Groups["target"].Value),
                int.Parse(r.Groups["dmg"].Value), DamageKind.Spell, "Direct spell",
                IsCritNote(r));

        if ((r = ItemProcRx().Match(msg)).Success)
            return new ItemProcEvent(ts, r.Groups["item"].Value);

        if ((r = MeleeOutRx().Match(msg)).Success)
            return new DamageDealtEvent(ts, Normalize(r.Groups["target"].Value),
                int.Parse(r.Groups["dmg"].Value), DamageKind.Melee,
                VerbToSkill(r.Groups["verb"].Value), IsCritNote(r), Note: NoteOf(r));

        if ((r = DotOutRx().Match(msg)).Success)
            return new DamageDealtEvent(ts, Normalize(r.Groups["target"].Value),
                int.Parse(r.Groups["dmg"].Value), DamageKind.Spell,
                r.Groups["spell"].Value, IsCritNote(r), OverTime: true);

        if ((r = MeleeMissRx().Match(msg)).Success)
            return new MissEvent(ts, Outgoing: true,
                Ability: VerbToSkill(r.Groups["verb"].Value),
                Reason: r.Groups["reason"].Value.TrimEnd('!'),
                Target: Normalize(r.Groups["target"].Value));

        if ((r = RuneBlockInRx().Match(msg)).Success)
            return new RuneBlockEvent(ts, Normalize(r.Groups["attacker"].Value));

        if ((r = MeleeInMissRx().Match(msg)).Success)
            return new MissEvent(ts, Outgoing: false);

        if ((r = HealOutRx().Match(msg)).Success)
            return new HealEvent(ts, r.Groups["target"].Value,
                int.Parse(r.Groups["amount"].Value),
                r.Groups["spell"].Success ? r.Groups["spell"].Value : "Unknown", Outgoing: true,
                OverTime: r.Groups["hot"].Success);

        if ((r = HealInRx().Match(msg)).Success)
            return new HealEvent(ts, "You", int.Parse(r.Groups["amount"].Value), "Unknown", Outgoing: false);

        if (RegenTickRx().IsMatch(msg))
            return new RegenTickEvent(ts);

        if ((r = RuneAbsorbRx().Match(msg)).Success)
            return new HealEvent(ts, "You", int.Parse(r.Groups["amount"].Value), "Rune",
                Outgoing: false, Healer: "Rune");

        if ((r = HealInByRx().Match(msg)).Success)
            return new HealEvent(ts, "You", int.Parse(r.Groups["amount"].Value),
                r.Groups["spell"].Success ? r.Groups["spell"].Value : "Unknown",
                Outgoing: false, Healer: r.Groups["healer"].Value,
                OverTime: r.Groups["hot"].Success);

        if ((r = AutoStoreRx().Match(msg)).Success)
            return new LootEvent(ts, r.Groups["item"].Value, Normalize(r.Groups["source"].Value),
                UpgradeResult: null,
                Count: r.Groups["n"].Success ? int.Parse(r.Groups["n"].Value) : 1);

        if ((r = AutoSellRx().Match(msg)).Success)
            return new AutoSellEvent(ts, r.Groups["item"].Value,
                r.Groups["n"].Success ? int.Parse(r.Groups["n"].Value) : 1,
                Normalize(r.Groups["source"].Value), ParseCoins(r.Groups["coins"].Value));

        if ((r = LootUpgradeRx().Match(msg)).Success)
            return new LootEvent(ts, r.Groups["item"].Value, Normalize(r.Groups["source"].Value),
                r.Groups["result"].Value);

        if ((r = LootRx().Match(msg)).Success)
            return new LootEvent(ts, r.Groups["item"].Value, Normalize(r.Groups["source"].Value), null,
                Count: r.Groups["n"].Success ? int.Parse(r.Groups["n"].Value) : 1);

        if ((r = ForageRx().Match(msg)).Success)
            return new LootEvent(ts, r.Groups["item"].Value, "Forage", null);

        if ((r = MoneyRx().Match(msg)).Success)
        {
            var copper = ParseCoins(r.Groups["coins"].Value);
            if (copper > 0) return new MoneyEvent(ts, copper);
        }

        if ((r = VendorSaleRx().Match(msg)).Success)
        {
            var copper = ParseCoins(r.Groups["coins"].Value);
            if (copper > 0) return new MoneyEvent(ts, copper, Vendor: true, Item: r.Groups["item"].Value);
        }

        if ((r = DestroyedRx().Match(msg)).Success)
            return new ItemDestroyedEvent(ts, r.Groups["item"].Value, int.Parse(r.Groups["n"].Value));

        if ((r = LootWindowSaleRx().Match(msg)).Success)
        {
            var copper = ParseCoins(r.Groups["coins"].Value);
            if (copper > 0) return new MoneyEvent(ts, copper, Vendor: true, Item: null);
        }

        if ((r = XpRx().Match(msg)).Success)
            return new XpEvent(ts,
                r.Groups["pct"].Success ? double.Parse(r.Groups["pct"].Value, CultureInfo.InvariantCulture) : 0,
                r.Groups["party"].Success);

        // Buff/HoT wear-off flavor lines never name their spell — the catalog does.
        // Exact-message dictionary lookup: one hash probe per line, no regex cost.
        if (FadeMessageCatalog.Default.Find(msg) is { } fade)
            return new BuffFadeEvent(ts, fade.Label, fade.Spells, fade.Category);

        // Slow landing lines ("You feel lethargic.") — same exact-match economics.
        // The harvest guarantees the two catalogs never claim the same message.
        if (SlowDebuffCatalog.Default.Find(msg) is not null)
            return new SlowLandedEvent(ts, msg);

        // Other detrimental landing lines ("You feel somewhat vulnerable.") — same
        // exact-match economics, and the harvest guarantees no message is claimed by
        // two catalogs. Read only by the buff-loss history (#120).
        if (DebuffLandingCatalog.Default.Find(msg) is not null)
            return new DebuffLandedEvent(ts, msg);

        // "You forget <song>." — the SlowTracker's haste-vs-slow disambiguator (#116).
        if (msg.StartsWith("You forget ", StringComparison.Ordinal) && msg.EndsWith('.'))
            return new SongForgottenEvent(ts, msg["You forget ".Length..^1]);

        // A colliding haste song's pulse landing on you — the group-member
        // disambiguator (a bard's songs never log in a groupmate's file; this
        // line is the only witness). Catalog-driven: the harvest keeps the set of
        // landing lines current as the wiki documents new songs.
        if (SlowDebuffCatalog.Default.IsHasteLanding(msg))
            return new HasteSongLandedEvent(ts);

        // Buff landing lines — third exact-match catalog, same disjointness guarantee.
        if (BuffDurationCatalog.Default.Find(msg) is not null)
            return new BuffLandedEvent(ts, msg);

        // Raid chat proves raid membership (the log carries no other raid signal).
        // Both directions: "Xxx tells the raid, '…'" and your own "You tell your raid, '…'".
        if (msg.Contains(" tells the raid, '", StringComparison.Ordinal)
            || msg.StartsWith("You tell your raid, '", StringComparison.Ordinal))
            return new RaidChatterEvent(ts);

        if ((r = PetSpellWornOffRx().Match(msg)).Success)
            return new SpellWornOffEvent(ts, r.Groups["spell"].Value,
                r.Groups["target"].Success ? Normalize(r.Groups["target"].Value) : "", Pet: true);

        if ((r = SpellWornOffRx().Match(msg)).Success)
            return new SpellWornOffEvent(ts, r.Groups["spell"].Value,
                r.Groups["target"].Success ? Normalize(r.Groups["target"].Value) : "");

        if ((r = LevelRx().Match(msg)).Success)
            return new LevelEvent(ts, int.Parse(r.Groups["level"].Value));

        if ((r = AaRx().Match(msg)).Success)
            return new AaEvent(ts, int.Parse(r.Groups["total"].Value),
                Points: r.Groups["n"].Success ? int.Parse(r.Groups["n"].Value) : 1);

        if ((r = AaPurchaseRx().Match(msg)).Success)
            return new AaPurchaseEvent(ts, r.Groups["ability"].Value, Rank: 1,
                int.Parse(r.Groups["cost"].Value));

        if ((r = AaImproveRx().Match(msg)).Success)
            return new AaPurchaseEvent(ts, r.Groups["ability"].Value,
                int.Parse(r.Groups["rank"].Value), int.Parse(r.Groups["cost"].Value));

        if ((r = SkillUpRx().Match(msg)).Success)
            return new SkillUpEvent(ts, r.Groups["skill"].Value, int.Parse(r.Groups["value"].Value));

        if ((r = SkillSubstitutionRx().Match(msg)).Success)
            return new SkillSubstitutionEvent(ts, r.Groups["ability"].Value, r.Groups["replaced"].Value);

        if ((r = FactionRx().Match(msg)).Success)
            return new FactionEvent(ts, r.Groups["faction"].Value, int.Parse(r.Groups["delta"].Value));

        if ((r = ConsiderRx().Match(msg)).Success)
            return new ConsiderEvent(ts, Normalize(r.Groups["name"].Value),
                int.Parse(r.Groups["level"].Value));

        if ((r = FactionCappedRx().Match(msg)).Success)
            return new FactionEvent(ts, r.Groups["faction"].Value, 0, Capped: true,
                CappedDown: r.Groups["dir"].Value == "worse");

        if ((r = MergeRx().Match(msg)).Success)
            return new CraftEvent(ts, r.Groups["item"].Value);

        if ((r = CastStartRx().Match(msg)).Success)
            return new SpellCastEvent(ts, r.Groups["spell"].Value,
                Song: r.Groups["how"].Value != "casting");

        if ((r = CastInterruptedRx().Match(msg)).Success)
            return new SpellInterruptedEvent(ts, r.Groups["spell"].Value);

        if ((r = SpellBlockedRx().Match(msg)).Success)
            return new SpellBlockedEvent(ts, r.Groups["spell"].Value,
                r.Groups["blocker"].Success ? r.Groups["blocker"].Value : "");

        if ((r = FizzleRx().Match(msg)).Success)
            return new FizzleEvent(ts, r.Groups["spell"].Value);

        if ((r = ResistRx().Match(msg)).Success)
            return new ResistEvent(ts, r.Groups["spell"].Value);

        if ((r = ResistAltRx().Match(msg)).Success)
            return new ResistEvent(ts, r.Groups["spell"].Value,
                Normalize(r.Groups["target"].Value));

        if ((r = StanceRx().Match(msg)).Success)
            return new StanceEvent(ts, Normalize(r.Groups["stance"].Value));

        if ((r = InvocationRx().Match(msg)).Success)
            return new InvocationEvent(ts, Normalize(r.Groups["invocation"].Value));

        // Pet announcement and third-party combat (checked last — specific patterns above win).
        if ((r = PetClaimRx().Match(msg)).Success)
            return new PetClaimEvent(ts, r.Groups["pet"].Value);

        // The leader response answers a question, in or out of combat — no attack happened,
        // so it is no evidence of a fight.
        if ((r = PetLeaderRx().Match(msg)).Success)
            return new PetClaimEvent(ts, r.Groups["pet"].Value,
                Leader: r.Groups["leader"].Value, Fighting: false);

        // Hold is checked before the generic claim lines: it is also a pet response, and
        // its extra fact (the pet will not attack) is the whole point.
        if ((r = PetHoldRx().Match(msg)).Success)
            return new PetHoldEvent(ts, r.Groups["pet"].Value, Holding: !r.Groups["no"].Success);

        if ((r = CharmedRx().Match(msg)).Success)
            return new CharmedEvent(ts, r.Groups["name"].Value);

        if ((r = MezzedRx().Match(msg)).Success)
            return new MezzedEvent(ts, Normalize(r.Groups["target"].Value));

        if ((r = MezAwakenedRx().Match(msg)).Success)
            return new MezAwakenedEvent(ts, Normalize(r.Groups["target"].Value),
                Normalize(r.Groups["waker"].Value));

        if ((r = OtherCastRx().Match(msg)).Success)
            return new OtherCastEvent(ts, Normalize(r.Groups["caster"].Value), r.Groups["spell"].Value);

        if ((r = PetBlinkRx().Match(msg)).Success)
            return new PetBlinkEvent(ts, r.Groups["name"].Value);

        if ((r = MoanRx().Match(msg)).Success)
            return new PetBlinkEvent(ts, r.Groups["name"].Value, Weak: true);

        // Fallback for the lines the hoisted fast path's guard skips on purpose —
        // "Your pet hits …" starts with "You" but is a third-party shape. Ordinary
        // third-party lines returned up top and never reach here (audit #5).
        if ((r = ThirdMeleeRx().Match(msg)).Success)
            return new ThirdMeleeEvent(ts, r.Groups["attacker"].Value.Trim(),
                Normalize(r.Groups["target"].Value), int.Parse(r.Groups["dmg"].Value),
                ThirdVerbToSkill(r.Groups["verb"].Value), IsCritNote(r));

        if ((r = ThirdDotRx().Match(msg)).Success)
            return new ThirdDotEvent(ts, r.Groups["caster"].Value.Trim(),
                Normalize(r.Groups["target"].Value), int.Parse(r.Groups["dmg"].Value),
                r.Groups["spell"].Value, IsCritNote(r));

        if ((r = ThirdSchoolRx().Match(msg)).Success)
            return new ThirdSchoolEvent(ts, r.Groups["attacker"].Value.Trim(),
                Normalize(r.Groups["target"].Value), int.Parse(r.Groups["dmg"].Value),
                r.Groups["spell"].Value, IsCritNote(r));

        if ((r = ThirdMissRx().Match(msg)).Success)
            return new ThirdMissEvent(ts, r.Groups["attacker"].Value.Trim());

        if ((r = LocationRx().Match(msg)).Success)
            return new LocationEvent(ts,
                double.Parse(r.Groups["y"].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(r.Groups["x"].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(r.Groups["z"].Value, System.Globalization.CultureInfo.InvariantCulture));

        if ((r = ZoneRx().Match(msg)).Success)
        {
            var zone = r.Groups["zone"].Value;
            // Filter non-zone "You have entered ..." flavor messages (e.g. "an area where levitation ...").
            if (!zone.StartsWith("an area", StringComparison.OrdinalIgnoreCase) &&
                !zone.Contains("area where", StringComparison.OrdinalIgnoreCase))
                return new ZoneEvent(ts, zone);
        }

        if (UnmatchedCandidateSink is { } sink && UnmatchedCandidateRx().IsMatch(msg))
        {
            try { sink(msg); } catch { /* debug capture must never break parsing */ }
        }

        return null;
    }

    private static string? NoteOf(Match m) =>
        m.Groups["note"].Success ? m.Groups["note"].Value : null;

    /// <summary>A trailing "(...)" note that means the hit was a critical ("Riposte Critical" counts).</summary>
    private static bool IsCritNote(Match m)
    {
        var note = m.Groups["note"];
        return note.Success &&
               (note.Value.Contains("Critical") || note.Value is "Crippling Blow");
    }

    private static long ParseCoins(string coins)
    {
        long copper = 0;
        foreach (Match c in CoinPartRx().Matches(coins))
        {
            long n = long.Parse(c.Groups["n"].Value);
            copper += c.Groups["unit"].Value switch
            {
                "platinum" => n * 1000,
                "gold" => n * 100,
                "silver" => n * 10,
                _ => n,
            };
        }
        return copper;
    }

    /// <summary>
    /// Capitalize creature names consistently and strip leading articles so
    /// "orc pawn", "An orc pawn", and "Orc pawn" all count as the same mob.
    /// </summary>
    public static string Normalize(string name)
    {
        name = name.Trim();
        foreach (var article in (string[])["a ", "an ", "the "])
        {
            if (name.Length > article.Length &&
                name.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                name = name[article.Length..];
                break;
            }
        }
        if (name.Length == 0) return name;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string VerbToSkill(string verb) => verb switch
    {
        "frenzy on" or "frenzies on" => "Frenzy",
        "hit" => "Hit",
        "shoot" => "Archery",
        _ => char.ToUpperInvariant(verb[0]) + verb[1..],
    };

    /// <summary>Third-person verbs ("bashes") reduce to the same skill labels as the
    /// player's own first-person forms, so a pet's Bash reads like yours. Only "-shes"
    /// and "-ches" need the whole "-es" dropped; the rest lose just the "s".</summary>
    private static string ThirdVerbToSkill(string verb) => VerbToSkill(
        verb == "frenzies on" ? "frenzy on"
        : verb.EndsWith("shes", StringComparison.Ordinal) || verb.EndsWith("ches", StringComparison.Ordinal)
            ? verb[..^2]
            : verb[..^1]);
}
