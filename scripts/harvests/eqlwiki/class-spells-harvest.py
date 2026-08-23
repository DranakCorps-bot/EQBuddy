#!/usr/bin/env python3
"""Harvest per-class spell levels from eqlwiki's CLASS pages, and diff them
against the shipped SpellLevels.json.

PR 0 of Fable 5's "next-level spells by class" plan (FABLE.md, 2026-08-23).
**This script changes no catalog.** It fetches, parses, compares, and writes
class-spells-report.md. Acting on the difference is PR 1, deliberately, because
the difference is ~500 rows and that is a human review rather than a diff nobody
reads.

WHY THE CLASS PAGES AND NOT THE SPELL PAGES
-------------------------------------------
SpellLevels.json is harvested from individual SPELL pages (spells-harvest.py ->
spell-levels-promote.py), whose {{Spellpagesmart}} carries a per-class level.
David asked (2026-08-23) for the next-level list to come from "their class pages
on EQL Wiki", and checking that turned out to matter: the two sources disagree.

  Druid, level 34
    class page ==Level 34==     5 spells   Endure Magic, Healing Water,
                                           Regeneration, Strength of Stone,
                                           Zephyr: North Karana
    our catalog (spell pages)  10 spells   the above minus Healing Water,
                                           plus six Velious-era ports

The class pages are Legends-CURATED — each row carries an `era=` field the spell
pages lack, and the Level 34 port is Zephyr: North Karana, the Legends one. The
spell pages are wider than the game. David's ruling: the class page wins; a
spell-page row is kept only where the class page has no section for that level,
and is flagged as derived.

WHAT IT PARSES
--------------
Each class page is a run of `==Level N==` headings; under each, zero or more
`{{RadSpellRow2 |name=... |era=... }}` blocks. Name and era are the only fields
that travel; description, mana, school and the rest stay on the wiki, which is
the same rule the spell harvest already follows.

WHAT IT FOUND, WHICH IS BIGGER THAN THE DISAGREEMENT
----------------------------------------------------
`spellname` on a spell page is a COPY-PASTE ARTEFACT, not a canonical name.
`Circle of Butcherblock` declares `spellname = Ring of South Ro` while describing
Butcherblock; `Healing Water` declares `spellname = Greater Healing` while
describing a 425-point heal. Our existing spell harvest keys on that field and
then de-duplicates, so pages carrying a wrong `spellname` are recorded under
another spell's name and dropped — which is why `Healing Water`,
`Circle of Butcherblock` and `Invisibility versus Animals` are ABSENT from the
shipped catalog while their pages plainly say Druid 34, Druid 25, Druid 8.

So this is not only "two sources disagree". **The catalog we ship is missing real
spells**, on the surface that already shows a ding list today. The `classes` list
on a spell page ("* [[Druid]] - Level 34") is the reliable field; `spellname` is
not.

Usage:
    python class-spells-harvest.py [--refetch]
"""
import json
import re
import sys
import time
import urllib.parse
import urllib.request
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
CACHE = HERE / "cache"
API = "https://eqlwiki.com/api.php"
UA = "EQBuddy-harvester/1.0 (contact: david.edwards08@gmail.com; polite ~1 req/sec)"
CATALOG = HERE.parents[2] / "src" / "EQBuddy.Core" / "Data" / "SpellLevels.json"
REPORT = HERE / "class-spells-report.md"
# The parsed class pages, for spell-levels-promote.py to merge (PR 1). This file is
# the harvest's DATA output where the report is its prose one — written here rather
# than re-parsed there so there is exactly one parser for the class pages, which is
# the same rule spells-harvest/spell-levels-promote already follow.
ROWS = HERE / "class-spells.json"

# The app's spelling wins where the wiki differs — the same fold the spell
# promote already applies (QuestClassFilter.Classes).
CLASS_FOLD = {"Shadowknight": "Shadow Knight"}

# Sixteen classes; the wiki titles them exactly as the app names them apart from
# the fold above. Warrior/Monk/Berserker have no spell tables — they are listed
# anyway so their ABSENCE is a measured result rather than an omission.
CLASSES = [
    "Bard", "Beastlord", "Berserker", "Cleric", "Druid", "Enchanter",
    "Magician", "Monk", "Necromancer", "Paladin", "Ranger", "Rogue",
    "Shadow Knight", "Shaman", "Warrior", "Wizard",
]

LEVEL_RX = re.compile(r"^==+\s*Level\s+(\d+)\s*==+\s*$", re.IGNORECASE | re.MULTILINE)
# The row body may itself contain templates ({{Classic Short}} in `era=`), so a
# non-greedy .*? terminates at the INNER close and truncates the row. PR 0 got away
# with it because only `name` travelled and it precedes `era`; PR 1 carries era and
# it came out as the literal "{{Classic Short". One level of nesting is all these
# rows have.
ROW_RX = re.compile(r"\{\{RadSpellRow2((?:[^{}]|\{\{[^{}]*\}\})*)\}\}", re.DOTALL)
FIELD_RX = re.compile(r"^\s*\|\s*([a-z_]+)\s*=\s*(.*?)\s*$", re.MULTILINE)
ERA_RX = re.compile(r"\{\{\s*([^}|]+?)\s*(?:Short)?\s*\}\}")


def api_get(params: dict) -> dict:
    params = dict(params, format="json", formatversion="2")
    url = API + "?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=60) as resp:
        data = json.load(resp)
    time.sleep(1.1)  # polite pacing — one request a second, as every harvest here does
    return data


def fetch_class_page(title: str, refetch: bool = False):
    """Wikitext plus the title the wiki actually SERVED.

    The served title is recorded, not the requested one: `redirects=1` means the
    page you get may not be the page you asked for, and recording the request is
    the bug that hit the contribution pack twice (trap 3).
    """
    CACHE.mkdir(exist_ok=True)
    stem = title.replace(" ", "_")
    text_file = CACHE / f"class-{stem}.wikitext"
    meta_file = CACHE / f"class-{stem}.json"
    if text_file.exists() and meta_file.exists() and not refetch:
        return text_file.read_text(encoding="utf-8"), json.loads(meta_file.read_text())["served"]

    data = api_get({
        "action": "query", "prop": "revisions", "titles": title,
        "rvprop": "content|timestamp", "rvslots": "main", "redirects": 1,
    })
    pages = data.get("query", {}).get("pages", [])
    if not pages or "missing" in pages[0]:
        return "", ""
    page = pages[0]
    text = page["revisions"][0]["slots"]["main"]["content"]
    served = page["title"]
    text_file.write_text(text, encoding="utf-8")
    meta_file.write_text(json.dumps({"served": served}), encoding="utf-8")
    return text, served


def level_sections(text: str):
    """Which `==Level N==` headings the page HAS, empty ones included.

    This is a different question from "which levels have rows", and the
    difference is load-bearing: David's ruling admits a spell-page row only where
    the class page has **no section** for that level, so a section that exists and
    is empty is the class page SAYING you get nothing — and must not be filled in
    from somewhere wider. Every page also stops at 50 against Legends' cap of 60,
    so 51-60 is derived for every class and has to be marked, not hidden
    (Bevel: "do not silently pad from spell pages").
    """
    return {int(m.group(1)) for m in LEVEL_RX.finditer(text)}


def parse_class_page(text: str):
    """[(level, name, era)] in page order.

    A row is attributed to the level heading ABOVE it. A page with no headings
    yields nothing, which is the honest answer for the three spell-less classes
    rather than an error.
    """
    rows = []
    marks = [(m.start(), int(m.group(1))) for m in LEVEL_RX.finditer(text)]
    if not marks:
        return rows
    for m in ROW_RX.finditer(text):
        level = None
        for pos, lv in marks:
            if pos < m.start():
                level = lv
            else:
                break
        if level is None:
            continue  # a spell row above the first Level heading belongs to no level
        fields = dict(FIELD_RX.findall(m.group(1)))
        name = (fields.get("name") or "").strip()
        if not name:
            continue
        era_raw = (fields.get("era") or "").strip()
        era = ERA_RX.sub(r"\1", era_raw).strip()
        # The class page's own one-liner ("Party: Increase STR, DEX, AC"). Legends-
        # curated, present on all 1,504 rows, ~50 chars, and the source of the unlock
        # row's hover (David, 2026-08-23). It comes from HERE rather than from the
        # spell pages for the same reason the levels do: this page is this game.
        desc = (fields.get("description") or "").strip()
        rows.append((level, name, era, desc))
    return rows


def probe_unmatched(names, refetch: bool = False):
    """{name -> its own per-class rows}, for class-page names the catalog lacks.

    **This deliberately does NOT resolve aliases, and the reason is the finding.**
    The first cut of this script matched an unmatched class-page name to the
    catalog through each page's `spellname` field. That resolved 32 of 45 — and
    several were nonsense: `Circle of Butcherblock -> Ring of South Ro`,
    `Illusion: Imp -> Illusion: Air Elemental`, `Katta's Song of Sword Dancing ->
    Aria of Asceticism`.

    Fetching the pages settles it. `Circle of Butcherblock` carries
    `spellname = Ring of South Ro` while its own description says it "transports
    your group to the Butcherblock Mountains" and its class list says Druid 25.
    **`spellname` is a copy-paste artefact of the page template, not a canonical
    name.** A resolver built on it invents aliases between unrelated spells.

    What IS reliable on a spell page is the `classes` list — "* [[Druid]] - Level
    34" — which is what this reads, so the report can say whether an unmatched
    name is a real spell we are missing or a phantom.
    """
    CACHE.mkdir(exist_ok=True)
    cache_file = CACHE / "class-spell-probe.json"
    known = json.loads(cache_file.read_text(encoding="utf-8")) if cache_file.exists() and not refetch else {}

    todo = sorted(n for n in names if n not in known)
    for i in range(0, len(todo), 40):
        chunk = todo[i:i + 40]
        data = api_get({
            "action": "query", "prop": "revisions", "titles": "|".join(chunk),
            "rvprop": "content", "rvslots": "main", "redirects": 1,
        })
        query = data.get("query", {})
        redirect = {r["from"]: r["to"] for r in query.get("redirects", [])}
        by_title = {p["title"]: p for p in query.get("pages", [])}
        for name in chunk:
            page = by_title.get(redirect.get(name, name))
            if not page or "revisions" not in page:
                known[name] = []           # no page at all
                continue
            text = page["revisions"][0]["slots"]["main"]["content"]
            known[name] = re.findall(r"^\*\s*\[\[([A-Za-z ]+)\]\]\s*-\s*Level\s*(\d+)",
                                     text, re.MULTILINE)
    cache_file.write_text(json.dumps(known, indent=1, sort_keys=True), encoding="utf-8")
    return known


def load_catalog():
    data = json.loads(CATALOG.read_text(encoding="utf-8-sig"))
    by_class = defaultdict(dict)          # class -> name -> level
    for spell in data["spells"]:
        for cl in spell["classes"]:
            cls = CLASS_FOLD.get(cl["class"], cl["class"])
            by_class[cls][spell["name"]] = cl["level"]
    return by_class


def main():
    refetch = "--refetch" in sys.argv
    catalog = load_catalog()
    per_class = {}

    for cls in CLASSES:
        text, served = fetch_class_page(cls, refetch)
        rows = parse_class_page(text)
        page = {}                          # name -> level (first wins; a page listing
        for level, name, era, desc in rows:   # a spell twice means the earlier level)
            page.setdefault(name, (level, era, desc))
        per_class[cls] = {"served": served, "page": page, "rows": len(rows),
                          "sections": sorted(level_sections(text))}
        print(f"{cls:<14} served={served or '(missing)':<16} rows={len(rows):>4} "
              f"catalog={len(catalog.get(cls, {})):>4}")

    # Case first: "Invisibility versus Animals" and "Invisibility Versus Animals"
    # are one spell and two rows, and counting them as a membership difference
    # would bury the real ones.
    for cls in CLASSES:
        cat = catalog.get(cls, {})
        folded = {n.casefold(): n for n in cat}
        page = per_class[cls]["page"]
        for name in list(page):
            if name not in cat and name.casefold() in folded:
                per_class[cls].setdefault("caseonly", []).append(
                    f"{name} (catalog: {folded[name.casefold()]})")
                page[folded[name.casefold()]] = page.pop(name)

    unmatched = {n for cls in CLASSES for n in per_class[cls]["page"]
                 if n not in catalog.get(cls, {})}
    probe = probe_unmatched(unmatched, refetch)
    real = {n for n, rows in probe.items() if rows}
    print(f"\n{len(unmatched)} class-page names are not in the catalog; "
          f"{len(real)} of them ARE real spell pages with their own class rows")

    lines = [
        "# Class-page spell levels vs the shipped catalog",
        "",
        "**Generated by `class-spells-harvest.py`. This report changes no data** — it is PR 0",
        "of the next-level-spells plan (`FABLE.md`), and it exists so the ~500-row difference",
        "is REVIEWABLE before anything acts on it.",
        "",
        "`SpellLevels.json` is harvested from individual SPELL pages. David ruled (2026-08-23)",
        "that the CLASS page wins, with spell-page rows kept only where the class page has no",
        "section for that level, and flagged as derived. This table is what that costs.",
        "",
        "| Class | Class-page rows | Catalog rows | On page, not in catalog | In catalog, not on page | Level disagrees |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    totals = [0, 0, 0, 0, 0]
    detail = []
    for cls in CLASSES:
        page = per_class[cls]["page"]
        cat = catalog.get(cls, {})
        only_page = sorted(n for n in page if n not in cat)
        only_cat = sorted(n for n in cat if n not in page)
        clash = sorted(n for n in page if n in cat and page[n][0] != cat[n])
        lines.append(f"| {cls} | {len(page)} | {len(cat)} | {len(only_page)} | "
                     f"{len(only_cat)} | {len(clash)} |")
        for i, v in enumerate((len(page), len(cat), len(only_page), len(only_cat), len(clash))):
            totals[i] += v
        if only_page or only_cat or clash:
            detail.append((cls, only_page, only_cat, clash, page, cat))
    lines.append(f"| **Total** | **{totals[0]}** | **{totals[1]}** | **{totals[2]}** | "
                 f"**{totals[3]}** | **{totals[4]}** |")

    lines += ["", "## Per class, by name", "",
              "**On page, not in catalog** is mostly NAMING — the class page prints the name the",
              "class sees, which is sometimes a redirect to the spell's own page title (`Healing",
              "Water` -> `Greater Healing`). **In catalog, not on page** is the ~500: spells whose",
              "own page names the class but which the Legends-curated class page does not list.",
              ""]
    # LEVEL COVERAGE. Fable's plan assumed "every page has all fifty", which would
    # make the spell-page gap-filler vestigial. It is not true, and the difference
    # decides how much of the catalog the class pages can actually source.
    lines += [
        "## Level coverage of the class pages — the gap-filler is NOT vestigial", "",
        "Fable's plan assumed every class page carries every level, which would mean no",
        "spell-page row is ever admitted. **Neither half of that holds.** Every page stops at",
        "**50** — Legends' cap is 60 — so levels 51-60 can only ever come from spell pages,",
        "flagged as derived. Several pages also have interior gaps.",
        "",
        "This is why David's ruling needed its second clause. A level-50 character asking",
        "\"what do I get next\" is answered entirely from derived rows, and Bevel's \"do not",
        "silently pad from spell pages\" is what makes that honest rather than invisible.",
        "",
        "| Class | Sections | Range | Interior gaps |",
        "|---|---:|---|---|",
    ]
    for cls in CLASSES:
        text, _ = fetch_class_page(cls)
        levels = sorted({int(m.group(1)) for m in LEVEL_RX.finditer(text)})
        if not levels:
            lines.append(f"| {cls} | 0 | — | no spell table at all |")
            continue
        interior = [l for l in range(levels[0], levels[-1] + 1) if l not in levels]
        lines.append(f"| {cls} | {len(levels)} | {levels[0]}-{levels[-1]} | "
                     + (", ".join(str(l) for l in interior) if interior else "none") + " |")
    lines.append("")

    caseonly = [(cls, c) for cls in CLASSES for c in per_class[cls].get("caseonly", [])]
    if caseonly:
        lines += [
            "## Case-only differences, folded before comparing", "",
            "One spell, two spellings. Counted as a match so they do not bury the real",
            "differences — but they are a catalog defect of their own.", "",
        ]
        lines += [f"- **{cls}**: {c}" for cls, c in caseonly]
        lines.append("")

    if real:
        lines += [
            "## THE FINDING: `spellname` is unreliable, and our catalog is missing real spells",
            "",
            "Every name below is on a class page, is NOT in our catalog, and **has its own spell",
            "page whose `classes` list names that class and level.** These are not aliases and",
            "not phantoms — they are spells the shipped catalog does not have.",
            "",
            "The cause is in the EXISTING harvest, not in the class pages. A spell page's",
            "`spellname` field is a copy-paste artefact:",
            "",
            "- `Circle of Butcherblock` -> `spellname = Ring of South Ro`, while its own",
            "  description says it \"transports your group to the Butcherblock Mountains\"",
            "  and its class list says Druid 25.",
            "- `Healing Water` -> `spellname = Greater Healing`, while describing a 425-point",
            "  heal and listing Druid 34.",
            "",
            "`spell-levels-promote.py` keys on that name and then de-duplicates, so a page with",
            "a wrong `spellname` is filed under another spell and dropped. **This affects the",
            "\"New at level N\" list that ships today**, not just the new feature.",
            "",
        ]
        for cls in CLASSES:
            missing = sorted(n for n in per_class[cls]["page"]
                             if n not in catalog.get(cls, {}) and n in real)
            if missing:
                lines.append(f"- **{cls}** ({len(missing)}): "
                             + " · ".join(f"{n} [{'; '.join(f'{c} {l}' for c, l in probe[n])}]"
                                          for n in missing))
        lines.append("")

    phantom = sorted(n for n in unmatched if n not in real)
    if phantom:
        lines += [
            "## On a class page, no spell page of its own", "",
            "Neither source is complete here. Listed rather than resolved — PR 1 does not",
            "invent a rule for them.", "",
            "- " + " · ".join(phantom), "",
        ]

    for cls, only_page, only_cat, clash, page, cat in detail:
        lines.append(f"### {cls}")
        lines.append("")
        if clash:
            lines.append("**Level disagrees** (class page wins):")
            lines += [f"- {n}: page {page[n][0]}, catalog {cat[n]}" for n in clash]
            lines.append("")
        if only_page:
            lines.append(f"**On page, not in catalog** ({len(only_page)}):")
            lines.append("- " + " · ".join(only_page))
            lines.append("")
        if only_cat:
            lines.append(f"**In catalog, not on page** ({len(only_cat)}):")
            lines.append("- " + " · ".join(only_cat))
            lines.append("")

    # The DATA output. Written after the case fold above has run, so a class page's
    # "Skin like Wood" is already reconciled with the catalog's "Skin Like Wood" and
    # the merge downstream is not re-deciding a spelling.
    ROWS.write_text(json.dumps({
        cls: {
            "served": per_class[cls]["served"],
            "sections": per_class[cls]["sections"],
            "spells": {name: {"level": lv, "era": era, "description": desc}
                       for name, (lv, era, desc) in sorted(per_class[cls]["page"].items())},
        } for cls in CLASSES
    }, indent=1, ensure_ascii=False, sort_keys=True), encoding="utf-8")
    print(f"wrote {ROWS}")

    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"\nwrote {REPORT}")
    print(f"totals: page={totals[0]} catalog={totals[1]} onlyPage={totals[2]} "
          f"onlyCatalog={totals[3]} levelClash={totals[4]}")


if __name__ == "__main__":
    main()
