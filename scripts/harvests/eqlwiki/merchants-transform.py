#!/usr/bin/env python3
"""Turn the cached eqlwiki ZONE wikitext into shipped MERCHANT LINES (DRA-149 D4, plan P5).

WHAT THIS IS
------------
The Founder's FAIL item 3 has two halves. The first — *"zones/creatures where gems drop"* —
landed in D3 as `Core/TradeskillMaterials.cs` over the item catalog's `Recipes` column. This
is the second: *"OR named vendors + where they are when shopping vendors."*

The data for it is not on the item pages. `StatsText` mentions a vendor on 3 of 11,196 of
them. It is on the ZONE pages, in the map key the wiki writes under each map image:

    * 1. Merchant selling Gems
    * 8. Everhot Forge - Merchants selling Blunt and Sharp Weapons, Small Chain and Plate
         Armor, ... ([[Bndainy Everhot]]), Jewelry Metal and Rare Gems, Forge Outside

`zones-harvest.py` already caches all 118 of those pages for the adjacency graph, and the
cache is COMMITTED. So this transform **fetches nothing** — consequence-list item 7 (our
request rate at eqlwiki) is not merely unchanged but untouched, exactly as
`zonelevels-transform.py` and `guides-transform.py` are. Its whole input is
`cache/zone-*.wikitext` plus `zone-titles.json`, and its output is
`src/EQBuddy.Core/Data/ZoneMerchants.json`, read by `Core/ZoneMerchants.cs`.

IT TRANSCRIBES. IT DOES NOT SUMMARISE, AND IT DOES NOT NAME THE VENDOR ITSELF
-----------------------------------------------------------------------------
A kept line is the page's own sentence, and the only edits are the ones that turn wikitext
back into what a reader sees: `[[A|B]]` and `[[A]]` fold to their display text, `'''` and
`''` come off, and the map key's own numbering (`1. `) is dropped because it refers to a
picture EQBuddy does not ship. Nothing is re-worded, nothing is shortened, and nothing is
assembled from parts. That is the standing rule for eqlwiki content and it is also the only
way this answers the Founder at the depth the wiki actually holds: *"Hut with Merchant
Darfumpel Zirubbel who sells Gems nearby"* is a better answer than any sentence we could
generate from a parse of it, and it is checkable against the page.

**In particular this does NOT extract the vendor's NAME into a field.** It is tempting —
`[[Bndainy Everhot]]` looks exactly like an NPC — but the links inside these lines are not
all NPCs: `[[Cleric]] Guild`, `[[Rogue]] Guild Members`, `[[Tumpy Irontoe]]` (an NPC),
`[[Kafia Ratsbone]]` (an NPC) all appear in the same list on one page. A rule that called
all of them vendors would print "Cleric" as a merchant's name, and a rule that tried to tell
them apart would be guessing about the wiki's own link targets. The name survives IN the
sentence, where the page put it, and a surface shows the sentence.

THE ADMIT RULE IS STRICT, AND THE REPORT IS WHERE THE REST GOES
----------------------------------------------------------------
A line is admitted when BOTH hold:

  * it is a LIST ITEM — the map key's own shape; and
  * its text contains `merchant`, case-insensitively.

**A list item has THREE spellings on these pages, and READING THE REFUSAL LIST is what found
the other two.** The first pass admitted only `*`, refused 86 lines, and most of them turned
out to be map keys written differently: Misty Thicket and Everfrost Peaks number theirs with
`#`, and Halas, Oggok, Runnyeye and Timorous Deep write theirs as raw HTML `<li>` (three of
those four with the previous item's `</li>` on the front of the line, so an anchored `^<li>`
rule missed Oggok's entire fifteen-entry key). All three spellings are the page saying "this
is an entry in the key", so all three are admitted. The rule is still structural — it is
about what the page marked up as a list, not about which sentences look useful — and this is
the report earning its place: a count of refusals would have said 86 and told nobody that
five zones were missing.

Everything else is refused and listed in `merchants-report.md` with its zone, including the
near misses that make the rule worth stating: Kaladim's *"Note that there is a merchant who
sells Ore located at approximately 750, 200 on this map."* is PROSE, so it does not land. It
is a real merchant and we could have taken it. It is refused because "a sentence somewhere on
the page that mentions a merchant" is a rule with no edge — it would also admit quest prose,
lore and patch notes (Freeport's city history and Crushbone's hunting advice both mention
merchants and neither points at one) — while "an item of the map key" is a rule the page's own
structure defines, and a refusal is visible in a report where a wrong admission is not.

The report also carries the **distinct-count telltale** (trap 73). If 41 zones carried 9
distinct merchant lines between them, the data would be a template rather than a per-zone
fact, and you would want to know that before believing any of it.

WHAT IT DELIBERATELY DOES NOT DECIDE
-------------------------------------
Which profession a line is about. That join is a CURATED per-profession keyword list in
`Core/ZoneMerchants.cs`, hand-written, eight rows, beside the curated
`Core/Tradeskills.cs` it serves — the same rule every other curated catalog in this repo
keeps. It lives in the C# rather than here for the reason `ZoneLevels` does not know about
the band gate: this file publishes what the page said, and the judgement lives with the
reader.

BYTE-REPRODUCIBLE, AND NO CONTAINER TO ARGUE ABOUT
---------------------------------------------------
Plain JSON, sorted zone keys, fixed key order, no clock and no locale: the same cache
produces the same bytes on any machine. Unlike `HarvestedGuides.json.gz` there is no gzip
container here, so the trap-74 failure — a gate reddening on which zlib build ran it —
cannot arise, and `--check` can compare the file itself.

    python scripts/harvests/eqlwiki/merchants-transform.py [--check]

`--check` writes nothing and exits 1 if the committed `ZoneMerchants.json` is not what this
produces. The write side compares the same way and leaves the file alone when nothing moved.
"""

from __future__ import annotations

import argparse
import collections
import json
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CACHE = HERE / "cache"
TITLES = HERE / "zone-titles.json"
REPORT = HERE / "merchants-report.md"
DATA = ROOT / "src" / "EQBuddy.Core" / "Data"
OUT = DATA / "ZoneMerchants.json"

SOURCE = ("eqlwiki zone pages, map-key list lines naming a merchant "
          "(scripts/harvests/eqlwiki/merchants-transform.py; fetches nothing)")

# A list ITEM, in the three spellings these pages use. Wikitext bullets (`*`, nested `**`),
# wikitext numbering (`#`, which Misty Thicket and Everfrost Peaks use for the same map key),
# and raw HTML (`<li>`, which is how Halas, Oggok, Runnyeye and Timorous Deep write theirs).
#
# The HTML arm allows the PREVIOUS item's `</li>` and an opening `<ol>`/`<ul>` in front of the
# `<li>`, because that is how three of those four pages are actually typed:
# `</li><li>Merchant selling Large Shields`. Without it Oggok's entire fifteen-entry map key
# reads as prose — which is what the first pass did, and what reading the refusal list caught.
LIST_RX = re.compile(r"^(?:\*+|#+)\s*(.*)$")
HTML_LI_RX = re.compile(r"^(?:</li>|<ol>|<ul>|\s)*<li>\s*(.*?)\s*(?:</li>\s*)?$", re.IGNORECASE)
MERCHANT_RX = re.compile(r"merchant", re.IGNORECASE)

# `[[Target|Shown]]` and `[[Target]]` — folded to what a reader sees, which is also what a
# player would type into a search box.
LINK_RX = re.compile(r"\[\[([^\[\]|]+)(?:\|([^\[\]]*))?\]\]")
# Wiki emphasis. Longest first, so `'''` is not read as `''` plus a stray quote.
EMPHASIS_RX = re.compile(r"'{2,5}")
# The map key's own numbering, which points at a picture we do not ship.
MAPKEY_RX = re.compile(r"^\d+\s*\.\s*")
# `<br>`, `<br/>`, and the handful of other bare tags these lines carry.
TAG_RX = re.compile(r"</?[A-Za-z][^>]*>")
SPACE_RX = re.compile(r"\s+")


def cache_path(title: str) -> pathlib.Path:
    """The filename `zones-harvest.py` cached this title under. Same sanitisation, so the
    zone key here is the WIKI TITLE — the same key `ZoneLevelBands.json` uses."""
    safe = re.sub(r"[^A-Za-z0-9._-]", "_", title)
    return CACHE / f"zone-{safe}.wikitext"


def fold(text: str) -> str:
    """Wikitext to what a reader sees. Every rule here is a RENDERING rule — nothing is
    dropped for being uninteresting, and no word is replaced by a different word."""
    text = LINK_RX.sub(lambda m: (m.group(2) or m.group(1)).strip(), text)
    text = EMPHASIS_RX.sub("", text)
    text = TAG_RX.sub(" ", text)
    text = MAPKEY_RX.sub("", text.strip())
    return SPACE_RX.sub(" ", text).strip()


def read_zone(wikitext: str) -> tuple[list[str], list[str]]:
    """(admitted lines, refused lines that mention a merchant) for one page.

    The refusals are collected rather than counted so the report can show them: the whole
    argument for the strict admit rule is that what it turns away is visible.
    """
    admitted: list[str] = []
    refused: list[str] = []
    for raw in wikitext.splitlines():
        line = raw.strip()
        if not MERCHANT_RX.search(line):
            continue
        m = HTML_LI_RX.match(line) or LIST_RX.match(line)
        if m is None:
            folded = fold(line)
            if folded:
                refused.append(folded)
            continue
        folded = fold(m.group(1))
        if folded:
            admitted.append(folded)
        else:
            refused.append(line)
    return admitted, refused


def build(titles: list[str]):
    """Returns (zones, no_lines, no_page, refusals).

    `no_lines` ships beside the answers for `ZoneLevelBands.json`'s reason: "the page has no
    merchant in its map key" and "we have never read a page for this zone" are different
    answers, and only one of them means a surface should say nothing at all.
    """
    zones: dict[str, list[str]] = {}
    no_lines: list[str] = []
    no_page: list[str] = []
    refusals: dict[str, list[str]] = {}
    for title in titles:
        path = cache_path(title)
        if not path.exists():
            no_page.append(title)
            continue
        admitted, refused = read_zone(path.read_text(encoding="utf-8", errors="replace"))
        if refused:
            refusals[title] = refused
        if admitted:
            zones[title] = admitted
        else:
            no_lines.append(title)
    return zones, no_lines, no_page, refusals


def payload(zones, no_lines) -> dict:
    return {
        "Source": SOURCE,
        "Zones": {z: zones[z] for z in sorted(zones)},
        "NoLines": sorted(no_lines),
    }


def rendered(doc: dict) -> str:
    return json.dumps(doc, indent=1, ensure_ascii=False, sort_keys=False) + "\n"


def report(zones, no_lines, no_page, refusals) -> str:
    lines_total = sum(len(v) for v in zones.values())
    distinct = len({line for v in zones.values() for line in v})
    refused_total = sum(len(v) for v in refusals.values())

    out: list[str] = []
    out.append("# eqlwiki zone MERCHANT lines — what shipped and what was refused")
    out.append("")
    out.append("Generated by `scripts/harvests/eqlwiki/merchants-transform.py` from the COMMITTED")
    out.append("zone wikitext cache. **It fetches nothing.** Output:")
    out.append("`src/EQBuddy.Core/Data/ZoneMerchants.json`, read by `Core/ZoneMerchants.cs`.")
    out.append("")
    out.append("| | |")
    out.append("|---|---|")
    out.append(f"| zone pages read | {len(zones) + len(no_lines)} |")
    out.append(f"| zones with at least one admitted line | **{len(zones)}** |")
    out.append(f"| zones whose map key names no merchant | {len(no_lines)} |")
    out.append(f"| admitted merchant lines | **{lines_total}** |")
    out.append(f"| distinct admitted lines | {distinct} |")
    out.append(f"| lines mentioning a merchant that were REFUSED | {refused_total} "
               f"(in {len(refusals)} zones) |")
    if no_page:
        out.append(f"| zone titles with no cached page | {len(no_page)} |")
    out.append("")
    out.append("**The distinct-count telltale** (trap 73): "
               f"{distinct} distinct lines across {lines_total} admitted. A ratio near 1 is what")
    out.append("per-zone transcription looks like; a low one would mean the pages share boilerplate")
    out.append("and the data is a template rather than a fact about each zone.")
    out.append("")
    out.append("## What was refused, and why the rule is worth its cost")
    out.append("")
    out.append("A line is admitted only when it is a LIST ITEM — `*`, `#`, or raw `<li>`, the three")
    out.append("spellings the map key has on these pages — AND contains `merchant`. Prose that")
    out.append("mentions a merchant is refused. Some of what is below is a real merchant we could")
    out.append("have taken — that is the trade: *\"an item of the map key\"* is a rule the page's")
    out.append("structure defines, where *\"a sentence mentioning a merchant\"* has no edge and would")
    out.append("pull in quest prose and lore.")
    out.append("")
    if refusals:
        for zone in sorted(refusals):
            for line in refusals[zone]:
                out.append(f"- **{zone}** — {line}")
    else:
        out.append("- *(nothing refused)*")
    out.append("")
    out.append("## Zones whose map key names no merchant")
    out.append("")
    out.append("These shipped too, in `NoLines`. A zone in this list is one EQBuddy has READ and")
    out.append("that says nothing about merchants; a zone in neither list is one it has never read,")
    out.append("and those are different sentences to a player.")
    out.append("")
    out.append(", ".join(f"`{z}`" for z in sorted(no_lines)) if no_lines else "*(none)*")
    out.append("")
    out.append("## Admitted lines, per zone")
    out.append("")
    for zone in sorted(zones):
        out.append(f"### {zone}")
        out.append("")
        for line in zones[zone]:
            out.append(f"- {line}")
        out.append("")
    return "\n".join(out) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true",
                    help="write nothing; exit 1 if the committed files are not what this produces")
    args = ap.parse_args()

    titles = json.loads(TITLES.read_text(encoding="utf-8"))
    zones, no_lines, no_page, refusals = build(titles)
    doc = rendered(payload(zones, no_lines))
    rep = report(zones, no_lines, no_page, refusals)

    if args.check:
        bad = False
        for path, want in ((OUT, doc), (REPORT, rep)):
            have = path.read_text(encoding="utf-8") if path.exists() else None
            if have != want:
                print(f"STALE: {path.relative_to(ROOT)} is not what this transform produces",
                      file=sys.stderr)
                bad = True
        if bad:
            print("run: python scripts/harvests/eqlwiki/merchants-transform.py", file=sys.stderr)
            return 1
        print(f"OK: {len(zones)} zones, {sum(len(v) for v in zones.values())} merchant lines")
        return 0

    for path, want in ((OUT, doc), (REPORT, rep)):
        if path.exists() and path.read_text(encoding="utf-8") == want:
            continue
        path.write_text(want, encoding="utf-8", newline="\n")
        print(f"wrote {path.relative_to(ROOT)}")
    counts = collections.Counter(len(v) for v in zones.values())
    print(f"{len(zones)} zones with merchants, {sum(len(v) for v in zones.values())} lines; "
          f"busiest page has {max(counts) if counts else 0}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
