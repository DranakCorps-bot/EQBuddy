#!/usr/bin/env python3
"""Build the fourteen Epic 1.0 guides into the curated guide catalog (DRA-41, Delivery 3).

WHAT THIS IS, AND WHAT IT IS NOT
--------------------------------
`GuideCatalog.json` is CURATED and never auto-written — the weekly refresh may FLAG a guide
whose source page moved, it may not rewrite one. This script does not break that rule, and the
distinction is worth stating plainly because it is the whole safety argument:

  * It never fetches. Nothing here touches eqlwiki.
  * Its input is `EpicQuestChecklist.json`, which is itself CURATED — a human transcribed each
    class's `== Checklist ==` into ordered, sectioned rows, and every box a player has ticked
    is keyed to those row ids.
  * It is a RESHAPE, not a harvest: row -> objective, section -> stage, and the row's text
    carried across verbatim as a `Transcribed` step's WHAT. No field is derived from prose, no
    prerequisite is invented, no reward is chosen.

So it is run BY A PERSON, once, as part of an authoring PR — the way you would run a rename
across 486 rows rather than doing it by hand and getting three of them wrong. It is committed
because a reviewer has to be able to check the 486 rows arrived unaltered, and reading a
diff is not that check; re-running this and finding the file unchanged is.

BYTE-REPRODUCIBLE
-----------------
Rewrites the whole catalog with the formatting it already has (UTF-8, 2-space indent, CRLF,
trailing newline), so the 95 Plane of Sky guides round-trip with a zero-byte diff and the only
change in `git diff` is the fourteen guides this adds. Run it twice: the second run changes
nothing. `EpicGuideDataTests` asserts the shape of the result from the other side.

    python scripts/harvests/eqlwiki/epic-guides-build.py [--check]

`--check` writes nothing and exits 1 if the file on disk is not what this would produce — the
form for CI or for a reviewer who wants the assertion without the write.
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
DATA = ROOT / "src" / "EQBuddy.Core" / "Data"
CATALOG = DATA / "GuideCatalog.json"
EPIC_CHECKLIST = DATA / "EpicQuestChecklist.json"
QUEST_CATALOG = DATA / "QuestCatalog.json"

# When the class epic pages this transcribes were harvested into the cache. Not "today": a
# source date says when the fact was taken from the page, and these sentences were taken when
# `cache/quest-{Class}_Epic_Quest.wikitext` was fetched. Verified identical for all fourteen:
#
#     git log -1 --format=%cs -- scripts/harvests/eqlwiki/cache/quest-Bard_Epic_Quest.wikitext
#
# A source with no honest date cannot be aged, and a date that flatters us is worse than none.
HARVEST_DATE = "2026-08-07"

# The state every one of these 486 steps is in, and the only one they may be in: the page
# states one complete instruction and we carry it verbatim. Promotion to Authored — a human
# answering who and where from the sources — is an authoring PR, never this script.
TRANSCRIBED = "Transcribed"

# Every row is a sentence off a checklist and we do not classify it. "Custom" is the schema's
# word for that, and it is the honest one: guessing Loot/TalkToNpc/Kill from prose is the
# inference `Transcribed` exists to refuse (trap 73), and the type is READ — it picks the
# verb in `GuidePresentation.Directions` and gates the Sky item-backed routing home.
OBJECTIVE_TYPE = "Custom"

# The section name that means "the page had no sub-headings", as `EpicQuestDefaults` spells it.
PLAIN_CHECKLIST = "Checklist"


def slug(text: str) -> str:
    """Kebab-case, matching the ids already in `EpicQuestChecklist.json`."""
    out = []
    for ch in text.lower():
        if ch.isalnum():
            out.append(ch)
        elif out and out[-1] != "-":
            out.append("-")
    return "".join(out).strip("-")


def source_for(quest: dict) -> dict:
    """The class's epic page, as the page a reader can check and the string the weekly refresh
    intersects with the week's changed pages. `quest["name"]` IS the served title — the
    harvest records what it got back, not what it asked for (trap 3)."""
    return {
        "url": quest["url"],
        "title": quest["name"],
        "retrievedAt": HARVEST_DATE,
    }


def stages_for(rows: list[dict], quest_name: str) -> list[dict]:
    """Sections become stages, IN ROW ORDER. The sections of every shipped class are
    contiguous runs (asserted below), so a stage is a run and the reading order is the row
    order — no sequence is invented and none is derived a second way (trap 4)."""
    stages: list[dict] = []
    seen_ids: set[str] = set()
    for row in sorted(rows, key=lambda r: r["order"]):
        section = row.get("section") or PLAIN_CHECKLIST
        if not stages or stages[-1]["_section"] != section:
            stage_id = slug(section) or "stage"
            if stage_id in seen_ids:
                raise SystemExit(
                    f"section '{section}' appears twice and not as one run — a stage would "
                    f"claim two positions in the reading order"
                )
            seen_ids.add(stage_id)
            stages.append({
                "_section": section,
                "id": stage_id,
                "name": section,
                "order": len(stages) + 1,
                "objectives": [],
            })
        stage = stages[-1]
        stage["objectives"].append({
            "id": row["id"],
            "order": len(stage["objectives"]) + 1,
            "objectiveType": OBJECTIVE_TYPE,
            # VERBATIM. The one thing a transcribed step claims.
            "what": row["text"],
            "authoring": TRANSCRIBED,
        })

    # A page with no sub-headings gave us one nameless run, and "Checklist" as a heading over
    # the whole quest says nothing. Name it for the epic instead — the quest's own page title,
    # which is a fact the catalog already holds rather than a phrase invented here.
    if len(stages) == 1 and stages[0]["name"] == PLAIN_CHECKLIST:
        stages[0]["name"] = quest_name
        stages[0]["id"] = slug(quest_name)

    for stage in stages:
        del stage["_section"]
    return stages


def build_guides(checklist: dict, quests: dict) -> list[dict]:
    by_name = {q["name"]: q for q in quests["quests"]}
    guides = []
    for class_checklist in checklist["classes"]:
        class_name = class_checklist["className"]
        quest_name = f"{class_name} Epic Quest"
        quest = by_name.get(quest_name)
        if quest is None:
            # The guide's link into the harvested index has to resolve as the APP loads it —
            # a dangling questName looks fine in a text search and is broken on every surface.
            raise SystemExit(f"QuestCatalog has no quest named '{quest_name}'")

        source = source_for(quest)
        zones = [z for z in quest.get("zones", []) if z] or [quest.get("startZone", "")]
        stages = stages_for(class_checklist["rows"], quest_name)
        for stage in stages:
            for objective in stage["objectives"]:
                objective["sources"] = [dict(source)]

        guides.append({
            "id": f"epic-{slug(class_name)}",
            # NOT a reward name. eqlwiki lists three rewards for the Warrior and six for the
            # Necromancer and names none of them "the epic" — see GuidePresentation.EpicTitle.
            "name": f"{class_name} Epic 1.0",
            "guideType": "EpicQuest",
            "questName": quest_name,
            "zoneNames": zones,
            "applicableClasses": [class_name],
            "sources": [dict(source)],
            "stages": stages,
        })
    return guides


def build_catalog() -> str:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    checklist = json.loads(EPIC_CHECKLIST.read_text(encoding="utf-8"))
    quests = json.loads(QUEST_CATALOG.read_text(encoding="utf-8"))

    epics = build_guides(checklist, quests)

    rows = sum(len(c["rows"]) for c in checklist["classes"])
    objectives = sum(len(s["objectives"]) for g in epics for s in g["stages"])
    if rows != objectives:
        # The must-list, stated positively (trap 34): every epic row has exactly one objective.
        # A guard that only forbids the wrong thing cannot see a missing thing, and a dropped
        # row is a step a player never sees and never knows is gone.
        raise SystemExit(f"{rows} epic rows produced {objectives} objectives — they must match")

    # Idempotent: this owns every EpicQuest guide in the file and nothing else in it.
    kept = [g for g in catalog["guides"] if g.get("guideType") != "EpicQuest"]
    catalog["guides"] = kept + epics

    text = json.dumps(catalog, indent=2, ensure_ascii=False) + "\n"
    return text.replace("\n", "\r\n")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="write nothing; exit 1 if the file on disk differs")
    args = parser.parse_args()

    wanted = build_catalog().encode("utf-8")
    current = CATALOG.read_bytes()

    if wanted == current:
        print(f"{CATALOG.name} is already what this produces ({len(wanted)} bytes).")
        return 0
    if args.check:
        print(f"{CATALOG.name} differs from what this produces "
              f"({len(current)} bytes on disk, {len(wanted)} bytes generated).", file=sys.stderr)
        return 1

    CATALOG.write_bytes(wanted)
    print(f"wrote {CATALOG.name} ({len(wanted)} bytes).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
