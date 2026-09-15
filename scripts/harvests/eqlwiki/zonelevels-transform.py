#!/usr/bin/env python3
"""Turn the cached eqlwiki zone wikitext into shipped zone LEVEL BANDS (DRA-84 D1, plan P1).

WHAT THIS IS
------------
eqlwiki zone pages carry an infobox row:

    ! ''' Level of Monsters: '''
    |5-20

`zones-harvest.py` already caches all 118 of those pages for the adjacency graph, and the
cache is COMMITTED. So this transform **fetches nothing** — consequence-list item 7 (our
request rate at eqlwiki) is not merely unchanged but untouched, exactly as
`guides-transform.py` is. Its whole input is `cache/zone-*.wikitext` plus
`zone-titles.json`, and its output is `src/EQBuddy.Core/Data/ZoneLevelBands.json`, read by
`Core/ZoneLevels.cs`.

This slice ships the INSTRUMENT and the numbers. It changes no engine: nothing in
`Recommendations` reads a band yet. The gate that will (plan P2 / D2) is a separate slice,
and it should not be written until the numbers below have been read.

PARSING IS STRICT, AND THE REPORT IS WHERE THE REST GOES
--------------------------------------------------------
Exactly two shapes are admitted from that row, after whitespace is stripped:

  * `N-M`  — a closed band, `1 <= N <= M <= 99`
  * `N`    — a single level, stored as `N-N`

**Everything else is ABSENT, never guessed** — and every rejected verbatim is listed in
`zonelevels-report.md` with the zones that carry it, because the shapes we refuse are the
evidence for whether a later slice should learn one of them. The big refused class is the
open top (`50+`, `45-60+`): a trailing `+` is a genuinely different fact from a closed band,
the plan's P2 gate reads a band's TOP, and inventing a number for "and above" is the shape
trap 73 exists to refuse. A second class is the multi-range page (`1-15, 35`), where the one
band a caller would get back is a fiction stitched from two. Neither is decided here.

Range-checking 1..99 is not a guess either — it refuses a row whose numbers cannot be levels
rather than shipping them. No row in the current cache is refused for that reason; the check
is there so a future wiki edit cannot quietly ship one.

The report also carries the **distinct-count telltale** (trap 73): if 40 zones carried 6
distinct bands between them, the data would be a template rather than a per-zone fact, and
you would want to know that before believing any of it.

ABSENT IS SHIPPED TOO, AND THAT IS THE POINT
---------------------------------------------
`ZoneLevelBands.json` carries a second section, `NoBand`: every zone page we read and did
NOT get a band out of, with the verbatim we refused (or an empty string where the page has
no row at all). Two reasons. It makes "we have never looked at this zone" and "we looked and
the page does not answer" different answers, which are different sentences to a player. And
it is what lets `ZoneLevels` refuse to hand one zone another zone's band: without the
absent list, a lookup rule loose enough to bridge a spelling difference is also loose enough
to answer "Qeynos Aqueducts" with Qeynos's `1-9`, which is a guess wearing a citation.

AND IT MEASURES THE JOIN BEFORE ANYONE TRUSTS THE GATE
-------------------------------------------------------
Item pages and zone pages are not obliged to spell a zone the same way. The gate P2 plans
looks a band up by the zone name an `ItemCatalog` record's `DropZones` carries, so the
number that actually decides whether that gate can do anything is *how many distinct
`DropZones` spellings resolve to a band*, and how many item records sit behind them.

The lookup rule is **exact title, then the repo's existing zone-identity fold**
(`ZoneMapFiles.IdentityKey` — lowercase, drop a parenthetical, drop a leading "the", squeeze
out spaces/apostrophes/hyphens). That is one producer for what counts as the same zone, and
it is deliberately NOT the longest-containment rule `ZoneGraph.Resolve` uses for travel:
containment bridges "Estate of Unrest" to "The Estate of Unrest", but it also hands
"Commonlands" West Commonlands's band, hands all four Qeynos sub-zones the city's `1-9`, and
matches the free prose that sits in some `DropZones` entries ("super ultra rare from any
spider in kaesora"). A wrong band makes the P2 gate REFUSE a zone that is fine; ABSENT makes
it do nothing. Measured over the committed catalog, containment bought 35 more spellings and
almost all of them were of that wrong kind. The fold is mirrored here in Python and pinned
against the C# by `ZoneLevelsTests`, over the exact names in this report.

The join half reads the committed `ItemCatalog.json.gz`, which **D3's refresh will rebuild**.
It is therefore a snapshot, stamped in the report with the record count it was taken
against. `--check` deliberately does NOT cover it: coupling this gate to the item catalog
would redden a refresh PR on a file it did not touch, and D3 runs in parallel with this
slice. Re-run this transform (no `--check`) after a catalog refresh to re-take the join.

BYTE-REPRODUCIBLE, AND NO CONTAINER TO ARGUE ABOUT
---------------------------------------------------
Plain JSON, sorted zone keys, fixed key order, no clock and no locale: the same cache
produces the same bytes on any machine. Unlike `HarvestedGuides.json.gz` there is no gzip
container here, so the trap-74 failure — a gate reddening on which zlib build ran it — cannot
arise, and `--check` can compare the file itself.

    python scripts/harvests/eqlwiki/zonelevels-transform.py [--check]

`--check` writes nothing and exits 1 if the committed `ZoneLevelBands.json` is not what this
produces. The write side compares the same way and leaves the file alone when nothing moved.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import json
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CACHE = HERE / "cache"
TITLES = HERE / "zone-titles.json"
REPORT = HERE / "zonelevels-report.md"
DATA = ROOT / "src" / "EQBuddy.Core" / "Data"
OUT = DATA / "ZoneLevelBands.json"
ITEM_CATALOG = DATA / "ItemCatalog.json.gz"

# The infobox row, in the same shape `zones-harvest.py` reads "Adjacent Zones" out of the
# same pages. Header line, then the value on the next line after a pipe.
LEVEL_ROW_RX = re.compile(
    r"!\s*'*\s*Level\s+of\s+Monsters\s*:?\s*'*\s*\n\|\s*([^\n]*)", re.IGNORECASE)

BAND_RX = re.compile(r"^(\d{1,2})\s*-\s*(\d{1,2})$")
SINGLE_RX = re.compile(r"^(\d{1,2})$")

MIN_LEVEL = 1
MAX_LEVEL = 99


def cache_path(title: str) -> pathlib.Path:
    """The filename `zones-harvest.py` cached this title under. Same sanitisation, so the
    band key is the WIKI TITLE — which is also what `ZoneGraph`'s nodes are keyed on."""
    safe = re.sub(r"[^A-Za-z0-9._-]", "_", title)
    return CACHE / f"zone-{safe}.wikitext"


def level_row(wikitext: str) -> str | None:
    """The `Level of Monsters` value verbatim, or None when the page has no such row."""
    m = LEVEL_ROW_RX.search(wikitext)
    if not m:
        return None
    return m.group(1).strip()


def parse_band(verbatim: str) -> tuple[int, int] | None:
    """`N-M` or `N`, in 1..99, or None. Nothing else, and nothing inferred."""
    if (m := BAND_RX.match(verbatim)) is not None:
        lo, hi = int(m.group(1)), int(m.group(2))
    elif (m := SINGLE_RX.match(verbatim)) is not None:
        lo = hi = int(m.group(1))
    else:
        return None
    if lo > hi or lo < MIN_LEVEL or hi > MAX_LEVEL:
        return None
    return lo, hi


def build(titles: list[str]):
    """Returns (bands, no_band, no_page). `no_band` is zone → the verbatim we refused, or
    "" where the page carries no row at all — both ship, because "the page does not answer"
    and "we have never read a page for this zone" are different answers."""
    bands: dict[str, dict] = {}
    no_band: dict[str, str] = {}
    no_page: list[str] = []
    for title in titles:
        path = cache_path(title)
        if not path.exists():
            no_page.append(title)
            continue
        verbatim = level_row(path.read_text(encoding="utf-8"))
        if verbatim is None or verbatim == "":
            no_band[title] = ""
            continue
        band = parse_band(verbatim)
        if band is None:
            no_band[title] = verbatim
            continue
        bands[title] = {"Min": band[0], "Max": band[1], "Verbatim": verbatim}
    return bands, no_band, no_page


def render(bands: dict[str, dict], no_band: dict[str, str]) -> str:
    """The committed bytes. Sorted keys, fixed key order inside each band, trailing
    newline — the same file on any machine from the same cache."""
    payload = {
        "Source": "eqlwiki zone pages, 'Level of Monsters' infobox row "
                  "(scripts/harvests/eqlwiki/zonelevels-transform.py; fetches nothing)",
        "Bands": {
            zone: {"Min": b["Min"], "Max": b["Max"], "Verbatim": b["Verbatim"]}
            for zone, b in sorted(bands.items())
        },
        "NoBand": {zone: v for zone, v in sorted(no_band.items())},
    }
    return json.dumps(payload, indent=1, ensure_ascii=False) + "\n"


def identity_key(zone: str) -> str:
    """The repo's zone-identity token, mirrored from `ZoneMapFiles.IdentityKey` — lowercase,
    drop a parenthetical, drop a trailing difficulty number, drop a leading "the", squeeze
    out spaces/apostrophes/hyphens.

    Mirrored rather than shared, because one side is Python and one is C#. The pin is
    ONE-DIRECTIONAL and worth being honest about: `ZoneLevelsTests` asserts the C# side's
    match/refuse outcome over the exact spellings this report's tables name, so a change to
    `ZoneMapFiles.IdentityKey` reddens. A change HERE moves the report's numbers and nothing
    goes red — so if you edit this, re-read those test rows."""
    z = zone.strip().lower()
    paren = z.find("(")
    if paren > 0:
        z = z[:paren]
    z = re.sub(r"\s+\d+\s*$", "", z)
    if z.startswith("the "):
        z = z[4:]
    return z.strip().replace(" ", "").replace("'", "").replace("-", "")


def join_survey(bands: dict[str, dict], no_band: dict[str, str]):
    """What the P2 gate would actually be able to look up.

    The number worth reading is not just "how many spellings hit a band" but WHY the rest
    miss: a spelling that lands on a zone page we read and refused is a band question, and a
    spelling that lands on no zone page at all is a catalog-data question. They go to
    different slices, so the report separates them.

    A snapshot against the catalog as committed — see the module docstring on why `--check`
    does not cover it."""
    if not ITEM_CATALOG.exists():
        return None
    catalog = json.loads(gzip.decompress(ITEM_CATALOG.read_bytes()).decode("utf-8"))
    items = catalog["Items"]

    # The fold index, with the same ambiguity rule `ZoneLevels` applies: two titles can fold
    # onto one key ("Chardok (Pre-Revamp)" / "Chardok (Post-Revamp)") and where they do not
    # agree on their whole answer the key answers nothing, rather than whichever was read
    # last.
    def answer_of(zone: str):
        if zone in bands:
            b = bands[zone]
            return ("band", b["Min"], b["Max"], b["Verbatim"])
        return ("absent", 0, 0, no_band[zone])

    first: dict[str, str] = {}
    ambiguous: set[str] = set()
    for zone in sorted(list(bands) + list(no_band)):
        key = identity_key(zone)
        if not key:
            continue
        if key in first:
            if answer_of(first[key]) != answer_of(zone):
                ambiguous.add(key)
        else:
            first[key] = zone
    banded = {k: z for k, z in first.items() if k not in ambiguous and z in bands}
    absent = {k: z for k, z in first.items() if k not in ambiguous and z in no_band}

    spellings: collections.Counter[str] = collections.Counter()
    for item in items:
        for zone in (item.get("DropZones") or []):
            spellings[zone] += 1

    hit, refused, unknown = {}, {}, []
    for zone in spellings:
        key = identity_key(zone)
        if key in banded:
            hit[zone] = banded[key]
        elif key in absent:
            refused[zone] = absent[key]
        else:
            unknown.append(zone)
    return {
        "records": len(items),
        "records_with_dropzone": sum(1 for i in items if i.get("DropZones")),
        "spellings": len(spellings),
        "mentions": sum(spellings.values()),
        "hit": hit,
        "refused": refused,
        "unknown": unknown,
        "hit_mentions": sum(spellings[z] for z in hit),
        "refused_mentions": sum(spellings[z] for z in refused),
        "unknown_mentions": sum(spellings[z] for z in unknown),
        "top_refused": sorted(((spellings[z], z, refused[z]) for z in refused), reverse=True),
        "top_unknown": sorted(((spellings[z], z) for z in unknown), reverse=True),
    }


def pct(part: int, whole: int) -> int:
    return part * 100 // max(whole, 1)


def write_report(titles, bands, no_band, no_page, survey) -> None:
    parsed_pairs = {(b["Min"], b["Max"]) for b in bands.values()}
    refused = {z: v for z, v in no_band.items() if v}
    no_row = sorted(z for z, v in no_band.items() if not v)
    grouped: dict[str, list[str]] = collections.defaultdict(list)
    for title, verbatim in refused.items():
        grouped[verbatim].append(title)

    lines = [
        "# Zone level bands report", "",
        "Written by `zonelevels-transform.py` from the COMMITTED zone wikitext cache.",
        "It fetches nothing. **Read the numbers here before trusting a band anywhere** —",
        "this slice ships the instrument and the measurement, and no engine reads a band yet.",
        "", "## Coverage", "",
        f"- Zone pages enumerated (`zone-titles.json`): **{len(titles)}**",
        f"- Bands shipped in `ZoneLevelBands.json`: **{len(bands)}** "
        f"({pct(len(bands), len(titles))}% of pages)",
        f"- ABSENT — row present but not `N-M` or `N`: **{len(refused)}**",
        f"- ABSENT — page has no `Level of Monsters` row: **{len(no_row)}**",
        f"- ABSENT — title enumerated but no cached page: **{len(no_page)}**", "",
        "The two ABSENT kinds ship in `NoBand` so a reader can tell them apart from a zone",
        "nobody has looked at.", "",
        "## Distinct-count telltale (trap 73)", "",
        f"- Distinct `Min-Max` pairs across {len(bands)} shipped bands: **{len(parsed_pairs)}**",
        "",
        "A per-zone fact should be nearly as varied as the zones carrying it. A handful of",
        "distinct values across dozens of zones would mean a template got parsed, not the",
        "wiki's own per-zone numbers, and nothing downstream should believe it.", "",
        "## Refused verbatims — the row was there and we would not read it", "",
        "Listed so a later slice can decide whether to learn one of these shapes with the",
        "evidence in front of it. Nothing here is guessed into a band. The dominant class is",
        "the **open top** (`50+`, `45-60+`): a trailing `+` is not a band's maximum, and the",
        "gate P2 plans reads a band's maximum.", "",
        "| Verbatim | Zones | Which |",
        "|---|---:|---|",
    ]
    for verbatim, zones in sorted(grouped.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        lines.append(f"| `{verbatim}` | {len(zones)} | {', '.join(sorted(zones))} |")

    lines += ["", "## Pages with no `Level of Monsters` row at all", "",
              *[f"- {z}" for z in no_row]]
    if no_page:
        lines += ["", "## Enumerated titles with no cached page", "",
                  *[f"- {z}" for z in sorted(no_page)]]

    lines += ["", "## The join — can a band actually be found for a drop zone?", ""]
    if survey is None:
        lines += ["`ItemCatalog.json.gz` is not present; the join was not measured."]
    else:
        lines += [
            f"Measured against the committed `ItemCatalog.json.gz` as it stands: "
            f"**{survey['records']}** records, **{survey['records_with_dropzone']}** of them "
            "carrying at least one `DropZones` entry. Lookup is exact title then the",
            "zone-identity fold, never containment — see the script's docstring for what",
            "containment bought and why it was refused.", "",
            f"| Where a `DropZones` spelling lands | Spellings | of {survey['spellings']} "
            f"| Mentions | of {survey['mentions']} |",
            "|---|---:|---:|---:|---:|",
            f"| On a zone we have a band for | **{len(survey['hit'])}** "
            f"| {pct(len(survey['hit']), survey['spellings'])}% "
            f"| **{survey['hit_mentions']}** "
            f"| {pct(survey['hit_mentions'], survey['mentions'])}% |",
            f"| On a zone page whose row we REFUSED | {len(survey['refused'])} "
            f"| {pct(len(survey['refused']), survey['spellings'])}% "
            f"| {survey['refused_mentions']} "
            f"| {pct(survey['refused_mentions'], survey['mentions'])}% |",
            f"| On no zone page we have read | {len(survey['unknown'])} "
            f"| {pct(len(survey['unknown']), survey['spellings'])}% "
            f"| {survey['unknown_mentions']} "
            f"| {pct(survey['unknown_mentions'], survey['mentions'])}% |", "",
            "**The middle row is the finding.** The gate's reach is not limited by spelling —",
            "it is limited by the open-top verbatims above. Every one of the heaviest drop",
            "zones in the catalog HAS a zone page, and we refused its row.", "",
            "**This half is a snapshot.** DRA-84 D3 rebuilds the item catalog; re-run this",
            "transform (no `--check`) afterwards to re-take it. `--check` deliberately does",
            "not cover the report, so a refresh PR is not reddened by a file it did not touch.",
            "",
            "### Heaviest spellings on a zone whose row we refused", "",
            "| Mentions | `DropZones` spelling | Zone page | Verbatim refused |",
            "|---:|---|---|---|",
            *[f"| {n} | {z} | {t} | `{no_band[t]}` |"
              for n, z, t in survey["top_refused"][:25]],
            "",
            "### Heaviest spellings that land on no zone page at all", "",
            "Not a band question. These are `DropZones` values that are markup, prose or a",
            "list of several zones — a catalog-data finding for the refresh and for D4's",
            "coverage survey, recorded here because this is where it was measured.", "",
            "| Mentions | `DropZones` spelling |",
            "|---:|---|",
            *[f"| {n} | {z} |" for n, z in survey["top_unknown"][:25]],
        ]
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Zone level bands from the committed cache.")
    parser.add_argument("--check", action="store_true",
                        help="write nothing; exit 1 if the committed file differs")
    args = parser.parse_args()

    titles = json.loads(TITLES.read_text(encoding="utf-8"))
    bands, no_band, no_page = build(titles)
    data = render(bands, no_band)
    current = OUT.read_text(encoding="utf-8") if OUT.exists() else None

    if args.check:
        if data != current:
            print(f"{OUT.name} differs from what this produces "
                  f"({len(current or '')} bytes on disk, {len(data)} generated).",
                  file=sys.stderr)
            return 1
        print(f"{OUT.name} is already what this produces "
              f"({len(bands)} bands, {len(data)} bytes).")
        return 0

    if data == current:
        print(f"{OUT.name} unchanged ({len(bands)} bands) — left alone.")
    else:
        OUT.write_text(data, encoding="utf-8")
        print(f"wrote {OUT.name}: {len(bands)} bands, "
              f"{len(no_band)} zones ABSENT, of {len(titles)} zone pages.")
    write_report(titles, bands, no_band, no_page, join_survey(bands, no_band))
    print(f"Report: {REPORT.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
