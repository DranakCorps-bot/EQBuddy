#!/usr/bin/env python3
"""DRA-149 D3's OPENING SURVEY — the plan's declared escalation seam (P4).

Fable's P4 un-parks Farm Materials on the catalog's ``Recipes`` column and says, in
as many words:

    Products self-filter structurally — they mostly carry no ``DropZones`` — but the
    slice's OPENING SURVEY measures that instead of assuming it, and **if materials
    cannot be told from products defensibly it STOPS AND ESCALATES with the number**.

This script is that measurement, and it is committed as the record of the answer the
way ``scripts/dra83-attachments.py`` is the record of DRA-83's two rules. It reads the
SHIPPED catalog and **fetches nothing**.

Run:  python scripts/dra149-materials-survey.py
"""

from __future__ import annotations

import collections
import gzip
import json
import pathlib
import re

CATALOG = pathlib.Path(__file__).resolve().parent.parent / "src/EQBuddy.Core/Data/ItemCatalog.json.gz"

# The EIGHT with a Mastery AA — Core/Tradeskills.cs is the curated source and this list
# is its echo, in the same alphabetical order. The four REAL skills deliberately outside
# it are the committed negatives below.
EIGHT = [
    "Alchemy",
    "Baking",
    "Blacksmithing",
    "Brewing",
    "Fletching",
    "Jewelcrafting",
    "Pottery",
    "Tailoring",
]

# Real skills the wiki names in recipe headings that are NOT professions here, because
# none has a Mastery AA (Tradeskills.cs says so by name). They are measured so the edge
# of the curation is visible rather than discovered.
NOT_PROFESSIONS = ["Spell Research", "Tinkering", "Make Poison", "Poison Making", "Fishing", "Research"]

TRIVIAL = re.compile(r"\(Trivial:")


def load():
    with gzip.open(CATALOG, "rt", encoding="utf-8") as fh:
        return json.load(fh)["Items"]


def heading_of(entry: str) -> str | None:
    """A recipe entry is a HEADING when it names a profession and carries no recipe.

    The wiki spells one of them as a raw wikitext heading on five pages ("== Tailoring =="),
    so the strip is part of the reading rather than a separate repair.
    """
    text = entry.strip().strip("=").strip()
    if TRIVIAL.search(entry):
        return None
    return text if text in EIGHT or text in NOT_PROFESSIONS else None


def sections(recipes: list[str]) -> dict[str, list[str]]:
    """Profession heading -> the recipe lines under it, in the page's own order."""
    out: dict[str, list[str]] = collections.defaultdict(list)
    current: str | None = None
    for entry in recipes:
        head = heading_of(entry)
        if head is not None:
            current = head
            out.setdefault(head, [])
            continue
        if current is not None:
            out[current].append(entry)
    return dict(out)


def product_name(line: str) -> str:
    """"Legion Lager (Trivial: 36)" -> "Legion Lager"."""
    return TRIVIAL.split(line)[0].strip().rstrip("(").strip()


# The DRA-84 D4 finding: a DropZones string is not always a place. This is the same
# class of junk, measured here so the engine's numbers are not inflated by it.
NON_PLACE = re.compile(r"^(\}\}|:\*|Category:|N ?O ?T ?_|ITEM REMOVED|<br)|^\s*$|^/$")
VAGUE = {"various zones", "unknown", "d3+ zones", "n/a", "?"}


def is_place(zone: str) -> bool:
    return not NON_PLACE.match(zone.strip()) and zone.strip().lower() not in VAGUE


def main() -> int:
    items = load()
    by_name = {r["Name"]: r for r in items}

    # Every recipe OUTPUT any page names, so "is this record itself a product" can be
    # asked of the corpus rather than guessed from the item's name.
    outputs: set[str] = set()
    for rec in items:
        for prof, lines in sections(rec.get("Recipes") or []).items():
            for line in lines:
                outputs.add(product_name(line))

    print(f"catalog: {len(items)} records, {sum(1 for r in items if r.get('Recipes'))} carry a Recipes list")
    print(f"distinct recipe OUTPUTS named anywhere: {len(outputs)}")
    print()

    print("== 1. Per profession (records naming it as a heading) ==")
    print(f"{'profession':<16}{'records':>9}{'drop zones':>12}{'real place':>12}{'creatures':>11}")
    totals = collections.Counter()
    per_prof_zones: dict[str, set[str]] = collections.defaultdict(set)
    for prof in EIGHT + NOT_PROFESSIONS:
        recs = [r for r in items if prof in sections(r.get("Recipes") or [])]
        zoned = [r for r in recs if r.get("DropZones")]
        placed = [r for r in recs if any(is_place(z) for z in (r.get("DropZones") or []))]
        mobbed = [r for r in recs if r.get("DropMobs")]
        for r in placed:
            for z in r.get("DropZones") or []:
                if is_place(z):
                    per_prof_zones[prof].add(z)
        if prof in EIGHT:
            totals["records"] += len(recs)
            totals["zoned"] += len(zoned)
            totals["placed"] += len(placed)
            totals["mobbed"] += len(mobbed)
        mark = "" if prof in EIGHT else "   (not a profession here)"
        print(f"{prof:<16}{len(recs):>9}{len(zoned):>12}{len(placed):>12}{len(mobbed):>11}{mark}")
    print(f"{'THE EIGHT':<16}{totals['records']:>9}{totals['zoned']:>12}{totals['placed']:>12}{totals['mobbed']:>11}")
    print()

    print("== 2. THE ESCALATION QUESTION: can a material be told from a product? ==")
    eight_recs = [r for r in items if any(p in sections(r.get("Recipes") or []) for p in EIGHT)]
    also_output = [r for r in eight_recs if r["Name"] in outputs]
    farmable = [r for r in eight_recs if any(is_place(z) for z in (r.get("DropZones") or []))]
    both = [r for r in also_output if any(is_place(z) for z in (r.get("DropZones") or []))]
    print(f"records naming one of the eight              : {len(eight_recs)}")
    print(f"  ... that are ALSO a recipe output somewhere: {len(also_output)}  (intermediates)")
    print(f"  ... that drop in a real place              : {len(farmable)}")
    print(f"  ... BOTH an output AND a real drop         : {len(both)}   <- the only ambiguous class")
    pure_products = [r for r in also_output if not any(is_place(z) for z in (r.get("DropZones") or []))]
    print(f"  ... an output that does NOT drop           : {len(pure_products)}  (self-filtered by the zone gate)")
    if both:
        print("  the ambiguous ones, all of them:")
        for r in both[:40]:
            zones = [z for z in (r.get("DropZones") or []) if is_place(z)]
            print(f"    - {r['Name']}  ({', '.join(zones[:3])})")
    print()
    print("  READING: an item page's `recipes` field names the recipes the item is USED IN,")
    print("  so the record IS the ingredient. The exhibit both ways, from the shipped file:")
    for name in ("A Giant Blood Sac", "Legion Lager"):
        r = by_name.get(name)
        if r:
            print(f"    {name:<20} Recipes={r.get('Recipes')}  DropZones={r.get('DropZones')}")
    print()

    print("== 3. Jewelcrafting, the Founder's own example ==")
    jc = [r for r in items if "Jewelcrafting" in sections(r.get("Recipes") or [])]
    jc_farm = [r for r in jc if any(is_place(z) for z in (r.get("DropZones") or []))]
    jc_who = [r for r in jc_farm if r.get("DropMobs")]
    print(f"materials: {len(jc)}   with a real drop zone: {len(jc_farm)}   naming a creature: {len(jc_who)}")
    print(f"distinct zones: {len(per_prof_zones['Jewelcrafting'])}")
    for r in jc_farm[:8]:
        zones = [z for z in (r.get("DropZones") or []) if is_place(z)]
        who = sum(len(v) for v in (r.get("DropMobs") or {}).values())
        print(f"    - {r['Name']:<34} {len(zones)} zone(s), {who} creature(s): {', '.join(zones[:3])}")
    print()

    print("== 4. The named gaps (each draws its own sentence, never padding) ==")
    for prof in EIGHT:
        recs = [r for r in items if prof in sections(r.get("Recipes") or [])]
        placed = [r for r in recs if any(is_place(z) for z in (r.get("DropZones") or []))]
        if not placed:
            print(f"  {prof}: {len(recs)} materials, ZERO with a drop zone — draws its gap sentence")
    print()

    print("== 5. Distinct-count telltale (trap 73) ==")
    zoned_all = sorted({z for r in eight_recs for z in (r.get("DropZones") or []) if is_place(z)})
    print(f"distinct real zones across the eight: {len(zoned_all)}")
    mobs = {m for r in eight_recs for v in (r.get("DropMobs") or {}).values() for m in v}
    print(f"distinct creatures named            : {len(mobs)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
