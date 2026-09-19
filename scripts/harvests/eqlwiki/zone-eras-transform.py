#!/usr/bin/env python3
"""Turn the cached eqlwiki zone wikitext into a shipped ZONE -> ERA table (DRA-180 D1, plan P1).

WHAT THIS IS
------------
eqlwiki zone pages open with an era banner template:

    {{Classic Era}}
    {{Chardok Revamp Era}}

`zones-harvest.py` already caches all 118 of those pages for the adjacency graph, and the
cache is COMMITTED. So this transform **fetches nothing** — consequence-list item 7 (our
request rate at eqlwiki) is not merely unchanged but untouched, exactly as
`zonelevels-transform.py` and `merchants-transform.py` are. Its whole input is
`cache/zone-*.wikitext` plus `zone-titles.json`, and its output is
`src/EQBuddy.Core/Data/ZoneEras.json`, read by `Core/ZoneEras.cs`.

**D1 ships the INSTRUMENT and changes no engine.** DRA-180's plan P1 names the first
reader: an era gate beside `Recommendations`' band gate, refusing a catalog zone row whose
era is later than the world's. Nothing in this file knows about that, and nothing in this
file states what era the WORLD is at — that fact is curated, it starts ABSENT, and it is
P2/P4's, not this slice's. This publishes what the page said; the judgement lives with the
engine.

WHY A BAND COULD NEVER ANSWER THIS
----------------------------------
The Founder's smoke put `The Baron's Blade` (a Befallen drop) against 25 dominating
catalog candidates, 20 of them Kunark/Velious raid loot. Kael Drakkel's `Level of Monsters`
row reads `30-60+`, so its `Min` is 30 and a level-29 character clears
`GearBandReachAbove` by four — the band gate PASSES it, correctly, because Kael really does
hold level-30 giants. In an era the world has not reached. A level band cannot express an
expansion; the era template can, and it is already on the page.

PARSING IS STRICT, AND THE REPORT IS WHERE THE REST GOES
--------------------------------------------------------
One shape is admitted — `{{<Word(s)> Era}}` — and the word must be one of
`QuestEraLadder.Eras`, mirrored below. Two refusals, and both are REPORTED by name rather
than guessed at:

  * **an era word that is not on the ladder.** `quests-harvest.py` carries a curated rename
    map for this, because QUEST pages really do drift ("kunark Era", "EpicQuests Era",
    "Chardok Era"). **The zone corpus does not**: all 104 zone pages that carry a banner
    spell it exactly as the ladder does, measured. So no map is mirrored here — inventing
    one against a corpus that does not need it is a fold nobody can check (trap 73). The
    day a page drifts, the report names the word and somebody decides with the word in
    front of them.
  * **two DIFFERENT era templates on one page.** One page, two claims about when its
    content exists, and picking either is a coin toss wearing a citation. Two occurrences
    of the SAME era are one claim stated twice and are admitted.

Matching is case-insensitive and the LADDER's spelling is what ships in `Era`, so a
lowercase wiki edit cannot move the committed bytes of the field a gate reads. The page's
own template text ships beside it in `Verbatim`, which is what a surface quotes.

The match is deliberately NOT positional. 102 of the 104 banners sit on line 1 and two
(Mines of Nurga, Permafrost) sit on line 2 — the report prints that distribution as
evidence — but a rule written against a POSITION is a rule about the wrong fact (trap 66),
so the whole page is searched.

ABSENT IS SHIPPED TOO, AND THAT IS THE POINT
---------------------------------------------
`ZoneEras.json` carries a second section, `NoEra`: every zone page we read and did NOT get
an era out of, with the verbatim we refused (or an empty string where the page carries no
banner at all). The `ZoneLevels.NoBand` idiom, for the same two reasons. It makes "we have
never looked at this zone" and "we looked and the page does not say" different answers,
which are different sentences to a player. And it is what lets `ZoneEras` refuse to hand
one zone another zone's era.

**"Absent means Classic" is refused by name, and the corpus is why.** 14 pages carry no
banner, and three of them — Stonebrunt Mountains, The Warrens, Kerra Island — are the
Paineel-adjacent set, in a corpus where exactly one page carries `{{Paineel Era}}`.
Defaulting them to Classic would put a level-45 Warrens camp in a pre-Paineel world on the
strength of a template nobody wrote. ABSENT ships as its own outcome.

THE FOLD, AND THE ONE COLLISION IN THE CORPUS
----------------------------------------------
Lookup is **exact title, then the repo's existing zone-identity fold**
(`ZoneMapFiles.IdentityKey`), and **nothing looser** — the `ZoneLevels` rule verbatim, for
the reason spelled out there: containment would hand "Commonlands" West Commonlands's
answer and would match a zone name sitting inside free prose, and a wrong era is a claim a
surface states as fact.

Exactly one pair of enumerated titles folds onto one key: `Chardok (Pre-Revamp)`
({{Kunark Era}}) and `Chardok (Post-Revamp)` ({{Chardok Revamp Era}}), while the item
catalog's `DropZones` just says `Chardok`. **The folded key answers the EARLIER era**
(Kunark) — content that exists from Kunark on exists in a Chardok-Revamp world too, so the
earlier era is the true answer to "has the world reached this place yet", and the later one
would refuse a zone that is in the game.

That rule lives in `ZoneEras.cs` and is NOT re-implemented here (trap 4): this file emits
what each PAGE said, one row per title, and the report names the collision and the rule
that resolves it. The rule's other arm — a disagreement where either side is ABSENT
answers nothing, because an absence is not an era and cannot be compared — is guarded in
`ZoneErasTests` against a fixture, since the corpus's one collision is Dated on both sides.

BYTE-REPRODUCIBLE, AND NO CONTAINER TO ARGUE ABOUT
---------------------------------------------------
Plain JSON, sorted zone keys, fixed key order, no clock and no locale: the same cache
produces the same bytes on any machine. **Unlike `HarvestedGuides.json.gz` there is no gzip
container here**, so the trap-74 failure — a gate reddening on which zlib build ran it —
cannot arise, and `--check` can compare the file itself.

    python scripts/harvests/eqlwiki/zone-eras-transform.py [--check] [--selftest]

`--check` writes nothing and exits 1 if the committed `ZoneEras.json` is not what this
produces. The write side compares the same way and leaves the file alone when nothing moved.

`--selftest` writes nothing either and exercises the parser's arms over synthetic wikitext.
It exists because **both refusal arms are unreachable in the current corpus** — 0 pages are
off-ladder and 0 carry two eras — and a refusal that has never fired on anything is a guard
aimed at nothing (trap 78). It also asserts the ladder is non-empty, which is the same trap
from the other side: an empty admitted set refuses everything and reports a clean corpus.
Both `--check` and `--selftest` run in `check.ps1` and CI.
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
REPORT = HERE / "zone-eras-report.md"
DATA = ROOT / "src" / "EQBuddy.Core" / "Data"
OUT = DATA / "ZoneEras.json"
ITEM_CATALOG = DATA / "ItemCatalog.json.gz"

# Mirrored from `QuestEraLadder.Eras` (src/EQBuddy.Core/QuestCatalog.cs). Mirrored rather
# than shared, because one side is Python and one is C# — the same call `identity_key`
# below makes. The pin is `ZoneErasTests.EveryShippedEraIsOnTheQuestLadder`, which reads
# the shipped file against the C# array, so a word this file admits that the ladder does
# not carry reddens there.
LADDER = ["Classic", "Sky", "Paineel", "Temple", "Epics", "Kunark",
          "Chardok Revamp", "Velious", "Luclin"]

# The era banner, in the shape `quests-harvest.py` reads the same template out of quest
# pages. `\s+` before `Era` is what keeps `{{Era|Kunark}}` — a real `DropZones` value in the
# item catalog — from matching, and the `[A-Za-z' ]` class is what keeps `{{VeliousGray|
# Skyshrine }}` out.
ERA_RX = re.compile(r"\{\{\s*([A-Za-z' ]+?)\s+Era\s*\}\}", re.IGNORECASE)


def canonical(word: str) -> str | None:
    """The ladder's own spelling for an era word, or None when it is not one of ours.

    Case-insensitive and whitespace-squeezed, so `{{velious  era}}` is Velious. Nothing
    else is folded: there is no rename map here and the docstring says why."""
    squeezed = " ".join(word.split()).casefold()
    for era in LADDER:
        if era.casefold() == squeezed:
            return era
    return None


def cache_path(title: str) -> pathlib.Path:
    """The filename `zones-harvest.py` cached this title under. Same sanitisation, so the
    era key is the WIKI TITLE — which is also what `ZoneGraph`'s nodes are keyed on."""
    safe = re.sub(r"[^A-Za-z0-9._-]", "_", title)
    return CACHE / f"zone-{safe}.wikitext"


def read_eras(wikitext: str) -> list[tuple[str, str]]:
    """Every era banner on the page, as (word, verbatim template), in page order."""
    return [(m.group(1).strip(), m.group(0)) for m in ERA_RX.finditer(wikitext)]


def decide(found: list[tuple[str, str]]):
    """One page's whole answer: `("dated", era, verbatim)`, `("off-ladder", word, verbatim)`,
    `("two-eras", "", verbatim)` or `("none", "", "")`.

    The two refusals are separated because they are different findings for a human — one is
    a wiki word we have never seen, the other is a page making two claims — even though both
    ship into `NoEra` as a refused verbatim."""
    if not found:
        return ("none", "", "")

    names = {" ".join(w.split()).casefold() for w, _ in found}
    if len(names) > 1:
        # Every template on the page, in page order, so the report can show the conflict.
        return ("two-eras", "", " ".join(v for _, v in found))

    word, verbatim = found[0]
    era = canonical(word)
    if era is None:
        return ("off-ladder", " ".join(word.split()), verbatim)
    return ("dated", era, verbatim)


def build(titles: list[str]):
    """Returns (eras, no_era, no_page, refusals).

    `no_era` is zone -> the verbatim we refused, or "" where the page carries no banner at
    all — both ship, because "the page does not say" and "we have never read a page for this
    zone" are different answers. `refusals` keeps WHY, for the report only."""
    eras: dict[str, dict] = {}
    no_era: dict[str, str] = {}
    no_page: list[str] = []
    refusals: dict[str, tuple[str, str, str]] = {}
    for title in titles:
        path = cache_path(title)
        if not path.exists():
            no_page.append(title)
            continue
        kind, value, verbatim = decide(read_eras(path.read_text(encoding="utf-8")))
        if kind == "dated":
            eras[title] = {"Era": value, "Verbatim": verbatim}
        elif kind == "none":
            no_era[title] = ""
        else:
            no_era[title] = verbatim
            refusals[title] = (kind, value, verbatim)
    return eras, no_era, no_page, refusals


def render(eras: dict[str, dict], no_era: dict[str, str]) -> str:
    """The committed bytes. Sorted keys, fixed key order inside each row, trailing
    newline — the same file on any machine from the same cache."""
    payload = {
        "Source": "eqlwiki zone pages, the {{<Era> Era}} banner template "
                  "(scripts/harvests/eqlwiki/zone-eras-transform.py; fetches nothing)",
        "Eras": {
            zone: {"Era": row["Era"], "Verbatim": row["Verbatim"]}
            for zone, row in sorted(eras.items())
        },
        "NoEra": {zone: v for zone, v in sorted(no_era.items())},
    }
    return json.dumps(payload, indent=1, ensure_ascii=False) + "\n"


def identity_key(zone: str) -> str:
    """The repo's zone-identity token, mirrored from `ZoneMapFiles.IdentityKey` — lowercase,
    drop a parenthetical, drop a trailing difficulty number, drop a leading "the", squeeze
    out spaces/apostrophes/hyphens.

    Used HERE only to find and report the collisions; the rule that RESOLVES one lives in
    `ZoneEras.cs` (trap 4). Same one-directional pin `zonelevels-transform.py` carries: a
    change to the C# reddens `ZoneErasTests`, a change here only moves this report's
    numbers — so if you edit this, re-read those rows."""
    z = zone.strip().lower()
    paren = z.find("(")
    if paren > 0:
        z = z[:paren]
    z = re.sub(r"\s+\d+\s*$", "", z)
    if z.startswith("the "):
        z = z[4:]
    return z.strip().replace(" ", "").replace("'", "").replace("-", "")


def banner_lines(titles: list[str]) -> collections.Counter:
    """Which line the first banner sits on, page by page. Reported as EVIDENCE that the
    match does not need to be positional — not used by the parse."""
    lines: collections.Counter = collections.Counter()
    for title in titles:
        path = cache_path(title)
        if not path.exists():
            continue
        for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if ERA_RX.search(line):
                lines[n] += 1
                break
    return lines


def collisions(eras: dict[str, dict], no_era: dict[str, str]) -> dict[str, list[str]]:
    """Enumerated titles that fold onto one identity key."""
    grouped: dict[str, list[str]] = collections.defaultdict(list)
    for zone in sorted(list(eras) + list(no_era)):
        key = identity_key(zone)
        if key:
            grouped[key].append(zone)
    return {k: v for k, v in grouped.items() if len(v) > 1}


def join_survey(eras: dict[str, dict], no_era: dict[str, str]):
    """What the P1 era gate would actually be able to look up.

    Same instrument `zonelevels-transform.py` carries, and the same caveat: this reads the
    committed `ItemCatalog.json.gz`, so it is a SNAPSHOT stamped with the record count it
    was taken against, and `--check` deliberately does not cover it — coupling this gate to
    the item catalog would redden a refresh PR on a file it did not touch.

    The number worth reading is not just "how many `DropZones` spellings hit an era" but WHY
    the rest miss: a spelling landing on a page we read and refused is an era question, a
    spelling landing on no page at all is a catalog-data question, and they go to different
    slices."""
    if not ITEM_CATALOG.exists():
        return None
    catalog = json.loads(gzip.decompress(ITEM_CATALOG.read_bytes()).decode("utf-8"))
    items = catalog["Items"]

    # The fold index, with the EARLIEST-era rule `ZoneEras` applies, restated here only to
    # describe what the C# will answer for this report's table.
    def rank(zone: str) -> int:
        return LADDER.index(eras[zone]["Era"])

    folded: dict[str, str | None] = {}
    for key, zones in [(identity_key(z), z) for z in sorted(list(eras) + list(no_era))]:
        if not key:
            continue
        if key not in folded:
            folded[key] = zones
            continue
        prior = folded[key]
        if prior is None:
            continue
        if prior in eras and zones in eras:
            folded[key] = prior if rank(prior) <= rank(zones) else zones
        elif not (prior in no_era and zones in no_era and no_era[prior] == no_era[zones]):
            folded[key] = None

    dated = {k: z for k, z in folded.items() if z is not None and z in eras}
    absent = {k: z for k, z in folded.items() if z is not None and z in no_era}

    spellings: collections.Counter = collections.Counter()
    for item in items:
        for zone in (item.get("DropZones") or []):
            spellings[zone] += 1

    hit, refused, unknown = {}, {}, []
    for zone in spellings:
        key = identity_key(zone)
        if key in dated:
            hit[zone] = dated[key]
        elif key in absent:
            refused[zone] = absent[key]
        else:
            unknown.append(zone)

    by_era: collections.Counter = collections.Counter()
    for zone, title in hit.items():
        by_era[eras[title]["Era"]] += spellings[zone]

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
        "by_era": by_era,
        "top_hit": sorted(((spellings[z], z, eras[hit[z]]["Era"]) for z in hit), reverse=True),
    }


def pct(part: int, whole: int) -> int:
    return part * 100 // max(whole, 1)


def write_report(titles, eras, no_era, no_page, refusals, survey) -> None:
    hist = collections.Counter(row["Era"] for row in eras.values())
    no_banner = sorted(z for z, v in no_era.items() if not v)
    off_ladder = {z: r for z, r in refusals.items() if r[0] == "off-ladder"}
    two_eras = {z: r for z, r in refusals.items() if r[0] == "two-eras"}
    lines_at = banner_lines(titles)
    folds = collisions(eras, no_era)

    lines = [
        "# Zone eras report", "",
        "Written by `zone-eras-transform.py` from the COMMITTED zone wikitext cache.",
        "It fetches nothing. **Read the numbers here before trusting an era anywhere.**", "",
        "This slice (DRA-180 D1) ships the INSTRUMENT. No engine reads it yet, and nothing",
        "in this repo states what era the WORLD is at — that fact is curated, it starts",
        "ABSENT, and it is plan P2/P4's, not this file's.", "",
        "## Coverage", "",
        f"- Zone pages enumerated (`zone-titles.json`): **{len(titles)}**",
        f"- Eras shipped in `ZoneEras.json`: **{len(eras)}** "
        f"({pct(len(eras), len(titles))}% of pages)",
        f"- ABSENT — page carries no `{{{{... Era}}}}` banner: **{len(no_banner)}**",
        f"- ABSENT — banner REFUSED (era word not on the ladder): **{len(off_ladder)}**",
        f"- ABSENT — banner REFUSED (two different eras on one page): **{len(two_eras)}**",
        f"- ABSENT — title enumerated but no cached page: **{len(no_page)}**", "",
        "The ABSENT kinds ship in `NoEra` so a reader can tell them apart from a zone nobody",
        "has looked at — `ZoneEras.Lookup` has four outcomes for exactly that reason.", "",
        "## The era histogram", "",
        "| Era | Zones |", "|---|---:|",
        *[f"| {era} | {n} |"
          for era, n in sorted(hist.items(), key=lambda kv: LADDER.index(kv[0]))],
        f"| **Total** | **{len(eras)}** |", "",
        "Ordered by the ladder (`QuestEraLadder.Eras`), not by count: the order IS the fact a",
        "gate reads.", "",
        "## Distinct-count telltale (trap 73) — and why there is NO floor here", "",
        f"**{len(hist)} distinct eras across {len(eras)} pages.** On a per-zone fact that",
        "ratio would be the template alarm `zonelevels-report.md` holds to a two-thirds",
        "floor. **Here it is the expected shape and a floor would be wrong**: an era is a",
        "CATEGORY every zone of an expansion shares, so 57 pages saying Classic is 57 pages",
        "agreeing, not one template parsed 57 times. Applying a distinctness floor to a",
        "category would fail a true reading of real pages.", "",
        "So the guard is a different one, and `ZoneErasTests` holds it: **the mapping of each",
        "known era spelling to a named zone, and the exact ABSENT list** — not a row count a",
        "single wiki edit would redden. A parse that latched onto something shared would have",
        "to move a named zone to be wrong, which is a thing a human can check.", "",
        "## Pages with no era banner at all — the ABSENT list, by name", "",
        "**\"Absent means Classic\" is refused, and these names are why.** Stonebrunt",
        "Mountains, The Warrens and Kerra Island are the Paineel-adjacent set, in a corpus",
        "where exactly one page carries `{{Paineel Era}}`. Defaulting them to Classic would",
        "put a level-45 Warrens camp in a pre-Paineel world on the strength of a template",
        "nobody wrote.", "",
        *[f"- {z}" for z in no_banner], "",
        "## Era words NOT on the ladder — REFUSED, never guessed", "",
        "`quests-harvest.py` carries a curated rename map because QUEST pages drift",
        "(\"kunark Era\", \"EpicQuests Era\", \"Chardok Era\"). **The zone corpus does not**,",
        "so no map is mirrored into this transform. If a page drifts, it lands here with its",
        "word, and a human decides with the word in front of them.", "",
    ]
    if off_ladder:
        lines += ["| Zone | Word | Verbatim |", "|---|---|---|",
                  *[f"| {z} | {r[1]} | `{r[2]}` |" for z, r in sorted(off_ladder.items())]]
    else:
        lines += ["**None.** Every banner in the corpus spells its era exactly as",
                  "`QuestEraLadder.Eras` does. The arm is exercised by",
                  "`zone-eras-transform.py --selftest` and by `ZoneErasTests`, because a",
                  "refusal that has never fired on anything is a guard aimed at nothing",
                  "(trap 78)."]

    lines += ["", "## Pages carrying two DIFFERENT era banners — REFUSED, never picked", ""]
    if two_eras:
        lines += ["| Zone | Verbatim |", "|---|---|",
                  *[f"| {z} | `{r[2]}` |" for z, r in sorted(two_eras.items())]]
    else:
        lines += ["**None.** Two occurrences of the SAME era would be one claim stated twice",
                  "and are admitted; there are none of those either. Same trap-78 note as",
                  "above — the arm is exercised in the selftest and in `ZoneErasTests`."]

    lines += [
        "", "## Where the banner sits on the page", "",
        "Reported as evidence that the parse does NOT need to be positional. A rule written",
        "against a POSITION is a rule about the wrong fact (trap 66), so the whole page is",
        "searched and this table is a measurement rather than a constraint.", "",
        "| Line | Pages |", "|---:|---:|",
        *[f"| {n} | {c} |" for n, c in sorted(lines_at.items())], "",
        "## The identity fold, and the one collision in the corpus", "",
        "Lookup is exact title, then `ZoneMapFiles.IdentityKey`, and **nothing looser** — the",
        "`ZoneLevels` rule verbatim. Containment would hand \"Commonlands\" West Commonlands's",
        "answer and would match a zone name sitting inside free prose, and a wrong era is a",
        "claim a surface states as fact.", "",
    ]
    if folds:
        lines += ["| Identity key | Titles | What each page says |", "|---|---|---|"]
        for key, zones in sorted(folds.items()):
            says = ", ".join(
                f"{z} = {eras[z]['Era']}" if z in eras
                else f"{z} = ABSENT" for z in zones)
            lines += [f"| `{key}` | {len(zones)} | {says} |"]
        lines += [
            "",
            "**The folded key answers the EARLIER era.** Content that exists from Kunark on",
            "exists in a Chardok-Revamp world too, so the earlier era is the true answer to",
            "\"has the world reached this place yet\" — the later one would refuse a zone that",
            "is in the game. The item catalog's `DropZones` just says `Chardok`, which is why",
            "this collision has to be decided at all.", "",
            "The rule lives in `ZoneEras.cs`, not in this transform, which emits what each",
            "PAGE said (trap 4). Its other arm — a disagreement where either side is ABSENT",
            "answers NOTHING, because an absence is not an era and cannot be compared — has no",
            "instance in this corpus and is guarded against a fixture in `ZoneErasTests`.",
        ]
    else:
        lines += ["**No enumerated titles fold together.**"]

    lines += ["", "## The join — can an era actually be found for a drop zone?", ""]
    if survey is None:
        lines += ["`ItemCatalog.json.gz` is not present; the join was not measured."]
    else:
        lines += [
            f"Measured against the committed `ItemCatalog.json.gz` as it stands: "
            f"**{survey['records']}** records, **{survey['records_with_dropzone']}** of",
            "them carrying at least one `DropZones` entry. This is what P1's gate will",
            "have to read, so it is measured before the gate is built rather than after",
            "it disappoints somebody.", "",
            f"| Where a `DropZones` spelling lands | Spellings | of {survey['spellings']} "
            f"| Mentions | of {survey['mentions']} |",
            "|---|---:|---:|---:|---:|",
            f"| On a zone we have an era for | **{len(survey['hit'])}** "
            f"| {pct(len(survey['hit']), survey['spellings'])}% "
            f"| **{survey['hit_mentions']}** "
            f"| {pct(survey['hit_mentions'], survey['mentions'])}% |",
            f"| On a zone page whose banner is ABSENT | {len(survey['refused'])} "
            f"| {pct(len(survey['refused']), survey['spellings'])}% "
            f"| {survey['refused_mentions']} "
            f"| {pct(survey['refused_mentions'], survey['mentions'])}% |",
            f"| On no zone page we have read | {len(survey['unknown'])} "
            f"| {pct(len(survey['unknown']), survey['spellings'])}% "
            f"| {survey['unknown_mentions']} "
            f"| {pct(survey['unknown_mentions'], survey['mentions'])}% |", "",
            "**The middle and bottom rows are where the gate stands down**, per P1's",
            "per-arm stand-down: an unmapped zone leaves the era arm silent and lets the band",
            "and who rules run. Neither is a refusal.", "",
            "### Drop weight by era — what a world era would actually reach", "",
            "| Era | Mentions |", "|---|---:|",
            *[f"| {era} | {survey['by_era'][era]} |"
              for era in LADDER if survey["by_era"][era]], "",
            "### Heaviest spellings that DO land on an era", "",
            "| Mentions | `DropZones` spelling | Era |", "|---:|---|---|",
            *[f"| {n} | {z} | {e} |" for n, z, e in survey["top_hit"][:25]], "",
            "**This half is a snapshot.** A catalog refresh rebuilds `ItemCatalog.json.gz`;",
            "re-run this transform (no `--check`) afterwards to re-take it. `--check`",
            "deliberately does not cover the report, so a refresh PR is not reddened by a",
            "file it did not touch.",
        ]
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")


# ----------------------------------------------------------------- the selftest

SELFTEST = [
    # (name, wikitext, expected decide() tuple)
    ("a clean banner on line 1",
     "{{Classic Era}}\nBefallen is a ruined keep.", ("dated", "Classic", "{{Classic Era}}")),
    ("a banner that is not on line 1 (Mines of Nurga's real shape)",
     "{{expand}}\n{{Kunark Era}}\n", ("dated", "Kunark", "{{Kunark Era}}")),
    ("a banner deep in the page — the match is not positional (trap 66)",
     "a\nb\nc\nd\ne\n{{Velious Era}}\n", ("dated", "Velious", "{{Velious Era}}")),
    ("a two-word era",
     "{{Chardok Revamp Era}}{{revamped}}",
     ("dated", "Chardok Revamp", "{{Chardok Revamp Era}}")),
    ("lowercase is canonicalised to the ladder's spelling",
     "{{velious era}}\n", ("dated", "Velious", "{{velious era}}")),
    ("inner whitespace is squeezed",
     "{{ Chardok  Revamp  Era }}\n",
     ("dated", "Chardok Revamp", "{{ Chardok  Revamp  Era }}")),
    ("the SAME era twice is one claim stated twice",
     "{{Classic Era}}\nprose\n{{Classic Era}}\n", ("dated", "Classic", "{{Classic Era}}")),
    ("two DIFFERENT eras are refused, not picked between",
     "{{Kunark Era}}\nprose\n{{Velious Era}}\n",
     ("two-eras", "", "{{Kunark Era}} {{Velious Era}}")),
    ("an era word that is not on the ladder is refused by name",
     "{{Prophecy Era}}\n", ("off-ladder", "Prophecy", "{{Prophecy Era}}")),
    ("`{{Era|Kunark}}` — a real DropZones value — is not a banner",
     "Timorous Deep {{Era|Kunark}}\n", ("none", "", "")),
    ("`{{VeliousGray| Skyshrine }}` is not a banner",
     "{{VeliousGray| Skyshrine }}\n", ("none", "", "")),
    ("`{{Era}}` with no word is not a banner", "{{Era}}\n", ("none", "", "")),
    ("an unrelated template is not a banner", "{{expand}}\n{{revamped}}\n", ("none", "", "")),
    ("a page with no templates at all", "Grobb is the home of the trolls.\n", ("none", "", "")),
    ("an empty page", "", ("none", "", "")),
]


def selftest() -> int:
    """Exercise the arms the corpus cannot reach. See the module docstring on why."""
    failures: list[str] = []

    # Trap 78 from the other side: an empty admitted set refuses everything and reports a
    # clean corpus. Assert the list is really populated before believing any row below.
    if len(LADDER) != 9:
        failures.append(f"LADDER has {len(LADDER)} entries, expected the ladder's 9")
    if canonical("Classic") != "Classic" or canonical("Prophecy") is not None:
        failures.append("canonical() does not separate a ladder word from a stranger")

    for name, wikitext, expected in SELFTEST:
        got = decide(read_eras(wikitext))
        if got != expected:
            failures.append(f"{name}: expected {expected}, got {got}")

    for line in failures:
        print(f"FAIL  {line}", file=sys.stderr)
    if failures:
        print(f"{len(failures)} of {len(SELFTEST) + 2} selftest checks failed.",
              file=sys.stderr)
        return 1
    refusals = sum(1 for _, _, e in SELFTEST if e[0] in ("off-ladder", "two-eras"))
    print(f"selftest: {len(SELFTEST) + 2} checks green "
          f"({refusals} of them the refusal arms the corpus cannot reach).")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Zone eras from the committed cache.")
    parser.add_argument("--check", action="store_true",
                        help="write nothing; exit 1 if the committed file differs")
    parser.add_argument("--selftest", action="store_true",
                        help="write nothing; exercise the parser's refusal arms")
    args = parser.parse_args()

    if args.selftest:
        return selftest()

    titles = json.loads(TITLES.read_text(encoding="utf-8"))
    eras, no_era, no_page, refusals = build(titles)
    data = render(eras, no_era)
    current = OUT.read_text(encoding="utf-8") if OUT.exists() else None

    if args.check:
        if data != current:
            print(f"{OUT.name} differs from what this produces "
                  f"({len(current or '')} bytes on disk, {len(data)} generated).",
                  file=sys.stderr)
            return 1
        print(f"{OUT.name} is already what this produces "
              f"({len(eras)} eras, {len(data)} bytes).")
        return 0

    if data == current:
        print(f"{OUT.name} unchanged ({len(eras)} eras) — left alone.")
    else:
        OUT.write_text(data, encoding="utf-8")
        print(f"wrote {OUT.name}: {len(eras)} eras, "
              f"{len(no_era)} zones ABSENT, of {len(titles)} zone pages.")
    write_report(titles, eras, no_era, no_page, refusals, join_survey(eras, no_era))
    print(f"Report: {REPORT.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
