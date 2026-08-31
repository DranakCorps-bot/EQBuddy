#!/usr/bin/env python3
"""Promote the quest harvest into the embedded catalog (QuestCatalog.json).

This scripts the converter that previously lived only in a working session —
verified byte-identical against the shipped catalog on 2026-08-09:

  - zones: zone categories + Related Zones + Start Zone, sorted. A category is
    a zone unless it ends in "Quests" — the wiki files quests under both zone
    categories ("South Qeynos") and quest-list categories ("Paladin Quests",
    "All Classes Quests"), and the suffix is the reliable separator.
  - questItems: Category:Quest Items passthrough (the wiki's own "part of a
    quest" marker — broader than turn-ins on purpose, it feeds the Loot views'
    "don't vendor this" signal).
  - every other field passes through; categories/relatedZones fold away.

Serialization is single-line compact JSON with a fixed entry key order: the
catalog is reviewed as a diff in knowledge-refresh PRs, and a stable shape is
what keeps those diffs about DATA, not formatting.
"""

import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[2] / "src" / "EQBuddy.Core" / "Data" / "QuestCatalog.json"


# Hand corrections, applied to the promoted catalog. One row per fact, each carrying
# the discussion that reported it and WHY the harvest cannot read it.
#
# **This exists because a guard caught the same revert twice.** #246 (jlcrisp) fixed
# Blackburrow Brewers from 1 cask to 3 and shipped in 1.99.14; CatalogSanityTests pinned
# it "so a future harvest run can't silently reset it back to 1" — and one week later the
# 2026-08-31 refresh reset it back to 1 anyway. The test failed, which is the guard doing
# its job, but a pin only a human can re-apply is a weekly chore rather than a fix, and
# the chore is invisible until the build breaks.
#
# The parser is not wrong: it reads "3 x [[Item]]", and this quest's page says "When you
# have recovered three of these casks" in prose. Teaching it English number words would
# change every quest in the catalog to fix one, which is a worse trade than a named row.
#
# **The wiki-first half is the real fix and does not live here** — the paste-ready ask is
# for the page to state the requirement as "3 x [[Blackburrow Cask]]", after which this
# row becomes redundant and should be deleted. Until then EQBuddy quotes what the player
# is actually asked to hand in.
ITEM_QTY_CORRECTIONS = {
    # (quest name, item name) -> qty
    ("Blackburrow Brewers", "Blackburrow Cask"):
        (3, "#246 jlcrisp — the page says 'three of these casks' in prose, not '3 x'"),
}


def promote(q):
    zones = {c for c in q["categories"] if not c.endswith("Quests")}
    zones |= set(q["relatedZones"])
    if q["startZone"]:
        zones.add(q["startZone"])
    return {
        "name": q["name"],
        "url": q["url"],
        "startZone": q["startZone"],
        "questGiver": q["questGiver"],
        "minLevel": q["minLevel"],
        "classes": q["classes"],
        "items": [dict(it, qty=ITEM_QTY_CORRECTIONS[(q["name"], it["name"])][0])
                  if (q["name"], it["name"]) in ITEM_QTY_CORRECTIONS else it
                  for it in q["items"]],
        "rewards": q["rewards"],
        "repeatable": q["repeatable"],
        "era": q["era"],
        "zones": sorted(zones),
    }


def main():
    quests = json.loads((HERE / "quests.json").read_text(encoding="utf-8"))
    items = json.loads((HERE / "quest-items.json").read_text(encoding="utf-8"))
    catalog = {"quests": [promote(q) for q in quests], "questItems": items}
    OUT.write_text(json.dumps(catalog, separators=(",", ":"), ensure_ascii=False),
                   encoding="utf-8")
    print(f"wrote {OUT}: {len(catalog['quests'])} quests, {len(items)} quest items")

    # A correction that matches nothing has ROTTED — the quest or item was renamed, or
    # the row was left behind after the wiki was fixed. Say so: a silently-inert
    # correction is how the catalog goes back to being wrong without anything failing.
    pairs = {(q["name"], it["name"]) for q in quests for it in q["items"]}
    for key, (qty, why) in sorted(ITEM_QTY_CORRECTIONS.items()):
        status = f"applied qty={qty}" if key in pairs else "NO LONGER MATCHES ANY ROW"
        print(f"  correction {key[0]} / {key[1]}: {status}  ({why})")


if __name__ == "__main__":
    main()
