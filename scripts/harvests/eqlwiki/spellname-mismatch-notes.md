# eqlwiki contribution notes — 24 spell pages whose `spellname` is not the page title

**Wiki-first, per Helm's 2026-08-31 ruling on PR #256 and the standing rule that eqlwiki is
the SOURCE and EQBuddy is the tool that helps it update.** These are paste-ready so a player
(or Scribe) can open each page and fix one field. **Nothing here publishes itself** — every
row is a suggestion a human opens, reviews and saves under their own account.

## What the defect is

Each of these 24 spell pages carries a `spellname=` field that does not match the page's own
title, and in every case the page's DESCRIPTION describes the title, not the `spellname`.
`Healing Water` declares `spellname = Greater Healing` while its own text describes a
425-point conjured heal; `Circle of Butcherblock` declares `spellname = Ring of South Ro`
while its own text says it "transports your group to the Butcherblock Mountains".

So `spellname` is a copy-paste artefact of the page template — a page was started from a
sibling spell's page and that one field was never updated. This is eqlwiki's own conclusion
as much as ours: `class-spells-harvest.py`'s docstring reached it in August, when
`spellname`-based alias resolution produced pairings like
`Circle of Butcherblock -> Ring of South Ro`.

## Why it is worth fixing on the wiki rather than only in EQBuddy

EQBuddy no longer depends on it — `spell-levels-promote.py` now keys the description fallback
on the page TITLE, so all 1,353 shipped spells carry prose again. But any other tool that
reads `spellname` as the canonical name inherits the bad pairing, and a reader who lands on
`Healing Water` and sees `Greater Healing` in the infobox has no way to tell which is right.
One field, 24 pages, and the wiki gets more accurate for everyone.

**Two kinds, and the second is the one worth care.** Thirteen are a typo or an abbreviation
of the same spell (`Leach` for `Leech`). Eleven name a genuinely DIFFERENT spell that exists
in its own right, so a reader cannot tell it is an error without opening the page — those are
the ones actively misleading.

## The 24

For each: open the edit link, change the `spellname=` line to the page title, save. Do not
change the description, the class list, or anything else.

| Page (correct name) | `spellname` says | Kind | Edit |
|---|---|---|---|
| `Blast of Cold` | `Shock of Frost` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Blast_of_Cold&action=edit) |
| `Cantata of Soothing` | `Cantana of Soothing` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Cantata_of_Soothing&action=edit) |
| `Circle of Butcherblock` | `Ring of South Ro` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Circle_of_Butcherblock&action=edit) |
| `Circle of North Karana` | `Circle of Karana` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Circle_of_North_Karana&action=edit) |
| `Evacuate: Nektulos` | `Evacuate: Greater Nektulos` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Evacuate%3A_Nektulos&action=edit) |
| `Healing Water` | `Greater Healing` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Healing_Water&action=edit) |
| `Illusion: Half-Elf` | `Illusion: Half Elf` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Illusion%3A_Half-Elf&action=edit) |
| `Illusion: Imp` | `Illusion: Air Elemental` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Illusion%3A_Imp&action=edit) |
| `Improved Superior Camouflage` | `Improved Superior Camo` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Improved_Superior_Camouflage&action=edit) |
| `Katta's Song of Sword Dancing` | `Aria of Asceticism` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Katta%27s_Song_of_Sword_Dancing&action=edit) |
| `Leech` | `Leach` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Leech&action=edit) |
| `Malaisement` | `Malisement` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Malaisement&action=edit) |
| `Markar's Clash` | `` Markar`s Clash `` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Markar%27s_Clash&action=edit) |
| `Mass Imbue Emerald` | `Imbue Emerald` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Mass_Imbue_Emerald&action=edit) |
| `Melody of Ervaj` | `Song: Melody of Ervaj` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Melody_of_Ervaj&action=edit) |
| `` O`Keil's Radiation `` | `O'Keils Radiation` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=O%60Keil%27s_Radiation&action=edit) |
| `Ring of Butcherblock` | `Ring of Butcher` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Ring_of_Butcherblock&action=edit) |
| `Ring of North Karana` | `Ring of Karana` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Ring_of_North_Karana&action=edit) |
| `Shield of Songs` | `Shield of Song` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Shield_of_Songs&action=edit) |
| `Shield of Thorns` | `Shield of Thorns (Spell)` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Shield_of_Thorns&action=edit) |
| `Solon's Bewitching Bravura` | `Solon's Bravura` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Solon%27s_Bewitching_Bravura&action=edit) |
| `Torbas' Acid Blast` | `Torbas Acid Blast` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Torbas%27_Acid_Blast&action=edit) |
| `Torbas' Poison Blast` | `Torbas Poison Blast` | typo / abbreviation | [edit](https://eqlwiki.com/index.php?title=Torbas%27_Poison_Blast&action=edit) |
| `Wrath of Al'Kabor` | `Wrath of Ap'Sagor` | **different spell** | [edit](https://eqlwiki.com/index.php?title=Wrath_of_Al%27Kabor&action=edit) |

## Evidence per page — the page's own description, which is what settles it

- **`Blast of Cold`** — declares `spellname = Shock of Frost`. Its own description: "Covers your target in a thin layer of frost, causing 18 damage." EQBuddy shows it at Wizard 1.
- **`Cantata of Soothing`** — declares `spellname = Cantana of Soothing`. Its own description: "A light cantata that regenerates the health, mana, and endurance of your entire group." EQBuddy shows it at Bard 34.
- **`Circle of Butcherblock`** — declares `spellname = Ring of South Ro`. Its own description: "Opens a mystical portal that transports your group to the Butcherblock Mountains." EQBuddy shows it at Druid 25.
- **`Circle of North Karana`** — declares `spellname = Circle of Karana`. Its own description: "Opens a mystical portal that transports your group to the [[Northern Plains of Karana]]." EQBuddy shows it at Druid 25.
- **`Evacuate: Nektulos`** — declares `spellname = Evacuate: Greater Nektulos`. Its own description: "Evacuates your group to the [[Nektulos Forest]]. While faster casting than normal portal spells, it is more likely to leave one or more of your group members behind." EQBuddy shows it at Wizard 42.
- **`Healing Water`** — declares `spellname = Greater Healing`. Its own description: "Conjures healing water from the ground, healing 425 damage to your target." EQBuddy shows it at Druid 34.
- **`Illusion: Half-Elf`** — declares `spellname = Illusion: Half Elf`. Its own description: "Cloaks you in a shimmering illusion that makes you appear to be a Half-Elf." EQBuddy shows it at Enchanter 3.
- **`Illusion: Imp`** — declares `spellname = Illusion: Air Elemental`. Its own description: "Cloaks you in a shimmering illusion that makes you appear to be an Imp. This spell also grants you levitation." EQBuddy shows it at Enchanter 45.
- **`Improved Superior Camouflage`** — declares `spellname = Improved Superior Camo`. Its own description: "Covers your body in a mystic cloak, allowing you to blend in with your surroundings." EQBuddy shows it at Druid 48.
- **`Katta's Song of Sword Dancing`** — declares `spellname = Aria of Asceticism`. Its own description: "Katta's song increases the dexterity of your group, and gives your group members a chance to lower the dexterity of their opponent. Adds {{SpellHoverLink|Blade Dance}} (melee proc)." EQBuddy shows it at Bard 39.
- **`Leech`** — declares `spellname = Leach`. Its own description: "Drains the life from your target, doing 8 damage every six seconds for 54s. 2% of the life-force taken is used to heal your wounds." EQBuddy shows it at Necromancer 9.
- **`Malaisement`** — declares `spellname = Malisement`. Its own description: "Decreases your target's resistance to cold, fire, magic, and poison." EQBuddy shows it at Magician 44, Shaman 32.
- **`Markar's Clash`** — declares `` spellname = Markar`s Clash ``. Its own description: "Strikes your target with energy that causes 200 damage and stuns targets up to level 55 for 8 seconds." EQBuddy shows it at Wizard 47.
- **`Mass Imbue Emerald`** — declares `spellname = Imbue Emerald`. Its own description: "Focuses the power of Tunare into five emeralds. Consumes five emeralds when cast." EQBuddy shows it at Cleric 29, Druid 29.
- **`Melody of Ervaj`** — declares `spellname = Song: Melody of Ervaj`. Its own description: "An archaic song that increases the attack speed of your group. This increase is cumulative with most other effects that increase attack speed." EQBuddy shows it at Bard 50.
- **`` O`Keil's Radiation ``** — declares `spellname = O'Keils Radiation`. Its own description: "Surrounds your target in radiating flame that damages any creature that strikes them." EQBuddy shows it at Wizard 2.
- **`Ring of Butcherblock`** — declares `spellname = Ring of Butcher`. Its own description: "Opens a mystical portal that transports you to the [[Butcherblock Mountains]]." EQBuddy shows it at Druid 16.
- **`Ring of North Karana`** — declares `spellname = Ring of Karana`. Its own description: "Opens a mystical portal that transports you to the [[Northern Plains of Karana]]." EQBuddy shows it at Druid 15.
- **`Shield of Songs`** — declares `spellname = Shield of Song`. Its own description: "This song wraps an aura of protection around your group that absorbs damage ." EQBuddy shows it at Bard 49.
- **`Shield of Thorns`** — declares `spellname = Shield of Thorns (Spell)`. Its own description: "Surrounds your target in a shield of thorns that cause damage to anything that strikes them for 15 min." EQBuddy shows it at Druid 47.
- **`Solon's Bewitching Bravura`** — declares `spellname = Solon's Bravura`. Its own description: "A bewitching melody that charms the target, allowing you to command it." EQBuddy shows it at Bard 39.
- **`Torbas' Acid Blast`** — declares `spellname = Torbas Acid Blast`. Its own description: "Strikes your target with a jet of poison, causing 332 damage." EQBuddy shows it at Necromancer 32.
- **`Torbas' Poison Blast`** — declares `spellname = Torbas Poison Blast`. Its own description: "Strikes your target with a jet of poison, causing 466 damage." EQBuddy shows it at Necromancer 49.
- **`Wrath of Al'Kabor`** — declares `spellname = Wrath of Ap'Sagor`. Its own description: "Creates a freezing ice storm that causes 448 damage to several creatures in the vicinity of your target. <br> Single target mana efficiency of 0.9. <br> This spell in its scroll form reads '''Wrath of Ap'Sagor'''. It scribes as '''Wrath of Al'Kabor'''." EQBuddy shows it at Wizard 48.

## Paste-ready wording, if you want to leave a talk-page note

> The `spellname` field on this page names a different spell — the page's own description
> matches this page's title. It looks like the page was started from another spell's page and
> this one field was not updated. Correcting it to match the title, so tools that read the
> infobox get the right name.

— Dranak (Claude Code)
