# EQL Wiki AA Harvest Report

Harvested: 2026-08-06 from https://eqlwiki.com/wiki/Alternate_Advancement (MediaWiki API)

## Discovery route

- `list=embeddedin` for Template:AApage / Template:AAPage / Template:AA: **empty**
- `list=categorymembers` for Category:Alternate Advancement / Category:AAs: **empty**
- `list=search` for "Alternate Advancement": **1 hit** - the single page `Alternate Advancement` (pageid 56762, 38,550 bytes)

**eqlwiki has no per-AA pages.** The whole catalog is 19 uniform wikitables (`Name / Ranks / Cost / Description`) on that one page, sectioned as General, Archetype, one table per class (16 classes), and Special. This report parses those tables.

## Totals

- **Total abilities: 144**
  - General: 31
  - Archetype: 34
  - Class: 78
  - Special: 1
  - Per class: Bard 7, Beastlord 5, Berserker 4, Cleric 6, Druid 3, Enchanter 2, Magician 5, Monk 5, Necromancer 5, Paladin 7, Ranger 4, Rogue 6, Shadow Knight 6, Shaman 2, Warrior 6, Wizard 5

Cost and effect numbers are slash-separated per rank exactly as the wiki gives them; `?` means the wiki itself doesn't know the value (unconfirmed ranks). Nothing is invented.

## AAs with DURATION / TIMING effects

Keyword scan (case-insensitive) of effect text for: duration, extend, mesmeri, charm, root, snare, lull, buff, tick, recast, reuse, faster, haste, regen.
**27 abilities matched.** Exact effect sentences quoted; per-rank numbers pulled from each quoted sentence.

### Adamant Will (General; 4 rank(s), cost 2/4/6/9)
*Requirements: Level 1.*

- Keywords `mesmeri, charm`:
  > This passive ability grants you an additional 20/40/60/80% chance to resist charm, and 15/30/45/60% chance to resist mesmerization spells.
  - Per-rank numbers: 20/40/60/80%; 15/30/45/60%

### Circular Breathing (General; 4 rank(s), cost 2/3/4/5)
*Requirements: Level 1.*

- Keywords `regen`:
  > This passive ability increases your endurance regeneration by 1/2/3/4 point(s).
  - Per-rank numbers: 1/2/3/4

### Innate Regeneration (General; 7 rank(s), cost 1/1/1/2/3/5/5)
*Requirements: Level 1.*

- Keywords `regen`:
  > This passive ability increases your health regeneration by 1/1/1/1/1/1/1 point(s).
  - Per-rank numbers: 1/1/1/1/1/1/1

### Permanent Illusion (General; 1 rank(s), cost 5)
*Requirements: Level 1.*

- Keywords `duration, extend`:
  > This passive ability extends the duration of your beneficial illusion spells to 16.6 hours and allows them to persist when zoning.

### Healing Adept (Archetype; 3 rank(s), cost 2/4/6)
*Requirements: Level 1.*

- Keywords `duration`:
  > This passive ability increases the effectiveness of your instant-duration healing spells by 2/5/10%.
  - Per-rank numbers: 2/5/10%

### Healing Gift (Archetype; 3 rank(s), cost 2/4/6)
*Requirements: Level 1.*

- Keywords `duration`:
  > This passive ability grants your instant-duration healing spells a 3%/6%/10% chance to score an exceptional heal.
  - Per-rank numbers: 3%/6%/10%

### Mass Group Buff (Archetype; 1 rank(s), cost 9)
*Requirements: Level 50.*

- Keywords `buff`:
  > This ability, when activated, doubles the mana cost of your next spell or ability that can be affected by the Mass Group Buff and causes it to land on all allies within the spell's radius.

### Mental Clarity (Archetype; 4 rank(s), cost 2/3/4/5)
*Requirements: Level 1.*

- Keywords `regen`:
  > This passive ability increases your mana regeneration by 1 points per rank.

### Pet Affinity (Archetype; 1 rank(s), cost 2)
*Requirements: level 1.<br>NOTE: This AA is not required for Quick Buff to affect your pet for single-target spells. This AA is required for "group" spells to affect your pet. Requirements: Level 1.*

- Keywords `buff`:
  > Requirements: level 1.<br>NOTE: This AA is not required for Quick Buff to affect your pet for single-target spells.

### Spell Casting Deftness (Archetype; 3 rank(s), cost 2/4/6)
*Requirements: level 1. Requirements: Level 1.*

- Keywords `duration`:
  > This passive ability reduces the cast time of beneficial spells that have a duration and an initial cast time of at least 3 seconds by 10/25/50%.
  - Per-rank numbers: 10/25/50%

### Spell Casting Reinforcement (Archetype; 4 rank(s), cost 2/4/6/8)
*Requirements: Level 1.*

- Keywords `duration`:
  > This passive ability increases the duration of beneficial spells that you cast by 5/15/30/50%.
  - Per-rank numbers: 5/15/30/50%

### Thief's Intuition (Archetype; 4 rank(s), cost 3/?/?/?)
*Requirements: Level 1.*

- Keywords `reuse`:
  > This passive ability reduces the reuse time of your Sense Traps and Disarm Traps skills by 1/?/?/?
  - Per-rank numbers: 1/?/?/?

### Reaching Notes (Bard; 6 rank(s), cost 2/4/6/?/?/?)
*Requirements: Level 1.*

- Keywords `extend`:
  > When enabled, this passive ability extends the radius of your beneficial area songs by 10%.

### Hobble of Spirits (Beastlord; 1 rank(s), cost 5)
*Requirements: Level 30.*

- Keywords `snare`:
  > This ability, when activated, grants your pet's melee attacks a chance (with a 150% bonus) to trigger Hobble of Spirits Snare I which reduces its target's movement speed by 40% for 24 seconds.
- Keywords `duration, buff`:
  > This buff has a permanent duration and a 3 second cast time.

### Paragon of Spirit (Beastlord; 1 rank(s), cost 6)
*Requirements: Level 50.*

- Keywords `regen`:
  > Paragon of Spirit I, when activated, shares your natural attunement with all group members within a 200 foot radius, increasing health regeneration by 200 points and mana regeneration by 80 points for 0:00:36.

### Enhanced Root (Druid; 1 rank(s), cost 5)
*Requirements: Level 1.*

- Keywords `root`:
  > This passive ability reduces the chance that an NPC target entangled by your root spells will break free when struck by a non-melee attack by 50%.

### Tricksters Misdirection (Enchanter; 1 rank(s), cost 9)
*Requirements: Level 50.*

- Keywords `buff`:
  > When triggered, casts Tricksters Misdirection, a defensive proc buff lasting 1 minute, with 1 charge.

### Unbound Clarity (Enchanter; 3 rank(s), cost 0)
*Requirements: Level 12/30/50.*

- Keywords `regen`:
  > This passive increases the Enchanters mana regeneration by 2/4/6 points.
  - Per-rank numbers: 2/4/6

### Companion's Fury (Magician; 1 rank(s), cost 6)
*Requirements: Level 15.*

- Keywords `haste`:
  > Frenzied Burnout I increases your pet's armor class by 75 points, overhaste by 15%, strength by 20 points, attack power by 200 points, chance to perform a flurry of attacks on a successful double attack by 5%, and accuracy by 10%.

### Purify Body (Monk; 1 rank(s), cost 9)
*Requirements: Level 15.*

- Keywords `charm`:
  > Purification I, when activated, instantly cures you of up to 20 detrimental effects (excluding charm, fear, resurrection, and revival sickness).

### Rapid Feign (Monk; 3 rank(s), cost 3/6/9)
*Requirements: Level 17.*

- Keywords `reuse`:
  > This passive ability reduces the reuse time of your Feign Death skill by 1/3/5 second(s).
  - Per-rank numbers: 1/3/5

### Unbound Alacrity (Monk; 3 rank(s), cost 0)
*Requirements: Level 12/30/50.*

- Keywords `haste`:
  > Gives a passive 3/6/10% increase in your current and maximum haste value.
  - Per-rank numbers: 3/6/10%

### Dead Mesmerization (Necromancer; 1 rank(s), cost 3)
*Requirements: Level 40.*

- Keywords `mesmeri`:
  > Dead Mesmerization I, when activated, mesmerizes up to 12 level 59 or lower undead creatures within a 35 foot radius of your target for 0:00:36.

### Unbound Lethality (Rogue; 3 rank(s), cost 0)
*Requirements: Level 12/30/50.*

- Keywords `duration`:
  > Gives a passive 10/15/20% bonus to the duration of all poisons.
  - Per-rank numbers: 10/15/20%

### Warrior's Endurance (Warrior; 1 rank(s), cost 6)
*Requirements: Level 30.*

- Keywords `regen`:
  > This passive ability increases your hit point regeneration by 1% per 6 seconds.

### Improved Familiar (Wizard; 1 rank(s), cost 6)
*Requirements: Level 45.*

- Keywords `regen`:
  > Improved Familiar I, which when activated, triggers Summon Improved Familiar I, the physical manifestation of your familiar, increases the damage dealt by your critical direct damage spells by 3%, the casting levels of your spells by 9, your cold, disease, fire, magic, and poison resistances by 25 points, your mana regeneration by 6 points, your maximum mana by 200 points, and allows you to see invisible creatures.

### Strong Root (Wizard; 1 rank(s), cost 5)
*Requirements: Level 35.*

- Keywords `root`:
  > This ability, when activated, roots your target in place for up to 48 seconds with a 300 point resist modifier and a 2 second cast time.

## Unparseable rows

None - every table row parsed cleanly into the 4-column schema.
