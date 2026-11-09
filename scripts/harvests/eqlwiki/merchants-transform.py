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

Two more refusals sit on top of that, both exact and both reported under their own reason
(DRA-4195). A folded list item whose whole text is on `PLACEHOLDERS` ("Merchant Name", the
unfilled zone template) is not a vendor. A list item whose current `== heading ==` is on
`QUEST_HEADINGS` is a walkthrough step that happens to mention a merchant, not a map-key
entry. Both lists are curated whole strings read off the committed cache, never a
substring. The survey that picked the headings is the comment on `QUEST_HEADINGS`.

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

    python scripts/harvests/eqlwiki/merchants-transform.py [--check] [--selftest]

`--check` writes nothing and exits 1 if the committed `ZoneMerchants.json` is not what this
produces. The write side compares the same way and leaves the file alone when nothing moved.
`--selftest` writes nothing either and fires the two DRA-4195 refusal arms over synthetic
wikitext, plus a map-key line that must still be admitted. Both run in `check.ps1` and CI.
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
# A wiki heading. Level is the number of `=` signs; only level 2 (`== … ==`) is the section
# a quest-heading refusal reads. `=== Merchants ===` is a subsection and must not move the
# section, or a real map key filed under a subheading would be refused for the heading above it.
HEADING_RX = re.compile(r"^(={2,6})\s*(.*?)\s*\1\s*$")

# Whole-string, case-insensitive, never containment. The unfilled zone template's merchant
# row. The committed cache has exactly one: The Nektropos Undercroft's `* [[Merchant Name]]`,
# which folds to "Merchant Name". A longer line that merely contains those words is a
# sentence and is not this.
PLACEHOLDERS = frozenset({"merchant name"})

# Exact `== heading ==` text, case-insensitive, never a substring. Read off the committed
# zone cache for DRA-4195, not guessed:
#   * "Quick Access Tips" is the heading over Nektropos Castle's entry checklist. The
#     section prose says "full walkthrough" and the list is a checkbox-list; the heading
#     itself does not contain "quest" or "walkthrough". A substring guess on those words
#     refuses zero shipped lines and leaves both walkthrough steps in. Survey of every
#     merchant list item: the only two under this heading are those two steps.
#   * The other three are the cache's level-2 headings that name quests. None of them has
#     a merchant list item today, so they remove nothing shipped; they are here so a later
#     page that puts a vendor line under "Newbie Quests" is refused for the same reason,
#     and so `--selftest` can fire an arm the corpus does not reach (trap 78).
QUEST_HEADINGS = frozenset({
    "quick access tips",
    "newbie quests",
    "inhabitants and quests",
    "plane of sky class quests",
})

REASON_PROSE = "prose"
REASON_PLACEHOLDER = "template placeholder"
REASON_QUEST = "under a quest heading"

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


def heading_of(line: str) -> tuple[int, str] | None:
    """(level, text) when `line` is a wiki heading, else None."""
    m = HEADING_RX.match(line)
    if m is None:
        return None
    return len(m.group(1)), m.group(2).strip()


def read_zone(wikitext: str) -> tuple[list[str], list[tuple[str, str]]]:
    """(admitted lines, refused (line, reason) pairs) for one page.

    The refusals are collected rather than counted so the report can show them: the whole
    argument for the strict admit rule is that what it turns away is visible. A heading is
    tracked even when it does not mention a merchant, because the section it opens is what
    a later list item is under. A heading that itself mentions a merchant stays a prose
    refusal — that is what `=== Merchants ===` already was, and skipping it would hide a
    line the report used to name.
    """
    admitted: list[str] = []
    refused: list[tuple[str, str]] = []
    heading = ""
    for raw in wikitext.splitlines():
        line = raw.strip()
        if not line:
            continue
        found = heading_of(line)
        if found is not None:
            level, text = found
            if level == 2:
                heading = text
            if MERCHANT_RX.search(line):
                folded = fold(line)
                if folded:
                    refused.append((folded, REASON_PROSE))
            continue
        if not MERCHANT_RX.search(line):
            continue
        m = HTML_LI_RX.match(line) or LIST_RX.match(line)
        if m is None:
            folded = fold(line)
            if folded:
                refused.append((folded, REASON_PROSE))
            continue
        folded = fold(m.group(1))
        if not folded:
            refused.append((line, REASON_PROSE))
            continue
        if folded.casefold() in PLACEHOLDERS:
            refused.append((folded, REASON_PLACEHOLDER))
            continue
        if heading.casefold() in QUEST_HEADINGS:
            refused.append((folded, REASON_QUEST))
            continue
        admitted.append(folded)
    return admitted, refused


def build(titles: list[str]):
    """Returns (zones, no_lines, no_page, refusals).

    `refusals` is reason -> zone -> lines, in page order. `no_lines` ships beside the
    answers for `ZoneLevelBands.json`'s reason: "the page has no merchant in its map key"
    and "we have never read a page for this zone" are different answers, and only one of
    them means a surface should say nothing at all.
    """
    zones: dict[str, list[str]] = {}
    no_lines: list[str] = []
    no_page: list[str] = []
    refusals: dict[str, dict[str, list[str]]] = {
        REASON_PROSE: {},
        REASON_PLACEHOLDER: {},
        REASON_QUEST: {},
    }
    for title in titles:
        path = cache_path(title)
        if not path.exists():
            no_page.append(title)
            continue
        admitted, refused = read_zone(path.read_text(encoding="utf-8", errors="replace"))
        for line, reason in refused:
            refusals[reason].setdefault(title, []).append(line)
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


def _count(by_zone: dict[str, list[str]]) -> int:
    return sum(len(v) for v in by_zone.values())


def _bullets(by_zone: dict[str, list[str]]) -> list[str]:
    if not by_zone:
        return ["- *(nothing refused)*"]
    out: list[str] = []
    for zone in sorted(by_zone):
        for line in by_zone[zone]:
            out.append(f"- **{zone}** — {line}")
    return out


def report(zones, no_lines, no_page, refusals) -> str:
    lines_total = sum(len(v) for v in zones.values())
    distinct = len({line for v in zones.values() for line in v})
    prose = refusals[REASON_PROSE]
    placeholders = refusals[REASON_PLACEHOLDER]
    quests = refusals[REASON_QUEST]
    refused_zones = set(prose) | set(placeholders) | set(quests)
    refused_total = _count(prose) + _count(placeholders) + _count(quests)

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
               f"(in {len(refused_zones)} zones) |")
    out.append(f"| refused as prose | {_count(prose)} |")
    out.append(f"| refused as a template placeholder | {_count(placeholders)} |")
    out.append(f"| refused as under a quest heading | {_count(quests)} |")
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
    out.append("spellings the map key has on these pages — AND contains `merchant`, AND its whole")
    out.append("text is not a template placeholder, AND the `== heading ==` it sits under is not a")
    out.append("quest heading. Prose that mentions a merchant is refused. Some of what is below is")
    out.append("a real merchant we could have taken — that is the trade: *\"an item of the map key\"*")
    out.append("is a rule the page's structure defines, where *\"a sentence mentioning a merchant\"*")
    out.append("has no edge and would pull in quest prose and lore.")
    out.append("")
    out.extend(_bullets(prose))
    out.append("")
    out.append("## Refused: template placeholder")
    out.append("")
    out.append("A folded list item whose whole text is on the curated placeholder list,")
    out.append("case-insensitive and never by containment. The seed is the unfilled zone")
    out.append("template's `Merchant Name`. Each one is named here, on its own, so a later keyword")
    out.append("cannot surface a line the page never filled in.")
    out.append("")
    out.extend(_bullets(placeholders))
    out.append("")
    out.append("## Refused: under a quest heading")
    out.append("")
    out.append("A list item whose current `== heading ==` is on the curated quest-heading list")
    out.append("(exact, case-insensitive). These are walkthrough steps that mention a merchant,")
    out.append("not a map-key entry. The list was read off the committed cache: `Quick Access Tips`")
    out.append("is the heading over Nektropos Castle's entry checklist, and the three headings that")
    out.append("name quests (`Newbie Quests`, `Inhabitants and Quests`, `Plane of Sky Class Quests`)")
    out.append("have no merchant list item in this cache.")
    out.append("")
    out.extend(_bullets(quests))
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


def _only(admitted: list[str], refused: list[tuple[str, str]], reason: str) -> list[str]:
    return [line for line, why in refused if why == reason]


def selftest() -> int:
    """Fire the refusal arms a corpus count cannot see, and one map-key line that must stay.

    The placeholder and the Nektropos Castle steps are in the committed cache, so `--check`
    sees them. `Newbie Quests` has no merchant list item in that cache, and a longer line
    that merely contains "Merchant Name" is not a placeholder — both stay green if the rule
    is aimed at nothing or aimed too wide (trap 78, trap 34).
    """
    failures: list[str] = []

    if "merchant name" not in PLACEHOLDERS or len(PLACEHOLDERS) < 1:
        failures.append("PLACEHOLDERS is empty or lost its seed (a detector aimed at nothing)")
    if "newbie quests" not in QUEST_HEADINGS or "quick access tips" not in QUEST_HEADINGS:
        failures.append("QUEST_HEADINGS lost a heading the cache survey named")

    cases: list[tuple[str, str, list[str], list[tuple[str, str]]]] = [
        (
            "Merchant Name under Services is a template placeholder",
            "== Services ==\n=== Merchants ===\n* [[Merchant Name]]\n",
            [],
            [("=== Merchants ===", REASON_PROSE), ("Merchant Name", REASON_PLACEHOLDER)],
        ),
        (
            "a lowercase placeholder is the same line",
            "== Services ==\n* merchant name\n",
            [],
            [("merchant name", REASON_PLACEHOLDER)],
        ),
        (
            "a list item under Newbie Quests is under a quest heading",
            "== Newbie Quests ==\n* Get the sword from the merchant in town.\n",
            [],
            [("Get the sword from the merchant in town.", REASON_QUEST)],
        ),
        (
            "Quick Access Tips is the heading over the castle checklist",
            "== Quick Access Tips ==\n* Turn both merchant items in to Retainer Ozval.\n",
            [],
            [("Turn both merchant items in to Retainer Ozval.", REASON_QUEST)],
        ),
        (
            "a map-key item is still admitted",
            "== Map ==\n* 1. Merchant selling Gems\n",
            ["Merchant selling Gems"],
            [],
        ),
        (
            "a longer line that contains the placeholder words is not the placeholder",
            "== Map ==\n* Merchant Name of the house sells ore\n",
            ["Merchant Name of the house sells ore"],
            [],
        ),
        (
            "a heading that merely contains a quest heading's words is not that heading",
            "== Newbie Quests and More ==\n* Merchant selling Gems\n",
            ["Merchant selling Gems"],
            [],
        ),
        (
            "a level-3 quest heading does not override the level-2 Map section",
            "== Map ==\n=== Newbie Quests ===\n* Merchant selling Gems\n",
            ["Merchant selling Gems"],
            [],
        ),
        (
            "prose that mentions a merchant is still prose",
            "Note that there is a merchant who sells Ore.\n",
            [],
            [("Note that there is a merchant who sells Ore.", REASON_PROSE)],
        ),
    ]

    for name, wikitext, want_admitted, want_refused in cases:
        got_admitted, got_refused = read_zone(wikitext)
        if got_admitted != want_admitted or got_refused != want_refused:
            failures.append(
                f"{name}: expected admitted {want_admitted} refused {want_refused}, "
                f"got admitted {got_admitted} refused {got_refused}")

    # The two negatives must be the reason they claim, not a prose miss that happens to
    # drop the same line. Removing the placeholder set or the heading set has to redden
    # these specifically.
    placeholder_page = "== Services ==\n* [[Merchant Name]]\n"
    admitted, refused = read_zone(placeholder_page)
    if admitted or _only(admitted, refused, REASON_PLACEHOLDER) != ["Merchant Name"]:
        failures.append("placeholder negative did not refuse Merchant Name by that reason")
    quest_page = "== Inhabitants and Quests ==\n* Speak to the merchant.\n"
    admitted, refused = read_zone(quest_page)
    if admitted or _only(admitted, refused, REASON_QUEST) != ["Speak to the merchant."]:
        failures.append("quest-heading negative did not refuse by that reason")

    for line in failures:
        print(f"FAIL  {line}", file=sys.stderr)
    if failures:
        print(f"{len(failures)} selftest checks failed.", file=sys.stderr)
        return 1
    print(f"selftest: {len(cases) + 2} checks green "
          f"(placeholder and quest-heading refusals, map-key line still admitted).")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true",
                    help="write nothing; exit 1 if the committed files are not what this produces")
    ap.add_argument("--selftest", action="store_true",
                    help="write nothing; fire the placeholder and quest-heading refusals")
    args = ap.parse_args()

    if args.selftest:
        return selftest()

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
