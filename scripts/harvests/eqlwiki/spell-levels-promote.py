#!/usr/bin/env python3
"""Promote per-class spell levels into SpellLevels.json — CLASS pages first.

PR 1 of Fable 5's "next-level spells by class" plan (FABLE.md). Until 2026-08-23
this file read only `spells.json`, the harvest of individual SPELL pages. David
ruled that eqlwiki's per-class pages win:

    "Class page wins; spell pages fill gaps only where the class page has no
     section for that level; anything sourced from a spell page is FLAGGED as
     such."

WHY THE SOURCES DISAGREE, AND WHY THE CLASS PAGE IS RIGHT
---------------------------------------------------------
A spell page names every class that has ever had the spell; the Legends-curated
CLASS page names what this game gives you at that level, and carries an `era`
field the spell pages lack. So the two disagree on MEMBERSHIP far more than on
level: 498 rows to 7 (class-spells-report.md). Applying the ruling removes about
a quarter of the old catalog — Velious-era ports a Legends druid never learns —
and that is the point of it rather than a side effect.

It also FIXES rows that were missing outright. The old promotion keyed each
harvest page on its `spellname` field, which is a copy-paste artefact rather than
a canonical name: `Healing Water` declares `spellname = Greater Healing` while
describing a 425-point heal, so it de-duplicated away and never reached the
catalog at all. Reading the class page settles it — the class page prints the
name the class actually sees.

THE GAP-FILLER IS LOAD-BEARING, NOT VESTIGIAL
---------------------------------------------
Fable's plan assumed every class page carries every level, which would mean no
spell-page row is ever admitted. Neither half holds. **Every class page stops at
level 50** and Legends' cap is 60, so 51-60 is derived for every class; several
pages also have interior gaps (Paladin is missing seven sections, Rogue
thirty-five). Those levels are answered from spell pages and MARKED, which is
what makes them honest instead of invisible (Bevel, Helm-signed 2026-08-23: "do
not silently pad from spell pages").

The distinction that decides each row is **section, not row**: a `==Level N==`
heading that exists and is empty is the class page SAYING you gain nothing at N,
and must not be filled in from a wider source. Only a level with no heading at
all is a gap.

Row discipline (unchanged from the spell-page era where it still applies):
  - levels: integers 1..60 only. 0/absent is the wiki's "unknown" and never a
    level.
  - class names: "Shadowknight" folds into "Shadow Knight" — the app's spelling
    (QuestClassFilter.Classes).
  - display name: the class page's spelling wins, which also settles seventeen
    case-only twins ("Skin like Wood" vs "Skin Like Wood").

Serialization is single-line compact JSON with a fixed entry key order: the
catalog is reviewed as a diff in knowledge-refresh PRs, and a stable shape is
what keeps those diffs about DATA, not formatting.

Inputs:  spells.json (spells-harvest.py), class-spells.json (class-spells-harvest.py)
Output:  src/EQBuddy.Core/Data/SpellLevels.json
"""

import json
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[2] / "src" / "EQBuddy.Core" / "Data" / "SpellLevels.json"

LEVEL_CAP = 60
CLASS_FOLD = {"Shadowknight": "Shadow Knight"}

SOURCE_CLASS = "class"
SOURCE_SPELL = "spell"


def spell_page_rows():
    """{(class, name_casefold): (level, display_name)} from the SPELL pages.

    The old de-duplication is kept verbatim for the rows that still reach the
    catalog — a spell held on extra pages (epic guides, "Spell: X" shells) whose
    own page wins the group, remaining conflicts merging to the earliest level.
    """
    spells = json.loads((HERE / "spells.json").read_text(encoding="utf-8"))
    groups = defaultdict(list)
    for s in spells:
        name = (s.get("name") or "").strip()
        if name:
            groups[name.casefold()].append(s)

    rows = {}
    descriptions = {}
    # A SECOND index, keyed on the page's own TITLE rather than its `spellname`.
    #
    # `name` above is `spellname or title` (spells-harvest.py), and this file's own
    # header says why that is not a canonical name: `Healing Water` declares
    # `spellname = Greater Healing` while describing a 425-point heal. The LEVELS
    # stopped trusting it when the class page became authoritative — but the
    # description fallback added for the KhazamSpellRow rename looked prose up by
    # that same artefact name, so a class-page row whose page files itself under a
    # copy-pasted `spellname` found nothing and shipped with no tooltip at all.
    #
    # That was all 24 of the entries the rename left description-less, and every one
    # of them HAS wiki prose on its own page: `Circle of Butcherblock` (spellname
    # `Ring of South Ro`) says "transports your group to the Butcherblock Mountains";
    # `Leech` (spellname `Leach`) says "Drains the life from your target". The page
    # title is what the class page names, so it is the key that matches.
    #
    # Consulted only AFTER the `spellname` index, so no row that resolves today
    # changes: this strictly fills blanks.
    by_page_title = {}
    for s in spells:
        title = (s.get("page_title") or "").strip()
        if title and (d := (s.get("description") or "").strip()):
            by_page_title.setdefault(title.casefold(), d)

    for group in groups.values():
        exact = [e for e in group if e.get("page_title") == e["name"]]
        picked = exact or group
        display = sorted(e["name"] for e in picked)[0]
        for e in picked:
            if (d := (e.get("description") or "").strip()):
                descriptions.setdefault(display.casefold(), d)
        for e in picked:
            for c in e.get("classes") or []:
                cls = CLASS_FOLD.get(c.get("class") or "", c.get("class") or "")
                lv = c.get("level")
                if not cls or not isinstance(lv, int) or not 1 <= lv <= LEVEL_CAP:
                    continue
                key = (cls, display.casefold())
                prev = rows.get(key)
                rows[key] = (min(prev[0], lv) if prev else lv, display)
    return rows, descriptions, by_page_title


def main():
    classes = json.loads((HERE / "class-spells.json").read_text(encoding="utf-8"))
    page_rows, page_descriptions, page_title_descriptions = spell_page_rows()

    # class -> the levels its page HAS a section for. A class with no page at all
    # (Warrior, Monk, Berserker) has no sections, so every spell-page row for it
    # would be a "gap" — which is why those three are excluded outright below:
    # the wiki says they have no spell table, and deriving them one from spell
    # pages is exactly the invention this ruling exists to stop.
    sections = {cls: set(v["sections"]) for cls, v in classes.items()}
    spell_less = {cls for cls, v in classes.items() if not v["sections"]}

    # name_casefold -> {"name": display, "classes": {cls: {...}}}
    entries = {}

    def put(display, cls, level, source, description=""):
        key = display.casefold()
        entry = entries.setdefault(key, {"name": display, "classes": {}, "description": ""})
        # The CLASS page's one-liner wins, for the same reason its levels do. A spell
        # page's description only fills an entry no class page describes — which is
        # every derived (51-60, interior-gap) row.
        if description and (source == SOURCE_CLASS or not entry["description"]):
            entry["description"] = description
        # The class page's spelling wins the display name for the whole entry.
        if source == SOURCE_CLASS:
            entry["name"] = display
        # `era` is deliberately NOT carried into the catalog. It parses cleanly now
        # and is "Classic" on all 1,504 class-page rows — one value discriminates
        # nothing, and a field written by a harvest and read by nothing is trap 43's
        # mirror. Add it the day a surface shows it.
        entry["classes"][cls] = {"class": cls, "level": level, "source": source}

    # 1. Class pages, authoritative.
    for cls, data in sorted(classes.items()):
        for name, row in data["spells"].items():
            lv = row["level"]
            if 1 <= lv <= LEVEL_CAP:
                # eqlwiki's class-row template lost its `description` field when
                # RadSpellRow2 became KhazamSpellRow (2026-08-31); the replacement
                # `effects` is a <br>-joined mechanics list, not the prose the tooltip
                # promises. The SPELL PAGE still carries that prose for 97% of them, and
                # it is the same source step 2 below already trusts -- so fall back to it
                # rather than inventing a sentence or showing markup. Without this the
                # class rows come back description-less: 1,352 of 1,352 described before
                # the rename, 345 of 1,353 after.
                # ...and the page-TITLE index last, which is what catches the pages
                # filed under a copy-pasted `spellname` (see spell_page_rows).
                desc = (row.get("description")
                        or page_descriptions.get(name.casefold(), "")
                        or page_title_descriptions.get(name.casefold(), ""))
                put(name, cls, lv, SOURCE_CLASS, desc)

    # 2. Spell pages, ONLY for a (class, level) the class page has no section for.
    derived = 0
    for (cls, _), (lv, display) in sorted(page_rows.items()):
        if cls in spell_less or cls not in sections:
            continue
        if lv in sections[cls]:
            continue          # the class page covered this level; its answer stands
        key = display.casefold()
        if key in entries and cls in entries[key]["classes"]:
            continue          # already answered for this class by its own page
        put(display, cls, lv, SOURCE_SPELL, page_descriptions.get(key, ""))
        derived += 1

    out = []
    for key in sorted(entries, key=lambda k: (entries[k]["name"].casefold(), entries[k]["name"])):
        e = entries[key]
        rows = [e["classes"][c] for c in sorted(e["classes"])]
        row = {"name": e["name"]}
        if e["description"]:
            row["description"] = e["description"]
        row["classes"] = rows
        out.append(row)

    OUT.write_text(json.dumps({"spells": out}, separators=(",", ":"), ensure_ascii=False),
                   encoding="utf-8")

    total_rows = sum(len(e["classes"]) for e in out)
    by_source = defaultdict(int)
    for e in out:
        for r in e["classes"]:
            by_source[r["source"]] += 1
    print(f"wrote {OUT}: {len(out)} spells, {total_rows} class-level rows "
          f"({by_source[SOURCE_CLASS]} from class pages, "
          f"{by_source[SOURCE_SPELL]} derived from spell pages where the class "
          f"page has no section for that level)")
    print(f"spell-less classes excluded outright: {', '.join(sorted(spell_less))}")


if __name__ == "__main__":
    main()
