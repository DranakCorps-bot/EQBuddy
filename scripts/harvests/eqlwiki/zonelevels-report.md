# Zone level bands report

Written by `zonelevels-transform.py` from the COMMITTED zone wikitext cache.
It fetches nothing. **Read the numbers here before trusting a band anywhere** —
this slice ships the instrument and the measurement, and no engine reads a band yet.

## Coverage

- Zone pages enumerated (`zone-titles.json`): **118**
- Bands shipped in `ZoneLevelBands.json`: **46** (38% of pages)
- ABSENT — row present but not `N-M` or `N`: **57**
- ABSENT — page has no `Level of Monsters` row: **15**
- ABSENT — title enumerated but no cached page: **0**

The two ABSENT kinds ship in `NoBand` so a reader can tell them apart from a zone
nobody has looked at.

## Distinct-count telltale (trap 73)

- Distinct `Min-Max` pairs across 46 shipped bands: **36**

A per-zone fact should be nearly as varied as the zones carrying it. A handful of
distinct values across dozens of zones would mean a template got parsed, not the
wiki's own per-zone numbers, and nothing downstream should believe it.

## Refused verbatims — the row was there and we would not read it

Listed so a later slice can decide whether to learn one of these shapes with the
evidence in front of it. Nothing here is guessed into a band. The dominant class is
the **open top** (`50+`, `45-60+`): a trailing `+` is not a band's maximum, and the
gate P2 plans reads a band's maximum.

| Verbatim | Zones | Which |
|---|---:|---|
| `50+` | 5 | Chardok (Post-Revamp), Chardok (Pre-Revamp), Howling Stones, Plane of Mischief, Plane of Sky |
| `45-60+` | 3 | Dragon Necropolis, Velketor's Labyrinth, Western Wastes |
| `48+` | 3 | Plane of Fear, Plane of Hate, Plane of Hate cleanupproject |
| `1-30+` | 2 | Lake of Ill Omen, Warsliks Woods |
| `10-30+` | 2 | Eastern Plains of Karana, Northern Plains of Karana |
| `55+` | 2 | Plane of Growth, Sleeper's Tomb |
| `60+` | 2 | Temple of Veeshan, Veeshan's Peak |
| `7-30+` | 2 | Oasis of Marr, Runnyeye |
| `1-10, 25-30` | 1 | Innothule Swamp |
| `1-13+, 35-50` | 1 | Kithicor Forest |
| `1-15, 33-38` | 1 | Qeynos Aqueducts |
| `1-15, 35` | 1 | Butcherblock Mountains |
| `1-18, 30-35` | 1 | Steamfont Mountains |
| `1-20+` | 1 | Everfrost Peaks |
| `1-20, 25-30` | 1 | Nektulos Forest |
| `1-20, 35` | 1 | East Commonlands |
| `1-25+` | 1 | Swamp of No Hope |
| `1-30, 34-40` | 1 | The Feerrott |
| `1-50+` | 1 | Field of Bone |
| `10-19, 25-30` | 1 | Lavastorm Mountains |
| `10-30, 40-50` | 1 | Lesser Faydark |
| `15-50+` | 1 | Permafrost |
| `20-40+ (50+ inside pit)` | 1 | The Overthere |
| `25-35+` | 1 | Dalnir |
| `29-34 Droga Main, 33-38 Inner Sanctum` | 1 | Temple of Droga |
| `30-35 (in caves), 30-45 (dwarves)` | 1 | Thurgadin |
| `30-50+` | 1 | Lower Guk |
| `30-60+` | 1 | Kael Drakkel |
| `33-60+` | 1 | The Wakening Land |
| `35-50+` | 1 | Dreadlands |
| `35-60+` | 1 | Cobalt Scar |
| `4-15+` | 1 | Blackburrow |
| `4-20+` | 1 | Western Karana |
| `4-25+` | 1 | Upper Guk |
| `40-55+` | 1 | Karnor's Castle |
| `40-60+` | 1 | Skyfire Mountains |
| `5-20+` | 1 | Southern Desert of Ro |
| `5-30+` | 1 | The Northern Desert of Ro |
| `50-60+` | 1 | Siren's Grotto |
| `7-25+` | 1 | Befallen |
| `9-30+` | 1 | Ocean of Tears |
| `?` | 1 | Surefall Glade |
| `Quest Only` | 1 | The Temple of Solusek Ro |
| `n/a` | 1 | The Arena |

## Pages with no `Level of Monsters` row at all

- Ak'Anon
- Cabilis
- Erudin
- Felwithe
- Freeport
- Grobb
- Halas
- Kaladim
- Kelethin
- Misty Thicket
- Neriak
- New Sebilis Expedition
- Oggok
- Paineel
- Rivervale

## The join — can a band actually be found for a drop zone?

Measured against the committed `ItemCatalog.json.gz` as it stands: **11146** records, **5613** of them carrying at least one `DropZones` entry. Lookup is exact title then the
zone-identity fold, never containment — see the script's docstring for what
containment bought and why it was refused.

| Where a `DropZones` spelling lands | Spellings | of 302 | Mentions | of 10612 |
|---|---:|---:|---:|---:|
| On a zone we have a band for | **48** | 15% | **3564** | 33% |
| On a zone page whose row we REFUSED | 74 | 24% | 5743 | 54% |
| On no zone page we have read | 180 | 59% | 1305 | 12% |

**The middle row is the finding.** The gate's reach is not limited by spelling —
it is limited by the open-top verbatims above. Every one of the heaviest drop
zones in the catalog HAS a zone page, and we refused its row.

**This half is a snapshot.** DRA-84 D3 rebuilds the item catalog; re-run this
transform (no `--check`) afterwards to re-take it. `--check` deliberately does
not cover the report, so a refresh PR is not reddened by a file it did not touch.

### Heaviest spellings on a zone whose row we refused

| Mentions | `DropZones` spelling | Zone page | Verbatim refused |
|---:|---|---|---|
| 293 | Plane of Sky | Plane of Sky | `50+` |
| 278 | Plane of Hate | Plane of Hate | `48+` |
| 247 | Temple of Veeshan | Temple of Veeshan | `60+` |
| 228 | Plane of Fear | Plane of Fear | `48+` |
| 209 | Lesser Faydark | Lesser Faydark | `10-30, 40-50` |
| 177 | Kael Drakkel | Kael Drakkel | `30-60+` |
| 161 | Steamfont Mountains | Steamfont Mountains | `1-18, 30-35` |
| 154 | Chardok | Chardok (Post-Revamp) | `50+` |
| 153 | Lake of Ill Omen | Lake of Ill Omen | `1-30+` |
| 149 | Lower Guk | Lower Guk | `30-50+` |
| 147 | Dragon Necropolis | Dragon Necropolis | `45-60+` |
| 146 | Velketor's Labyrinth | Velketor's Labyrinth | `45-60+` |
| 145 | Northern Desert of Ro | The Northern Desert of Ro | `5-30+` |
| 144 | Western Wastes | Western Wastes | `45-60+` |
| 141 | Butcherblock Mountains | Butcherblock Mountains | `1-15, 35` |
| 134 | The Wakening Land | The Wakening Land | `33-60+` |
| 133 | Everfrost Peaks | Everfrost Peaks | `1-20+` |
| 131 | Karnor's Castle | Karnor's Castle | `40-55+` |
| 127 | Ocean of Tears | Ocean of Tears | `9-30+` |
| 125 | Southern Desert of Ro | Southern Desert of Ro | `5-20+` |
| 118 | Nektulos Forest | Nektulos Forest | `1-20, 25-30` |
| 109 | Upper Guk | Upper Guk | `4-25+` |
| 109 | Befallen | Befallen | `7-25+` |
| 105 | Plane of Growth | Plane of Growth | `55+` |
| 104 | Siren's Grotto | Siren's Grotto | `50-60+` |

### Heaviest spellings that land on no zone page at all

Not a band question. These are `DropZones` values that are markup, prose or a
list of several zones — a catalog-data finding for the refresh and for D4's
coverage survey, recorded here because this is where it was measured.

| Mentions | `DropZones` spelling |
|---:|---|
| 136 | Western Plains of Karana |
| 114 | Commonlands |
| 108 | Various Zones |
| 102 | Burning Woods |
| 73 | Northern Karana |
| 73 | Clan Runnyeye |
| 68 | Crypt of Dalnir |
| 68 | Beholder's Maze |
| 48 | West Freeport |
| 48 | North Qeynos |
| 26 | East Freeport |
| 21 | (ToV East mobs) |
| 18 | East Cabilis |
| 16 | South Qeynos |
| 16 | North Kaladim |
| 14 | South Kaladim |
| 13 | South Karana |
| 13 | Neriak Third Gate |
| 12 | {{VeliousGray| The Warrens }} |
| 11 | RunnyEye Citadel |
| 10 | Plane of Fear<br> |
| 7 | {{VeliousGray| Stonebrunt Mountains }} |
| 7 | {{VeliousGray| Cobalt Scar }} |
| 7 | North Freeport |
| 7 | Kerra Isle |
