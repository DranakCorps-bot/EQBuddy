# Zone eras report

Written by `zone-eras-transform.py` from the COMMITTED zone wikitext cache.
It fetches nothing. **Read the numbers here before trusting an era anywhere.**

This slice (DRA-180 D1) ships the INSTRUMENT. No engine reads it yet, and nothing
in this repo states what era the WORLD is at — that fact is curated, it starts
ABSENT, and it is plan P2/P4's, not this file's.

## Coverage

- Zone pages enumerated (`zone-titles.json`): **118**
- Eras shipped in `ZoneEras.json`: **104** (88% of pages)
- ABSENT — page carries no `{{... Era}}` banner: **14**
- ABSENT — banner REFUSED (era word not on the ladder): **0**
- ABSENT — banner REFUSED (two different eras on one page): **0**
- ABSENT — title enumerated but no cached page: **0**

The ABSENT kinds ship in `NoEra` so a reader can tell them apart from a zone nobody
has looked at — `ZoneEras.Lookup` has four outcomes for exactly that reason.

## The era histogram

| Era | Zones |
|---|---:|
| Classic | 57 |
| Paineel | 1 |
| Temple | 1 |
| Kunark | 25 |
| Chardok Revamp | 1 |
| Velious | 19 |
| **Total** | **104** |

Ordered by the ladder (`QuestEraLadder.Eras`), not by count: the order IS the fact a
gate reads.

## Distinct-count telltale (trap 73) — and why there is NO floor here

**6 distinct eras across 104 pages.** On a per-zone fact that
ratio would be the template alarm `zonelevels-report.md` holds to a two-thirds
floor. **Here it is the expected shape and a floor would be wrong**: an era is a
CATEGORY every zone of an expansion shares, so 57 pages saying Classic is 57 pages
agreeing, not one template parsed 57 times. Applying a distinctness floor to a
category would fail a true reading of real pages.

So the guard is a different one, and `ZoneErasTests` holds it: **the mapping of each
known era spelling to a named zone, and the exact ABSENT list** — not a row count a
single wiki edit would redden. A parse that latched onto something shared would have
to move a named zone to be wrong, which is a thing a human can check.

## Pages with no era banner at all — the ABSENT list, by name

**"Absent means Classic" is refused, and these names are why.** Stonebrunt
Mountains, The Warrens and Kerra Island are the Paineel-adjacent set, in a corpus
where exactly one page carries `{{Paineel Era}}`. Defaulting them to Classic would
put a level-45 Warrens camp in a pre-Paineel world on the strength of a template
nobody wrote.

- Grobb
- Halas
- Highpass Hold
- Kaladim
- Kerra Island
- Lower Guk
- Misty Thicket
- Oasis of Marr
- Oggok
- Plane of Hate cleanupproject
- Rivervale
- Stonebrunt Mountains
- The Warrens
- Upper Guk

## Era words NOT on the ladder — REFUSED, never guessed

`quests-harvest.py` carries a curated rename map because QUEST pages drift
("kunark Era", "EpicQuests Era", "Chardok Era"). **The zone corpus does not**,
so no map is mirrored into this transform. If a page drifts, it lands here with its
word, and a human decides with the word in front of them.

**None.** Every banner in the corpus spells its era exactly as
`QuestEraLadder.Eras` does. The arm is exercised by
`zone-eras-transform.py --selftest` and by `ZoneErasTests`, because a
refusal that has never fired on anything is a guard aimed at nothing
(trap 78).

## Pages carrying two DIFFERENT era banners — REFUSED, never picked

**None.** Two occurrences of the SAME era would be one claim stated twice
and are admitted; there are none of those either. Same trap-78 note as
above — the arm is exercised in the selftest and in `ZoneErasTests`.

## Where the banner sits on the page

Reported as evidence that the parse does NOT need to be positional. A rule written
against a POSITION is a rule about the wrong fact (trap 66), so the whole page is
searched and this table is a measurement rather than a constraint.

| Line | Pages |
|---:|---:|
| 1 | 102 |
| 2 | 2 |

## The identity fold, and the one collision in the corpus

Lookup is exact title, then `ZoneMapFiles.IdentityKey`, and **nothing looser** — the
`ZoneLevels` rule verbatim. Containment would hand "Commonlands" West Commonlands's
answer and would match a zone name sitting inside free prose, and a wrong era is a
claim a surface states as fact.

| Identity key | Titles | What each page says |
|---|---|---|
| `chardok` | 2 | Chardok (Post-Revamp) = Chardok Revamp, Chardok (Pre-Revamp) = Kunark |

**The folded key answers the EARLIER era.** Content that exists from Kunark on
exists in a Chardok-Revamp world too, so the earlier era is the true answer to
"has the world reached this place yet" — the later one would refuse a zone that
is in the game. The item catalog's `DropZones` just says `Chardok`, which is why
this collision has to be decided at all.

The rule lives in `ZoneEras.cs`, not in this transform, which emits what each
PAGE said (trap 4). Its other arm — a disagreement where either side is ABSENT
answers NOTHING, because an absence is not an era and cannot be compared — has no
instance in this corpus and is guarded against a fixture in `ZoneErasTests`.

## The join — can an era actually be found for a drop zone?

Measured against the committed `ItemCatalog.json.gz` as it stands: **11196** records, **5626** of
them carrying at least one `DropZones` entry. This is what P1's gate will
have to read, so it is measured before the gate is built rather than after
it disappoints somebody.

| Where a `DropZones` spelling lands | Spellings | of 297 | Mentions | of 10637 |
|---|---:|---:|---:|---:|
| On a zone we have an era for | **108** | 36% | **8695** | 81% |
| On a zone page whose banner is ABSENT | 15 | 5% | 674 | 6% |
| On no zone page we have read | 174 | 58% | 1268 | 11% |

**The middle and bottom rows are where the gate stands down**, per P1's
per-arm stand-down: an unmapped zone leaves the era arm silent and lets the band
and who rules run. Neither is a refusal.

### Drop weight by era — what a world era would actually reach

| Era | Mentions |
|---|---:|
| Classic | 4690 |
| Paineel | 8 |
| Temple | 11 |
| Kunark | 1918 |
| Velious | 2068 |

### Heaviest spellings that DO land on an era

| Mentions | `DropZones` spelling | Era |
|---:|---|---|
| 283 | Plane of Sky | Classic |
| 281 | Plane of Hate | Classic |
| 247 | Temple of Veeshan | Velious |
| 232 | Plane of Fear | Classic |
| 211 | Lesser Faydark | Classic |
| 186 | The Estate of Unrest | Classic |
| 178 | Kael Drakkel | Velious |
| 177 | Old Sebilis | Kunark |
| 163 | Steamfont Mountains | Classic |
| 163 | Rathe Mountains | Classic |
| 155 | Chardok | Kunark |
| 153 | Lake of Ill Omen | Kunark |
| 149 | Dragon Necropolis | Velious |
| 146 | Velketor's Labyrinth | Velious |
| 145 | Northern Desert of Ro | Classic |
| 145 | Mistmoore Castle | Classic |
| 144 | Western Wastes | Velious |
| 141 | Butcherblock Mountains | Classic |
| 140 | The Wakening Land | Velious |
| 137 | Nagafen's Lair | Classic |
| 133 | Everfrost Peaks | Classic |
| 133 | Eastern Wastes | Velious |
| 131 | Karnor's Castle | Kunark |
| 127 | Ocean of Tears | Classic |
| 125 | Southern Karana | Classic |

**This half is a snapshot.** A catalog refresh rebuilds `ItemCatalog.json.gz`;
re-run this transform (no `--check`) afterwards to re-take it. `--check`
deliberately does not cover the report, so a refresh PR is not reddened by a
file it did not touch.
