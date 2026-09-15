# Zone level bands report

Written by `zonelevels-transform.py` from the COMMITTED zone wikitext cache.
It fetches nothing. **Read the numbers here before trusting a band anywhere.**

## Coverage

- Zone pages enumerated (`zone-titles.json`): **118**
- Bands shipped in `ZoneLevelBands.json`: **87** (73% of pages)
  - of those, CLOSED (`N-M` / `N`): **46**
  - of those, OPEN TOP (`N+` / `N-M+`, `Max` is null): **41**
- ABSENT — row present and in none of the four admitted shapes: **16**
- ABSENT — page has no `Level of Monsters` row: **15**
- ABSENT — title enumerated but no cached page: **0**

The two ABSENT kinds ship in `NoBand` so a reader can tell them apart from a zone
nobody has looked at.

**An open top is a band with a bottom and no top, and a caller has to handle it as
one.** `Max` is JSON `null` and `ZoneLevels.Band.Max` is `int?`; the DRA-84 D2 gate's
TOP arm stands down for these zones and only its BOTTOM arm (`Min`) can refuse one.
That is the whole of what Helm's option (a) authorised — no maximum is invented, and
the number before the `+` is not promoted into one.

## Distinct-count telltale (trap 73)

A per-zone fact should be nearly as varied as the zones carrying it. A handful of
distinct values across dozens of zones would mean a template got parsed, not the
wiki's own per-zone numbers, and nothing downstream should believe it.

- Distinct `Min-Max` pairs across all 87 shipped bands: **53**
- Distinct verbatim rows across all 87: **64**
- Distinct `Min-Max` pairs across the 46 CLOSED bands: **36**
- Distinct `Min` across the 41 OPEN TOPS: **17**

**Read the last two rows, not the first.** D2's open top DISCARDS the maximum by
design, so the all-bands pair count is measuring a deliberately coarser fact than D1's
was and its ratio fell for that reason rather than because the data got worse. The
closed-band ratio is the one the two-thirds floor was calibrated on, and the verbatim
count is the measure a template would actually collapse — a shared infobox default
would show up as one row string on dozens of pages.

The open tops repeat more than the closed bands do, and that repetition is the wiki's
own: five plane pages print `50+`, three print `48+`. That is a real shared value on
real separate pages, not a parse latching onto a default, which is why it is reported
here as a measurement and is not held to the floor.

## Refused verbatims — the row was there and we would not read it

Listed so a later slice can decide whether to learn one of these shapes with the
evidence in front of it. Nothing here is guessed into a band.

**The open top has LEFT this table** (DRA-84 D2): `50+` and `45-60+` are now bands
with a null `Max`. What is left is the class where a trailing `+` is not the only
thing wrong with the row — a multi-range (`1-15, 35`), a range plus prose
(`20-40+ (50+ inside pit)`), or the page declining to answer (`Quest Only`, `n/a`,
`?`). Coalescing a multi-range into one open top is refused by name: the bottom of
the first range and no top would assert a continuity the page contradicts.

| Verbatim | Zones | Which |
|---|---:|---|
| `1-10, 25-30` | 1 | Innothule Swamp |
| `1-13+, 35-50` | 1 | Kithicor Forest |
| `1-15, 33-38` | 1 | Qeynos Aqueducts |
| `1-15, 35` | 1 | Butcherblock Mountains |
| `1-18, 30-35` | 1 | Steamfont Mountains |
| `1-20, 25-30` | 1 | Nektulos Forest |
| `1-20, 35` | 1 | East Commonlands |
| `1-30, 34-40` | 1 | The Feerrott |
| `10-19, 25-30` | 1 | Lavastorm Mountains |
| `10-30, 40-50` | 1 | Lesser Faydark |
| `20-40+ (50+ inside pit)` | 1 | The Overthere |
| `29-34 Droga Main, 33-38 Inner Sanctum` | 1 | Temple of Droga |
| `30-35 (in caves), 30-45 (dwarves)` | 1 | Thurgadin |
| `?` | 1 | Surefall Glade |
| `Quest Only` | 1 | The Temple of Solusek Ro |
| `n/a` | 1 | The Arena |

## Open tops learned — every zone whose `Max` is null

Here in full rather than summarised, because this is the class D1 refused and D2
admitted on a ruling, and the row a reader should be able to audit one zone at a
time. `Min` is the page's own bottom; the number after the dash in a `N-M+` verbatim
is NOT the `Max` and is not shipped as one.

| Zone | `Min` | `Max` | Verbatim |
|---|---:|---|---|
| Befallen | 7 | *null* | `7-25+` |
| Blackburrow | 4 | *null* | `4-15+` |
| Chardok (Post-Revamp) | 50 | *null* | `50+` |
| Chardok (Pre-Revamp) | 50 | *null* | `50+` |
| Cobalt Scar | 35 | *null* | `35-60+` |
| Dalnir | 25 | *null* | `25-35+` |
| Dragon Necropolis | 45 | *null* | `45-60+` |
| Dreadlands | 35 | *null* | `35-50+` |
| Eastern Plains of Karana | 10 | *null* | `10-30+` |
| Everfrost Peaks | 1 | *null* | `1-20+` |
| Field of Bone | 1 | *null* | `1-50+` |
| Howling Stones | 50 | *null* | `50+` |
| Kael Drakkel | 30 | *null* | `30-60+` |
| Karnor's Castle | 40 | *null* | `40-55+` |
| Lake of Ill Omen | 1 | *null* | `1-30+` |
| Lower Guk | 30 | *null* | `30-50+` |
| Northern Plains of Karana | 10 | *null* | `10-30+` |
| Oasis of Marr | 7 | *null* | `7-30+` |
| Ocean of Tears | 9 | *null* | `9-30+` |
| Permafrost | 15 | *null* | `15-50+` |
| Plane of Fear | 48 | *null* | `48+` |
| Plane of Growth | 55 | *null* | `55+` |
| Plane of Hate | 48 | *null* | `48+` |
| Plane of Hate cleanupproject | 48 | *null* | `48+` |
| Plane of Mischief | 50 | *null* | `50+` |
| Plane of Sky | 50 | *null* | `50+` |
| Runnyeye | 7 | *null* | `7-30+` |
| Siren's Grotto | 50 | *null* | `50-60+` |
| Skyfire Mountains | 40 | *null* | `40-60+` |
| Sleeper's Tomb | 55 | *null* | `55+` |
| Southern Desert of Ro | 5 | *null* | `5-20+` |
| Swamp of No Hope | 1 | *null* | `1-25+` |
| Temple of Veeshan | 60 | *null* | `60+` |
| The Northern Desert of Ro | 5 | *null* | `5-30+` |
| The Wakening Land | 33 | *null* | `33-60+` |
| Upper Guk | 4 | *null* | `4-25+` |
| Veeshan's Peak | 60 | *null* | `60+` |
| Velketor's Labyrinth | 45 | *null* | `45-60+` |
| Warsliks Woods | 1 | *null* | `1-30+` |
| Western Karana | 4 | *null* | `4-20+` |
| Western Wastes | 45 | *null* | `45-60+` |

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

Measured against the committed `ItemCatalog.json.gz` as it stands: **11196** records, **5626** of them carrying at least one `DropZones` entry. Lookup is exact title then the
zone-identity fold, never containment — see the script's docstring for what
containment bought and why it was refused.

| Where a `DropZones` spelling lands | Spellings | of 297 | Mentions | of 10637 |
|---|---:|---:|---:|---:|
| On a zone we have a band for | **93** | 31% | **7987** | 75% |
| On a zone page whose row we REFUSED | 30 | 10% | 1382 | 12% |
| On no zone page we have read | 174 | 58% | 1268 | 11% |

**Read the first two rows against D1's own numbers.** When D1 shipped, the
middle row carried 54% of the catalog's drop weight and the finding was that the
gate's reach was limited by the open-top verbatims rather than by spelling. D2
learned that class, so weight has moved from the middle row to the top one. What
is left in the middle is the multi-range and prose class, which stays refused.
**An open-top hit is not a hit on both arms** — those zones can only ever be
refused by the gate's BOTTOM arm, so the top row overstates what a TOP-arm
reading can reach. The open-top table above is the denominator for that.

**This half is a snapshot.** DRA-84 D3 rebuilds the item catalog; re-run this
transform (no `--check`) afterwards to re-take it. `--check` deliberately does
not cover the report, so a refresh PR is not reddened by a file it did not touch.

### Heaviest spellings on a zone whose row we refused

| Mentions | `DropZones` spelling | Zone page | Verbatim refused |
|---:|---|---|---|
| 211 | Lesser Faydark | Lesser Faydark | `10-30, 40-50` |
| 163 | Steamfont Mountains | Steamfont Mountains | `1-18, 30-35` |
| 141 | Butcherblock Mountains | Butcherblock Mountains | `1-15, 35` |
| 120 | Nektulos Forest | Nektulos Forest | `1-20, 25-30` |
| 100 | The Feerrott | The Feerrott | `1-30, 34-40` |
| 82 | Kithicor Forest | Kithicor Forest | `1-13+, 35-50` |
| 76 | Lavastorm Mountains | Lavastorm Mountains | `10-19, 25-30` |
| 75 | The Overthere | The Overthere | `20-40+ (50+ inside pit)` |
| 74 | Innothule Swamp | Innothule Swamp | `1-10, 25-30` |
| 67 | Misty Thicket | Misty Thicket | `` |
| 60 | Temple of Droga | Temple of Droga | `29-34 Droga Main, 33-38 Inner Sanctum` |
| 47 | Qeynos Aqueducts | Qeynos Aqueducts | `1-15, 33-38` |
| 28 | Thurgadin | Thurgadin | `30-35 (in caves), 30-45 (dwarves)` |
| 26 | Ak'Anon | Ak'Anon | `` |
| 21 | East Commonlands | East Commonlands | `1-20, 35` |
| 15 | Rivervale | Rivervale | `` |
| 12 | Surefall Glade | Surefall Glade | `?` |
| 12 | New Sebilis Expedition | New Sebilis Expedition | `` |
| 11 | Temple of Solusek Ro | The Temple of Solusek Ro | `Quest Only` |
| 9 | Erudin | Erudin | `` |
| 8 | Paineel | Paineel | `` |
| 8 | Oggok | Oggok | `` |
| 5 | Kaladim | Kaladim | `` |
| 3 | Halas | Halas | `` |
| 2 | Overthere | The Overthere | `20-40+ (50+ inside pit)` |

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
| 49 | West Freeport |
| 49 | North Qeynos |
| 26 | East Freeport |
| 21 | (ToV East mobs) |
| 18 | East Cabilis |
| 16 | South Qeynos |
| 16 | North Kaladim |
| 15 | South Kaladim |
| 13 | South Karana |
| 13 | Neriak Third Gate |
| 11 | RunnyEye Citadel |
| 10 | {{VeliousGray| The Warrens }} |
| 7 | North Freeport |
| 7 | Kerra Isle |
| 6 | West Karana |
| 6 | North Karana |
| 5 | {{VeliousGray| Stonebrunt Mountains }} |
