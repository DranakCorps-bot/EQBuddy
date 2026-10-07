# Faction routes report

Written by `faction-routes-transform.py` from the COMMITTED eqlwiki cache. It fetches
nothing. **One engine reads `FactionRoutes.json`**: DRA-728 D2's cold-start arm in `UnlockGuidance.Faction`.

## Coverage

- Routes shipped: **53** (5 from the table only, 47 from a quest page only, 1 where both agree)
- Faction amounts shipped: **177** (44 of them NEGATIVE — the costs are kept)
- Quests refused: **577**

| Why refused | Quests |
|---|---:|
| direction-only facblock (no amount) | 484 |
| several turn-in items, one faction block | 32 |
| no turn-in item in the catalog | 31 |
| several different faction blocks on one page | 27 |
| the sources disagree | 3 |

Facblock lines carrying an EDITOR's number beside a direction ("got better. (+5)"), refused as direction-only: **251**.

## The survey floor — race-unlock factions with a numeric route

**26 of 40** race-unlock factions have at least one admitted route that RAISES them (65%). The floor is half; under
it this slice STOPS and escalates to Planner (DRA-728 plan §6 S1). The list is every
`Get maximum faction with` line in the committed achievements fixtures, matched with
`FactionNames`' fold; `FactionRoutesTests` holds the same statuses through the C#.

| Race-unlock faction | Status | Quests |
|---|---|---|
| Clerics of Tunare | routed | Bracers of Erollisi Quest, Muffin for Pandos |
| Clurg | routed | Clurg's New Creation, Clurg's Revenge, Lizard Meat No 2, Lizard Meat Quest, Lizard Tails, Lizard Tails No 2, Pickled Frogloks |
| Coalition of Tradesfolk | routed | Exotic Drinks, The Frikniller Family |
| Corrupt Qeynos Guard | routed | Exotic Drinks, Honey Mead for Trumpy, Orc Scalp Collecting, The Clothspinner Sisters (evil), The Frikniller Family |
| Da Bashers | routed | A Job for Nanrum, Bone Chips Grobb |
| Dark Bargainers | routed | Book of Turmoil Quest, Bottle of Red Wine |
| Dark Ones | direction-only | Guild Summons - Dark Ones, Majik Power, More Help for Innoruuk, Urako's Big Mistake |
| Deepwater Knights | direction-only | Armor of Ro Quests, Barnacle Breastplate Quest, Cleanse the Ocean, Fisherman Convert, Guild Summons - Deepwater Cleric, Guild Summons - Deepwater Paladin, Heretic Battle, Kobold Shaman Artifacts, Odus Pearls, The Bridge, The Geologist's Purloined Tools, Zombie Flesh Quest |
| Dreadguard Inner | routed | Book of Turmoil Quest, Bottle of Red Wine |
| Dreadguard Outer | routed | Book of Turmoil Quest, Bottle of Red Wine |
| Eldritch Collective | routed | The Telescope |
| Emerald Warriors | direction-only | Beguile Plants Quest, Brain Bite (Good), Cannibalize II (Good), Captain Nealith's Brother, Crude Stein Quest, Dragon Scales Quest, Drolvarg Teeth, Druid Epic Quest, Emerald Warriors' Items, Guard of Ik Quest, Guild Summons - Emerald Warriors, Handy Shillelagh, Hogcaller's Inn, Illegible Scrolls (Felwithe), Illusion: Iksar Quest, Kilij's Plans, Muffin Quests, Orc Runner (Felwithe), Orc Runner (Kelethin), Orc Vest, Rare Coins, Red Wine to Lady Shae, Shark Meat Quest, Slave Keys, The Bread Shipment (Kelethin) |
| Freeport Militia | routed | Cutthroat Rings, Exotic Drinks, Orc Scalp Collecting, The Frikniller Family |
| Gem Choppers | routed | The Telescope |
| Grobb Merchants | routed | A Job for Nanrum |
| Guardians of the Vale | routed | Bandages for Honeybugger, Honey Jum Quest, Orc Belts |
| Guards of Qeynos | routed | Bandit Sashes, Bone Chips Qeynos, Deathfist Slashed Belts |
| Guktan Elders | none | — |
| Guktan Suppliers | none | — |
| Heretics | direction-only | Azzar's Dreadful Hat Quest, Azzar's Dreadful Hat, Part 2, Bat Fur and Beetle Legs, Bat Wings and Snake Fangs, Cat Courier, Catfish Tail, Cazic Thule Symbol Quests, Cold Iron, Experienced Courier, Guild Summons - Tabernacle of Terror, Guild Summons - The Abbatoir, Guild Summons - The Fell Blade, Inert Potion Quest, Kobold Molars (Evil), Ridossan's Spirit, Second Test of Kejaar, The Fisherman, The Lost Pet, The Summoning of Dread, The Summoning of Fright, The Summoning of Terror, The Torrid Corruptor, The Waylaid Courier, Tunic of Ridossan Quest |
| High Council of Erudin | routed | Catman Alliance, Poacher's Head Quest (Erudin) |
| Kazon Stormhammer | routed | Bone Chips (Kaladim) |
| Keepers of the Art | routed | Bat Wings |
| Kelethin Merchants | direction-only | Emerald Warriors' Items, Guild Summons - Emerald Warriors, Hogcaller's Inn, Kilij's Plans, Muffin Quests, Orc Vest, Red Wine to Lady Shae, Shark Meat Quest, Slave Keys, The Bread Shipment (Kelethin) |
| King Ak`Anon | routed | The Telescope |
| Knights of Truth | routed | Deathfist Slashed Belts |
| Merchants of Felwithe | direction-only | Emerald Warriors' Items, Guild Summons - Emerald Warriors, Hogcaller's Inn, Red Wine to Lady Shae, Shark Meat Quest, Slave Keys |
| Merchants of Halas | routed | Cindl's Polar Bear Collection, Cindl's Wristband Collection, McMannus Revenge |
| Merchants of Kaladim | direction-only | Cleaner Clockwork, Crushbone Belts, Crushbone Shoulderpads Quest, Eye of Stormhammer, Fresh Baked Muffins (Kaladim), Gretta's Baking Supplies Quest, Guild Summons - Stormguard, Knight Card Quest, Muffin Quests, Ogre Heads, Rat Pelts, Runnyeye Warbeads (Kaladim Warrior), Scarab Armor Quests, Slave Keys, The Bread Shipment (Kaladim), Trueshot Longbow Quest, Trumpy Irontoe, Tumpy Tonics |
| Merchants of Oggok | direction-only | Muffin Quests |
| Merchants of Qeynos | direction-only | Cheslin's Illusion Cards, Cleanse the Ocean, Corrupt Guards, Faren's Tacklebox, Find Lucie Elron, Fresh Baked Muffins (Qeynos), Gharin's Note (evil), Gharin's Note (good), Gnasher's Head, Guild Summons - Circle of Unseen Hands, Indaria's Doll, Jagged Pine Crook Staff, Kwint's Kwest, Moonstones Quest, Muffin Quests, Nesiff's Statue, Peacekeeper Staff Quest, Plaguebringer Proof, Qeynos Badge Quests, Quench Lasen's Thirst, Rain Caller Quest, Rogue Epic Quest, Rohand's Brandy, Scout Blade, Sneed's Rat Infestation, Taxes, Tayla Ironforge, The Bread Shipment (East Freeport), The Bread Shipment (N Karana), The Bread Shipment (Qeynos), The Bread Shipment (W Karana Danin), The Bread Shipment (W Karana Rislarn), The Clothspinner Sisters (good), The Crate (evil), The Crate (good), The Lottery Ticket, The Nitrates and the Assassin, Tonics for Groflah, Trumpy Irontoe, Trumpy's Head, Vasty Deep Water, Weapons Delivery, Wenbie's Muffins |
| Merchants of Rivervale | routed | Bandages for Honeybugger, Honey Jum Quest, Nillipuss the Brownie, Orc Belts |
| New Sebilisian Expedition | routed | Supplies for the New Sebilisian Expedition |
| Oggok Guards | routed | Clurg's New Creation, Clurg's Revenge, Pickled Frogloks |
| Priests of Mischief | direction-only | Cleric Supplies, The Acolyte |
| Protectors of Gukta | none | — |
| Rogues of the White Rose | routed | Cindl's Polar Bear Collection, Cindl's Wristband Collection, Mammoth Calf Hides, McMannus Revenge |
| Soldiers of Tunare | routed | Bracers of Erollisi Quest, Muffin for Pandos |
| Storm Guard | direction-only | Aviak Chicks, Beguile Plants Quest, Brain Bite (Good), Cannibalize II (Good), Captain Nealith's Brother, Cleaner Clockwork, Crushbone Belts, Crushbone Shoulderpads Quest, Dragon Scales Quest, Drolvarg Teeth, Eye of Stormhammer, Fresh Baked Muffins (Kaladim), Gretta's Baking Supplies Quest, Guard of Ik Quest, Guild Summons - Stormguard, Handy Shillelagh, Illegible Scrolls (Felwithe), Illusion: Iksar Quest, Knight Card Quest, Muffin Quests, Ogre Heads, Orc Runner (Felwithe), Orc Runner (Kelethin), Parrying Pick Quest, Rare Coins, Rat Pelts, Runnyeye Warbeads (Kaladim Warrior), Scarab Armor Quests, Slave Keys, The Bread Shipment (Kaladim), The Mudtoes, Trueshot Longbow Quest, Trumpy Irontoe, Tumpy Tonics |
| Wolves of the North | routed | Cindl's Polar Bear Collection, Cindl's Wristband Collection, McMannus Revenge |

## Distinct-count telltale (trap 73)

**13 distinct amounts across 177 shipped faction amounts.**
Faction amounts are NOT a per-row fact the way a level band is — +5 is the game's
ordinary hand-in step, so most rows agreeing on it is the expected shape. The guard
is that more than one value appears and that the named fixtures (Bottle of Red Wine,
Bandit Sashes' -20, Clurg's Revenge's -15) read back exactly (`FactionRoutesTests`).

| Amount | Times |
|---:|---:|
| -20 | 2 |
| -15 | 1 |
| -5 | 5 |
| -3 | 1 |
| -2 | 3 |
| -1 | 32 |
| +1 | 2 |
| +5 | 95 |
| +7 | 3 |
| +8 | 1 |
| +10 | 15 |
| +15 | 8 |
| +20 | 9 |

## Conflicts — refused, both readings shown, never averaged

| Quest | Disagreement |
|---|---|
| Gnoll Bounty | count per turn-in [3] vs [1] |
| Red Wine to Lady Shae | count per turn-in [4] vs [1] |
| Shondo and the Tonic | Merchants of AkAnon +5 vs +4 |

## Spelling differences where the sources otherwise agree

The catalog's spelling ships (it is what `QuestMatcher` and the bags use).

| Quest | Table | Catalog (shipped) |
|---|---|---|
| Bandages for Honeybugger | Bandage | Bandages |

## Every refused quest, by name

| Quest | From | Why |
|---|---|---|
| 10th Coldain Ring Quest | page | direction-only facblock (no amount) |
| Acumen Mask Quest | page | direction-only facblock (no amount) |
| Aegis of Life Quest | page | direction-only facblock (no amount) |
| Aenia and Behroe | page | direction-only facblock (no amount) |
| Aid the Dar Brood | page | direction-only facblock (no amount) |
| Air Tight Box Quest | page | no turn-in item in the catalog |
| Armor of Ro Quests | page | direction-only facblock (no amount) |
| Assist the Great Xelha | page | several different faction blocks on one page |
| Aviak Chicks | page | direction-only facblock (no amount) |
| Aviak Talons | page | direction-only facblock (no amount) |
| Azraxs' Legacy | page | direction-only facblock (no amount) |
| Azzar's Dreadful Hat Quest | page | direction-only facblock (no amount) |
| Azzar's Dreadful Hat, Part 2 | page | direction-only facblock (no amount) |
| Bandit Sisters | page | direction-only facblock (no amount) |
| Bandit Spectacles | page | no turn-in item in the catalog |
| Bard Kael Armor Quests | page | direction-only facblock (no amount) |
| Bard Reports | page | direction-only facblock (no amount) |
| Bard Skyshrine Armor Quests | page | direction-only facblock (no amount) |
| Bard Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Barnacle Breastplate Quest | page | direction-only facblock (no amount) |
| Basilisk Tongues | page | direction-only facblock (no amount) |
| Bat Fur and Beetle Legs | page | direction-only facblock (no amount) |
| Bat Fur Quest | page | direction-only facblock (no amount) |
| Bat Wings and Snake Fangs | page | direction-only facblock (no amount) |
| Bear Hide Armor | page | several turn-in items, one faction block |
| Beguile Plants Quest | page | direction-only facblock (no amount) |
| Behead the Freeport Militia | page | direction-only facblock (no amount) |
| Bertoxxulous Symbol Quests | page | several different faction blocks on one page |
| Black Wolf Skin Quest | page | direction-only facblock (no amount) |
| Blackburrow Stout Delivery | page | direction-only facblock (no amount) |
| Blackburrow Stout Quest | page | direction-only facblock (no amount) |
| Blackburrow Stout Shipment | page | direction-only facblock (no amount) |
| Bladed Weapons | page | direction-only facblock (no amount) |
| Blank Scrolls | page | no turn-in item in the catalog |
| Blessed Oil | page | direction-only facblock (no amount) |
| Blessed Oil Quest | page | direction-only facblock (no amount) |
| Blizzent's Fang Quest | page | direction-only facblock (no amount) |
| Blood Ink | page | direction-only facblock (no amount) |
| Blurred Map Quest | page | direction-only facblock (no amount) |
| Blyle Bundin's Head | page | direction-only facblock (no amount) |
| Bone Chips Felwithe | page | direction-only facblock (no amount) |
| Bone Chips Field of Bone | page | direction-only facblock (no amount) |
| Bone Granite Powder Quest | page | direction-only facblock (no amount) |
| Bonethunder Staff Quest | page | direction-only facblock (no amount) |
| Bottle of Red Wine | page | direction-only facblock (no amount) |
| Brain Bite (Evil) | page | direction-only facblock (no amount) |
| Brain Bite (Good) | page | direction-only facblock (no amount) |
| Brell Serilis Symbol Quests | page | direction-only facblock (no amount) |
| Broken Lute | page | direction-only facblock (no amount) |
| Broom Of Trilon Quest | page | direction-only facblock (no amount) |
| Brother Trintle (Quest) | page | direction-only facblock (no amount) |
| Bug Collection | page | no turn-in item in the catalog |
| Bugglegupp (Quest) | page | direction-only facblock (no amount) |
| Bulthar Trunks | page | direction-only facblock (no amount) |
| Bvellos' Bounty | page | direction-only facblock (no amount) |
| Call of Flame Quest | page | direction-only facblock (no amount) |
| Cannibalize II (Good) | page | direction-only facblock (no amount) |
| Captain Nealith's Brother | page | direction-only facblock (no amount) |
| Cat Courier | page | direction-only facblock (no amount) |
| Catfish Croak Sandwich Quest | page | direction-only facblock (no amount) |
| Catfish Tail | page | several different faction blocks on one page |
| Cazic Thule Symbol Quests | page | direction-only facblock (no amount) |
| Chalice of Conquest Quest | page | several different faction blocks on one page |
| Cheslin's Illusion Cards | page | direction-only facblock (no amount) |
| Cleaner Clockwork | page | direction-only facblock (no amount) |
| Cleanse the Ocean | page | direction-only facblock (no amount) |
| Cleric Kael Armor Quests | page | direction-only facblock (no amount) |
| Cleric Skyshrine Armor Quests | page | direction-only facblock (no amount) |
| Cleric Supplies | page | no turn-in item in the catalog |
| Cleric Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Cold Iron | page | direction-only facblock (no amount) |
| Coldain Books of Tactics | page | direction-only facblock (no amount) |
| Coldain Prayer Shawl Quests | page | direction-only facblock (no amount) |
| Coldain Ring Quests | page | direction-only facblock (no amount) |
| Coldain Skulls | page | direction-only facblock (no amount) |
| Collier's Weapon Treatment | page | several turn-in items, one faction block |
| Corrupt Guards | page | direction-only facblock (no amount) |
| Corrupt Guards (Cleric version) | page | direction-only facblock (no amount) |
| Corrupt Guards (Paladin Version) | page | direction-only facblock (no amount) |
| Crest of the Drixie Quest | page | direction-only facblock (no amount) |
| Crest of the Faerie Dragons Quest | page | direction-only facblock (no amount) |
| Crest of the Fauns Quest | page | direction-only facblock (no amount) |
| Crest of the Sifaye Quest | page | direction-only facblock (no amount) |
| Crest of the Wood Nymphs Quest | page | several turn-in items, one faction block |
| Crude Stein Quest | page | direction-only facblock (no amount) |
| Crusader's Tests | page | direction-only facblock (no amount) |
| Crush the Undead | page | direction-only facblock (no amount) |
| Crushbone Belts | page | direction-only facblock (no amount) |
| Crushbone Shoulderpads Quest | page | direction-only facblock (no amount) |
| Crystal Caverns' Ancient Artifacts | page | direction-only facblock (no amount) |
| Cure for Lempeck Hargrin | page | direction-only facblock (no amount) |
| Cures | page | direction-only facblock (no amount) |
| Curscale Armor Quest | page | direction-only facblock (no amount) |
| Curscale Shield | page | direction-only facblock (no amount) |
| Cursed Wafers Quest | page | direction-only facblock (no amount) |
| Dain's Head | page | direction-only facblock (no amount) |
| Darkwood Staff Quest | page | direction-only facblock (no amount) |
| Demise of Blizzent | page | direction-only facblock (no amount) |
| Deputy Tagil's Debt | page | direction-only facblock (no amount) |
| Di'Zok Signet of Service Quest | page | direction-only facblock (no amount) |
| Dire Wolf-Hide Cloak Quest | page | direction-only facblock (no amount) |
| Dragon Heads | page | direction-only facblock (no amount) |
| Dragon Scales Quest | page | direction-only facblock (no amount) |
| Drolvarg Teeth | page | direction-only facblock (no amount) |
| Drosco the Zombie (evil) | page | direction-only facblock (no amount) |
| Drosco the Zombie (good) | page | direction-only facblock (no amount) |
| Druid Epic Quest | page | direction-only facblock (no amount) |
| Druid Kael Armor Quests | page | direction-only facblock (no amount) |
| Druid Skyshrine Armor Quests | page | direction-only facblock (no amount) |
| Druid Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Duster Models | page | direction-only facblock (no amount) |
| Earthen Boots Quest | page | direction-only facblock (no amount) |
| Emerald Dragonscale Quest | page | direction-only facblock (no amount) |
| Emerald Warriors' Items | page | direction-only facblock (no amount) |
| Emil's Report | page | direction-only facblock (no amount) |
| Enchanter Kael Armor Quests | page | direction-only facblock (no amount) |
| Enchanter Skyshrine Armor Quests | page | direction-only facblock (no amount) |
| Enchanter Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Errand for Tonmerk | page | direction-only facblock (no amount) |
| Errand for Wolten | page | direction-only facblock (no amount) |
| Erud's Tonic Quest | page | no turn-in item in the catalog |
| Erudite Prisoners | page | direction-only facblock (no amount) |
| Essence Lens Quest | page | direction-only facblock (no amount) |
| Evil Research | page | direction-only facblock (no amount) |
| Exotic Drinks | page | several different faction blocks on one page |
| Experienced Courier | page | direction-only facblock (no amount) |
| Explorer Survival Knives | page | direction-only facblock (no amount) |
| Eye of Stormhammer | page | direction-only facblock (no amount) |
| Fabian's Strings | page | several different faction blocks on one page |
| Fang Tooth (quest) | page | no turn-in item in the catalog |
| Faren's Tacklebox | page | direction-only facblock (no amount) |
| Feeding Dooga | page | direction-only facblock (no amount) |
| Fern Flower Collection | page | several turn-in items, one faction block |
| Feskr's Supplies | page | several turn-in items, one faction block |
| Field Supplies Quest | page | direction-only facblock (no amount) |
| Find Lucie Elron | page | direction-only facblock (no amount) |
| Fire Goblin Runner | page | direction-only facblock (no amount) |
| Fish Dinner | page | direction-only facblock (no amount) |
| Fisherman Convert | page | direction-only facblock (no amount) |
| Fleshy Orbs | page | direction-only facblock (no amount) |
| Fresh Baked Muffins (Kaladim) | page | direction-only facblock (no amount) |
| Fresh Baked Muffins (Qeynos) | page | direction-only facblock (no amount) |
| Friend of the Kin | page | direction-only facblock (no amount) |
| Friend of the Tunarean Court | page | direction-only facblock (no amount) |
| Froglok Skin Mask Quest | page | direction-only facblock (no amount) |
| Froglok Tad Tongues | page | direction-only facblock (no amount) |
| Frostbite's Fish | page | several different faction blocks on one page |
| Fusibility Research | page | direction-only facblock (no amount) |
| Gathering Grain | page | direction-only facblock (no amount) |
| Gearheart (Quest) | page | direction-only facblock (no amount) |
| Geozite Tool Quest | page | direction-only facblock (no amount) |
| Gharin's Note (evil) | page | several different faction blocks on one page |
| Gharin's Note (good) | page | several different faction blocks on one page |
| Giant Helmets | page | direction-only facblock (no amount) |
| Gleed's Bow | page | direction-only facblock (no amount) |
| Gnasher's Head | page | direction-only facblock (no amount) |
| Gnoll Bounty | both | the table and the quest page disagree: count per turn-in [3] vs [1] |
| Gnoll Scalp Collecting | page | direction-only facblock (no amount) |
| Gnomish Toy | page | direction-only facblock (no amount) |
| Goblin Battlemasters | page | direction-only facblock (no amount) |
| Goblin Caster Necklace | page | several turn-in items, one faction block |
| Goblin Raiders | page | direction-only facblock (no amount) |
| Going Postal | page | direction-only facblock (no amount) |
| Going Postal (Felwithe to Kelethin) | page | direction-only facblock (no amount) |
| Going Postal (Kaladim to Kelethin) | page | direction-only facblock (no amount) |
| Granite Pebbles | page | direction-only facblock (no amount) |
| Greenblood Tunics | page | direction-only facblock (no amount) |
| Gretta's Baking Supplies Quest | page | direction-only facblock (no amount) |
| Grim's Tiger Revenge | page | direction-only facblock (no amount) |
| Groflah Steadirt's Death | page | direction-only facblock (no amount) |
| Guard of Ik Quest | page | direction-only facblock (no amount) |
| Guard Shilster's Stout | page | direction-only facblock (no amount) |
| Guild Summons - Abbey of Deep Musing Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Abbey of Deep Musing Rogue | page | direction-only facblock (no amount) |
| Guild Summons - Cathedral of Fortitude | page | direction-only facblock (no amount) |
| Guild Summons - Cauldron of Hate | page | direction-only facblock (no amount) |
| Guild Summons - Church of Underfoot | page | direction-only facblock (no amount) |
| Guild Summons - Circle of Unseen Hands | page | direction-only facblock (no amount) |
| Guild Summons - Coalition of Tradefolk Underground | page | direction-only facblock (no amount) |
| Guild Summons - Craft Keepers | page | direction-only facblock (no amount) |
| Guild Summons - Crimson Hands | page | direction-only facblock (no amount) |
| Guild Summons - Da Bashers | page | direction-only facblock (no amount) |
| Guild Summons - Dark Ones | page | direction-only facblock (no amount) |
| Guild Summons - Dark Reflection Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Dark Reflection Enchanter | page | direction-only facblock (no amount) |
| Guild Summons - Dark Reflection Magician | page | direction-only facblock (no amount) |
| Guild Summons - Dark Reflection Necromancer | page | direction-only facblock (no amount) |
| Guild Summons - Dark Reflection Rogue | page | direction-only facblock (no amount) |
| Guild Summons - Dark Reflection Warrior | page | direction-only facblock (no amount) |
| Guild Summons - Dark Reflection Wizard | page | direction-only facblock (no amount) |
| Guild Summons - Deepwater Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Deepwater Paladin | page | direction-only facblock (no amount) |
| Guild Summons - Dismal Rage Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Dismal Rage Enchanter | page | direction-only facblock (no amount) |
| Guild Summons - Dismal Rage Magician | page | direction-only facblock (no amount) |
| Guild Summons - Dismal Rage Necromancer | page | direction-only facblock (no amount) |
| Guild Summons - Dismal Rage Shadowknight | page | direction-only facblock (no amount) |
| Guild Summons - Dismal Rage Warrior | page | direction-only facblock (no amount) |
| Guild Summons - Dismal Rage Wizard | page | direction-only facblock (no amount) |
| Guild Summons - Emerald Warriors | page | direction-only facblock (no amount) |
| Guild Summons - Faydark's Champions | page | direction-only facblock (no amount) |
| Guild Summons - Fortress Craknek | page | direction-only facblock (no amount) |
| Guild Summons - Gate Callers | page | direction-only facblock (no amount) |
| Guild Summons - Gemchopper Hall | page | direction-only facblock (no amount) |
| Guild Summons - Greenblood Rock | page | direction-only facblock (no amount) |
| Guild Summons - Hall of Sorcery Enchanter | page | direction-only facblock (no amount) |
| Guild Summons - Hall of Sorcery Magician | page | direction-only facblock (no amount) |
| Guild Summons - Hall of Sorcery Wizard | page | direction-only facblock (no amount) |
| Guild Summons - Hall of Steel | page | direction-only facblock (no amount) |
| Guild Summons - Hall of the Ebon Mask | page | direction-only facblock (no amount) |
| Guild Summons - Hall of Truth Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Hall of Truth Paladin | page | direction-only facblock (no amount) |
| Guild Summons - Jaggedpine Treefolk | page | direction-only facblock (no amount) |
| Guild Summons - Libary Mechanimagica Enchanter | page | direction-only facblock (no amount) |
| Guild Summons - Libary Mechanimagica Magician | page | direction-only facblock (no amount) |
| Guild Summons - Libary Mechanimagica Wizard | page | direction-only facblock (no amount) |
| Guild Summons - Marsheart's Chords | page | direction-only facblock (no amount) |
| Guild Summons - Miners Guild 628 | page | direction-only facblock (no amount) |
| Guild Summons - Murdunk's Palace | page | direction-only facblock (no amount) |
| Guild Summons - Night Keep | page | direction-only facblock (no amount) |
| Guild Summons - Order of the Silent Fist | page | direction-only facblock (no amount) |
| Guild Summons - Paladins of the Underfoot | page | direction-only facblock (no amount) |
| Guild Summons - Priests of Innoruuk | page | direction-only facblock (no amount) |
| Guild Summons - Protectors of the Pine | page | direction-only facblock (no amount) |
| Guild Summons - Rogues of the White Rose | page | direction-only facblock (no amount) |
| Guild Summons - Scouts of Tunare | page | direction-only facblock (no amount) |
| Guild Summons - Shamen of Justice | page | direction-only facblock (no amount) |
| Guild Summons - Shrine of Bertoxxulous Enchanter | page | direction-only facblock (no amount) |
| Guild Summons - Shrine of Bertoxxulous Magician | page | direction-only facblock (no amount) |
| Guild Summons - Shrine of Bertoxxulous Necromancer | page | direction-only facblock (no amount) |
| Guild Summons - Shrine of Bertoxxulous Shadowknight | page | direction-only facblock (no amount) |
| Guild Summons - Shrine of Bertoxxulous Warrior | page | direction-only facblock (no amount) |
| Guild Summons - Shrine of Bertoxxulous Wizard | page | direction-only facblock (no amount) |
| Guild Summons - Soldiers of Tunare | page | direction-only facblock (no amount) |
| Guild Summons - Songweavers | page | direction-only facblock (no amount) |
| Guild Summons - Stormguard | page | direction-only facblock (no amount) |
| Guild Summons - Tabernacle of Terror | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Bertoxxulous Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Divine Light Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Divine Light Paladin | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Life Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Life Paladin | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Marr Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Marr Paladin | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Thunder Cleric | page | direction-only facblock (no amount) |
| Guild Summons - Temple of Thunder Paladin | page | direction-only facblock (no amount) |
| Guild Summons - The Abbatoir | page | direction-only facblock (no amount) |
| Guild Summons - The Amethyst Palace | page | direction-only facblock (no amount) |
| Guild Summons - The Dead Necromancer | page | direction-only facblock (no amount) |
| Guild Summons - The Dead Shadowknight | page | direction-only facblock (no amount) |
| Guild Summons - The Fell Blade | page | direction-only facblock (no amount) |
| Guild Summons - The Spurned Enchanter | page | direction-only facblock (no amount) |
| Guild Summons - The Spurned Magician | page | direction-only facblock (no amount) |
| Guild Summons - The Spurned Wizard | page | direction-only facblock (no amount) |
| Guild Summons - The Wind Spirit's Song | page | direction-only facblock (no amount) |
| Guild Summons - Wolves of the North | page | direction-only facblock (no amount) |
| Halfling Raider Helms | page | direction-only facblock (no amount) |
| Handy Shillelagh | page | direction-only facblock (no amount) |
| Harvester Quest | page | direction-only facblock (no amount) |
| Head of Granin O'Gill | page | direction-only facblock (no amount) |
| Health Potion | page | several turn-in items, one faction block |
| HEHE Meat Quest | page | direction-only facblock (no amount) |
| Helms of Giant Warriors | page | direction-only facblock (no amount) |
| Help Hergor Get Fatter | page | direction-only facblock (no amount) |
| Heretic Battle | page | direction-only facblock (no amount) |
| Heretic Toy | page | no turn-in item in the catalog |
| Heretic's Toy | page | no turn-in item in the catalog |
| Hero Bracers Quest | page | several different faction blocks on one page |
| High Guard Battlestaff | page | direction-only facblock (no amount) |
| Hogcaller's Inn | page | several turn-in items, one faction block |
| Hollow Skull Quest | page | direction-only facblock (no amount) |
| Holy Armor Scroll | page | direction-only facblock (no amount) |
| Hopeless Love, Part 1 | page | direction-only facblock (no amount) |
| Hsagra's Wrath Quest | page | direction-only facblock (no amount) |
| Hukulk's Love | page | direction-only facblock (no amount) |
| Hungry Deputy | page | direction-only facblock (no amount) |
| Hurrieta's Tunic Quest | page | direction-only facblock (no amount) |
| Ice Goblin Beads | page | direction-only facblock (no amount) |
| Ice Goblin Necklaces | page | several turn-in items, one faction block |
| Iksar Prisoner Quest | page | direction-only facblock (no amount) |
| Ilanic's Scroll | page | direction-only facblock (no amount) |
| Illegible Cantrip Quest | page | direction-only facblock (no amount) |
| Illegible Scrolls (Felwithe) | page | direction-only facblock (no amount) |
| Illusion: Iksar Quest | page | direction-only facblock (no amount) |
| Illweed Parchment Quest | page | no turn-in item in the catalog |
| Incandescent Armor Quests | page | several turn-in items, one faction block |
| Indaria's Doll | page | direction-only facblock (no amount) |
| Inert Potion Quest | page | direction-only facblock (no amount) |
| Innoruuk Recommendation | page | several different faction blocks on one page |
| Innoruuk Symbol Quests | page | direction-only facblock (no amount) |
| Ivan McMannus' Remains | page | several turn-in items, one faction block |
| Jagged Pine Crook Staff | page | direction-only facblock (no amount) |
| Jillin's Stew | page | direction-only facblock (no amount) |
| Karana Clovers | page | several different faction blocks on one page |
| Karana's Blessing | page | direction-only facblock (no amount) |
| Kelorek's Scales | page | direction-only facblock (no amount) |
| Key to Jaled Dar's Lair (Neb) | page | direction-only facblock (no amount) |
| Key to Jaled Dar's Lair (Zlandicar) | page | direction-only facblock (no amount) |
| Kilij's Plans | page | direction-only facblock (no amount) |
| Knight Card Quest | page | direction-only facblock (no amount) |
| Kobold Molars (Evil) | page | direction-only facblock (no amount) |
| Kobold Molars (Good) | page | direction-only facblock (no amount) |
| Kobold Shaman Artifacts | page | direction-only facblock (no amount) |
| Kobold Shaman Paws | page | direction-only facblock (no amount) |
| Kwint's Kwest | page | direction-only facblock (no amount) |
| Langseax Quest | page | several turn-in items, one faction block |
| Leatherfoot Raiders | page | direction-only facblock (no amount) |
| Left Goblin Ears | page | direction-only facblock (no amount) |
| Legion Lager Quest | page | direction-only facblock (no amount) |
| Lenka's Pouch | page | direction-only facblock (no amount) |
| Letter to Master Whoopal | page | direction-only facblock (no amount) |
| Leuz's Task | page | direction-only facblock (no amount) |
| Library Book | page | direction-only facblock (no amount) |
| Lion Meat Shipment Quest | page | no turn-in item in the catalog |
| Living Dragons | page | direction-only facblock (no amount) |
| Living Granite | page | direction-only facblock (no amount) |
| Lodizal Shell Shield Quest | page | direction-only facblock (no amount) |
| Lydl Mastat | page | direction-only facblock (no amount) |
| Lynuga's Gem Collection | page | no turn-in item in the catalog |
| Magic Elixir for the Warriors | page | several turn-in items, one faction block |
| Magician Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Majik Power | page | direction-only facblock (no amount) |
| Mammoth Tusks Quest | page | direction-only facblock (no amount) |
| Marauder Armor | page | direction-only facblock (no amount) |
| Marda's Secret Mission | page | direction-only facblock (no amount) |
| Marr Minnows for Palon | page | direction-only facblock (no amount) |
| Mercenary Assignments Quest | page | direction-only facblock (no amount) |
| Merona's Brother | page | direction-only facblock (no amount) |
| Message Intercept | page | direction-only facblock (no amount) |
| Messages For Neriak | page | no turn-in item in the catalog |
| Metal Bits for the New Sebilisian Expedition | page | no turn-in item in the catalog |
| Miner's Cap | page | direction-only facblock (no amount) |
| Miners Pick | page | direction-only facblock (no amount) |
| Minotaur Horns | page | direction-only facblock (no amount) |
| Miranda's Chocolate | page | no turn-in item in the catalog |
| Miranda's Dice | page | no turn-in item in the catalog |
| Monk Headband Quests | page | direction-only facblock (no amount) |
| Monk Kael Armor Quests | page | direction-only facblock (no amount) |
| Monk Sash Quests | page | direction-only facblock (no amount) |
| Monk Shackle Quests | page | direction-only facblock (no amount) |
| Monk Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Moonstones Quest | page | several different faction blocks on one page |
| More Help for Innoruuk | page | direction-only facblock (no amount) |
| Moss Snakes | page | several turn-in items, one faction block |
| Muffin Quests | page | several different faction blocks on one page |
| Necro Spells | page | direction-only facblock (no amount) |
| Necromancer Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Necromancer Words - X`Ta Tempi | page | direction-only facblock (no amount) |
| Nesiff's Statue | page | several different faction blocks on one page |
| Newbie Quest: Halfling Druid | page | direction-only facblock (no amount) |
| Newbie Quest: Troll Warrior | page | direction-only facblock (no amount) |
| Noble Hunters | page | direction-only facblock (no amount) |
| Note for Janam | page | several turn-in items, one faction block |
| Note for Konem | page | direction-only facblock (no amount) |
| Note for Rebby | page | direction-only facblock (no amount) |
| Note to Neclo Quest | page | no turn-in item in the catalog |
| Odus Pearls | page | direction-only facblock (no amount) |
| Ogre Heads | page | direction-only facblock (no amount) |
| Orc Hatchets | page | direction-only facblock (no amount) |
| Orc Pawn Picks | page | direction-only facblock (no amount) |
| Orc Picks | page | no turn-in item in the catalog |
| Orc Runner (Felwithe) | page | direction-only facblock (no amount) |
| Orc Runner (Kelethin) | page | direction-only facblock (no amount) |
| Orc Vest | page | several turn-in items, one faction block |
| Order of Thunder (from Drosco) | page | direction-only facblock (no amount) |
| Ortallius' Cutthroat Rings | page | direction-only facblock (no amount) |
| Oven Mittens | page | direction-only facblock (no amount) |
| Package from Lomarc | page | direction-only facblock (no amount) |
| Paladin Hunting | page | direction-only facblock (no amount) |
| Paladin Kael Armor Quests | page | direction-only facblock (no amount) |
| Paladin Message | page | direction-only facblock (no amount) |
| Paladin Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Parrying Pick Quest | page | direction-only facblock (no amount) |
| Peacekeeper Staff Quest | page | several turn-in items, one faction block |
| Phosphorous Powder for Zok Zribb | page | direction-only facblock (no amount) |
| Piranha Hunting | page | direction-only facblock (no amount) |
| Pirate Earrings | page | no turn-in item in the catalog |
| Pixie Dust (Kelethin Ranger) | page | direction-only facblock (no amount) |
| Pixie Dust (Kelethin Rogue) | page | direction-only facblock (no amount) |
| Plaguebringer Proof | page | direction-only facblock (no amount) |
| Plane of Mischief Faction Quest | page | direction-only facblock (no amount) |
| Poacher Leader | page | direction-only facblock (no amount) |
| Preserved Meat Delivery | page | no turn-in item in the catalog |
| Princess Lenya (Quest) | page | direction-only facblock (no amount) |
| Protect the Shipyard | page | direction-only facblock (no amount) |
| Putrid Skeletons | page | several turn-in items, one faction block |
| Qeynos Badge Quests | page | direction-only facblock (no amount) |
| Quellious Symbol Quests | page | direction-only facblock (no amount) |
| Quench Lasen's Thirst | page | direction-only facblock (no amount) |
| Rabid Grizzlies | page | direction-only facblock (no amount) |
| Rabid Wolves | page | direction-only facblock (no amount) |
| Rain Caller Quest | page | several different faction blocks on one page |
| Rallos Zek Symbol Quests | page | direction-only facblock (no amount) |
| Ranger Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Ranjor's Test | page | direction-only facblock (no amount) |
| Rare Coins | page | direction-only facblock (no amount) |
| Rat Ear Pie Quest | page | several turn-in items, one faction block |
| Rat Fur Cap Quest | page | direction-only facblock (no amount) |
| Rat Patrol | page | direction-only facblock (no amount) |
| Rat Pelt Cape Quest | page | direction-only facblock (no amount) |
| Rat Pelts | page | direction-only facblock (no amount) |
| Rat Shaped Rings | page | direction-only facblock (no amount) |
| Rat Teeth | page | direction-only facblock (no amount) |
| Rat's Foot Necklace Quest | page | direction-only facblock (no amount) |
| Rathmana's Traveling Offer | page | direction-only facblock (no amount) |
| Ratskin Gloves Quest | page | direction-only facblock (no amount) |
| Red V Clockwork | page | direction-only facblock (no amount) |
| Red Wine to Lady Shae | both | the table and the quest page disagree: count per turn-in [4] vs [1] |
| Reebo's Carrots | page | direction-only facblock (no amount) |
| Reinforcements for The Tunarean Regiment | page | direction-only facblock (no amount) |
| Renew Bones Quest | page | direction-only facblock (no amount) |
| Reserve Militia | page | no turn-in item in the catalog |
| Respecialization | page | direction-only facblock (no amount) |
| Ridossan's Spirit | page | direction-only facblock (no amount) |
| Rod of Insidious Glamour Quest | page | several turn-in items, one faction block |
| Rogue Epic Quest | page | direction-only facblock (no amount) |
| Rogue Errands | page | several different faction blocks on one page |
| Rogue Redemption | page | direction-only facblock (no amount) |
| Rogue Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Rohand's Brandy | page | direction-only facblock (no amount) |
| Runescale Cloak Quest | page | direction-only facblock (no amount) |
| Rungupp (Quest) | page | direction-only facblock (no amount) |
| Runnyeye Warbeads (Kaladim Rogue) | page | direction-only facblock (no amount) |
| Runnyeye Warbeads (Kaladim Warrior) | page | direction-only facblock (no amount) |
| Runnyeye Warbeads (Rivervale) | page | direction-only facblock (no amount) |
| Rusted Black Boxes | page | direction-only facblock (no amount) |
| Sarnak Hatchling Brains | page | direction-only facblock (no amount) |
| Saucy Salted Seadragon Steak | page | direction-only facblock (no amount) |
| Scarab Armor Quests | page | direction-only facblock (no amount) |
| Scorpion Pincers | page | direction-only facblock (no amount) |
| Scout Blade | page | direction-only facblock (no amount) |
| Scouts Cape Quest | page | direction-only facblock (no amount) |
| Scrap Metal Quest | page | direction-only facblock (no amount) |
| Second Test of Kejaar | page | no turn-in item in the catalog |
| Sentry Xyrin Quest | page | no turn-in item in the catalog |
| Series C Black Boxes | page | several different faction blocks on one page |
| Shadowknight Kael Armor Quests | page | direction-only facblock (no amount) |
| Shadowknight Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Shakey's Stuffing | page | direction-only facblock (no amount) |
| Shaman Epic Quest | page | direction-only facblock (no amount) |
| Shaman Skull Quests | page | direction-only facblock (no amount) |
| Shaman Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Shaman's Velium Sleeves | page | direction-only facblock (no amount) |
| Shark Meat Quest | page | direction-only facblock (no amount) |
| Shondo and the Tonic | both | the table and the quest page disagree: Merchants of AkAnon +5 vs +4 |
| Shovel Of Ponz Quest | page | direction-only facblock (no amount) |
| Sir Lindeal's Testimony | page | direction-only facblock (no amount) |
| Skeleton Killing | page | several turn-in items, one faction block |
| Skunk Hunting | page | direction-only facblock (no amount) |
| Slaggak's Bounty | page | direction-only facblock (no amount) |
| Slave Keys | page | direction-only facblock (no amount) |
| Snake Fang Necklace Quest | page | direction-only facblock (no amount) |
| Sneed's Rat Infestation | page | direction-only facblock (no amount) |
| Soil of Underfoot | page | direction-only facblock (no amount) |
| Soil of Underfoot Quest | page | direction-only facblock (no amount) |
| Solusek's Flower | page | no turn-in item in the catalog |
| Sparring Armor | page | direction-only facblock (no amount) |
| Spider Legs Quest | page | direction-only facblock (no amount) |
| Spirit Aid | page | several turn-in items, one faction block |
| Spirit Powder | page | several turn-in items, one faction block |
| Steel Warrior Initiation | page | direction-only facblock (no amount) |
| Stein Of Ulissa Quest | page | direction-only facblock (no amount) |
| Storm Giant Toes to Sentry Kcor | page | direction-only facblock (no amount) |
| Strategies of the Ancient Dragons | page | direction-only facblock (no amount) |
| Strife to the Coldain | page | direction-only facblock (no amount) |
| Supplies for the New Sebilisian Expedition | page | no turn-in item in the catalog |
| Talym Shoontar's Head | page | direction-only facblock (no amount) |
| Taxes | page | direction-only facblock (no amount) |
| Tayla Ironforge | page | direction-only facblock (no amount) |
| Telescope Lenses | page | direction-only facblock (no amount) |
| Temple Blankets Quest | page | no turn-in item in the catalog |
| Tergon's Spellbook Quest | page | direction-only facblock (no amount) |
| Test of the Fire Storm | page | direction-only facblock (no amount) |
| The Acolyte | page | no turn-in item in the catalog |
| The Bind | page | direction-only facblock (no amount) |
| The Bloody Shank | page | direction-only facblock (no amount) |
| The Bones of Darak Lightforge | page | several turn-in items, one faction block |
| The Bread Shipment (East Freeport) | page | direction-only facblock (no amount) |
| The Bread Shipment (Erudin) | page | direction-only facblock (no amount) |
| The Bread Shipment (Feerrott) | page | direction-only facblock (no amount) |
| The Bread Shipment (Halas) | page | direction-only facblock (no amount) |
| The Bread Shipment (Kaladim) | page | direction-only facblock (no amount) |
| The Bread Shipment (Kelethin) | page | direction-only facblock (no amount) |
| The Bread Shipment (N Karana) | page | direction-only facblock (no amount) |
| The Bread Shipment (Neriak) | page | direction-only facblock (no amount) |
| The Bread Shipment (Qeynos) | page | direction-only facblock (no amount) |
| The Bread Shipment (W Karana Danin) | page | direction-only facblock (no amount) |
| The Bread Shipment (W Karana Rislarn) | page | direction-only facblock (no amount) |
| The Bridge | page | several turn-in items, one faction block |
| The Crate (evil) | page | several different faction blocks on one page |
| The Crate (good) | page | direction-only facblock (no amount) |
| The Donations | page | direction-only facblock (no amount) |
| The Emissary | page | direction-only facblock (no amount) |
| The Falchion | page | several turn-in items, one faction block |
| The Fiery Avenger | page | several turn-in items, one faction block |
| The Fisherman | page | direction-only facblock (no amount) |
| The Fishslayers | page | direction-only facblock (no amount) |
| The Geologist's Purloined Tools | page | direction-only facblock (no amount) |
| The Gnome Take | page | several different faction blocks on one page |
| The Lorekeeper's Scrolls | page | direction-only facblock (no amount) |
| The Lost Circle | page | direction-only facblock (no amount) |
| The Lost Pet | page | direction-only facblock (no amount) |
| The Lottery Ticket | page | direction-only facblock (no amount) |
| The Mighty Snowfang Hero | page | direction-only facblock (no amount) |
| The Mudtoes | page | direction-only facblock (no amount) |
| The Mystic Cloak | page | direction-only facblock (no amount) |
| The Nitrates and the Assassin | page | several different faction blocks on one page |
| The Package | page | direction-only facblock (no amount) |
| The Painting | page | no turn-in item in the catalog |
| The Penance | page | direction-only facblock (no amount) |
| The Power of the Gatecallers | page | direction-only facblock (no amount) |
| The Rat King | page | direction-only facblock (no amount) |
| The Regurgitonic | page | several different faction blocks on one page |
| The Restraining Order | page | direction-only facblock (no amount) |
| The Rogue Take | page | no turn-in item in the catalog |
| The Seax | page | no turn-in item in the catalog |
| The Summoning of Dread | page | several different faction blocks on one page |
| The Summoning of Fright | page | direction-only facblock (no amount) |
| The Summoning of Terror | page | direction-only facblock (no amount) |
| The Supply Run - Eastern Wastes | page | direction-only facblock (no amount) |
| The Tattered Pouch | page | direction-only facblock (no amount) |
| The Torn Pouch | page | direction-only facblock (no amount) |
| The Torrid Corruptor | page | several different faction blocks on one page |
| The Traitor | page | direction-only facblock (no amount) |
| The Velium Focus | page | direction-only facblock (no amount) |
| The Visiting Priestess | page | direction-only facblock (no amount) |
| The Waylaid Courier | page | direction-only facblock (no amount) |
| Thex Dagger Quest | page | direction-only facblock (no amount) |
| Thex Mallet Quest | page | several turn-in items, one faction block |
| This Means Warrr | page | several turn-in items, one faction block |
| Tiny Savages | page | direction-only facblock (no amount) |
| Tiny Skeletons | page | direction-only facblock (no amount) |
| Titan Samples (good) | page | direction-only facblock (no amount) |
| Tome of Ages | page | direction-only facblock (no amount) |
| Tomer's Rescue | page | direction-only facblock (no amount) |
| Tonics for Groflah | page | direction-only facblock (no amount) |
| Torch Of Alna Quest | page | direction-only facblock (no amount) |
| Tormax's Head - Dragons | page | direction-only facblock (no amount) |
| Tormax's Head - Dwarves | page | direction-only facblock (no amount) |
| Track, Stalk, Hunt | page | several turn-in items, one faction block |
| Trueshot Longbow Quest | page | several different faction blocks on one page |
| Trumpy Irontoe | page | direction-only facblock (no amount) |
| Trumpy's Head | page | direction-only facblock (no amount) |
| Tumpy Tonics | page | several turn-in items, one faction block |
| Tunare Symbol Quests | page | direction-only facblock (no amount) |
| Tunic of Ridossan Quest | page | direction-only facblock (no amount) |
| Ulthork Tusks Quest | page | direction-only facblock (no amount) |
| Unsar's Glory | page | direction-only facblock (no amount) |
| Unser's Call | page | direction-only facblock (no amount) |
| Urako's Big Mistake | page | direction-only facblock (no amount) |
| Vambraces of Avoidance Quest | page | direction-only facblock (no amount) |
| Vasty Deep Water | page | several different faction blocks on one page |
| Velium Retrieval | page | direction-only facblock (no amount) |
| Vengeance for Frenway | page | no turn-in item in the catalog |
| Vkjor's Major Task | page | direction-only facblock (no amount) |
| Vkjor's Minor Task | page | direction-only facblock (no amount) |
| Wage War Upon The Coldain | page | direction-only facblock (no amount) |
| Wall Squad Ring | page | direction-only facblock (no amount) |
| Warrior Kael Armor Quests | page | direction-only facblock (no amount) |
| Warrior Pike Quests | page | direction-only facblock (no amount) |
| Warrior Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Watcher Torches | page | direction-only facblock (no amount) |
| Weapons Delivery | page | direction-only facblock (no amount) |
| Wenbie's Muffins | page | direction-only facblock (no amount) |
| Werewolf Hunters | page | direction-only facblock (no amount) |
| Winds of Karana | page | direction-only facblock (no amount) |
| Wizard Epic Quest | page | direction-only facblock (no amount) |
| Wizard Thurgadin Armor Quests | page | direction-only facblock (no amount) |
| Wolf Hide Armor | page | direction-only facblock (no amount) |
| Words of Darkness Quest | page | direction-only facblock (no amount) |
| Xelha's Cyclops Eye | page | direction-only facblock (no amount) |
| Yegek's Test, Part 1 | page | direction-only facblock (no amount) |
| Yegek's Test, Part 2 | page | direction-only facblock (no amount) |
| Yelinak's Head Quest | page | direction-only facblock (no amount) |
| Yuio's Illness | page | several turn-in items, one faction block |
| Zimel's Blades (SoulFire) | page | several different faction blocks on one page |
| Zombie Flesh Quest | page | direction-only facblock (no amount) |

