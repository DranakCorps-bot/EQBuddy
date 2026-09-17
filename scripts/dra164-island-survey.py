#!/usr/bin/env python3
"""DRA-164 D1 — the island-placement survey, reproducible in one command.

The plan's four figures (103 / 22 / 97 / 95) are the D1 guard's floors, and a floor
nobody can re-derive is a number copied rather than measured (trap 52). This script is
the second parse: it re-implements `SkyIslands.Parse` and the P2 placement rule over the
SHIPPED `GuideCatalog.json` and prints the outcome counts. The C# sweep
(`SkyIslandPlacementSweepTests`) is the first parse, and the two must agree.

    python scripts/dra164-island-survey.py [--shipped-parser]

`--shipped-parser` uses the PRE-DRA-164 regex, which is how the Efreeti defect is shown
rather than asserted: the same 22 rows come back on {1.5} alone.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

CATALOG = Path(__file__).resolve().parent.parent / "src" / "EQBuddy.Core" / "Data" / "GuideCatalog.json"

WORDS = {
    "one": 1.0, "two": 2.0, "three": 3.0, "four": 4.0,
    "five": 5.0, "six": 6.0, "seven": 7.0, "eight": 8.0,
}

# The shipped (pre-DRA-164) shape: the isle word, then ONE number.
SHIPPED_RX = re.compile(r"\bisles?\b\s*\.?\s*(\d+(?:\.\d+)?|[a-z]+)", re.IGNORECASE)

# DRA-164 P3: the isle word, then a LIST of numbers joined by commas / "and" / "&".
# Only numbers — a spelled-out word may still lead, but the list arm is digits, because
# "Isle four - griffons and pegasus" must not read "pegasus" as a second island.
LIST_RX = re.compile(
    r"\bisles?\b\s*\.?\s*(?P<first>\d+(?:\.\d+)?|[a-z]+)"
    r"(?P<rest>(?:\s*(?:,|and|&)\s*\d+(?:\.\d+)?)*)",
    re.IGNORECASE,
)
NUM_RX = re.compile(r"\d+(?:\.\d+)?")


def parse_shipped(source: str | None) -> list[float]:
    if not source or not source.strip():
        return []
    found: list[float] = []
    for token in SHIPPED_RX.findall(source):
        n = _number(token)
        if n is not None and n not in found:
            found.append(n)
    return sorted(found)


def parse_new(source: str | None) -> list[float]:
    if not source or not source.strip():
        return []
    found: list[float] = []
    for m in LIST_RX.finditer(source):
        n = _number(m.group("first"))
        if n is None:
            continue
        if n not in found:
            found.append(n)
        for tail in NUM_RX.findall(m.group("rest") or ""):
            t = float(tail)
            if t not in found:
                found.append(t)
    return sorted(found)


def _number(token: str) -> float | None:
    try:
        return float(token)
    except ValueError:
        return WORDS.get(token.lower())


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--shipped-parser", action="store_true",
                    help="use the pre-DRA-164 regex (shows the Efreeti defect)")
    args = ap.parse_args()
    parse = parse_shipped if args.shipped_parser else parse_new

    data = json.loads(CATALOG.read_text(encoding="utf-8"))
    buckets: Counter[str] = Counter()
    stages: Counter[str] = Counter()
    efreeti_sets: Counter[str] = Counter()
    # heading -> (sort, count), so the printed order is the order the view draws.
    headings: dict[str, list] = {}
    guides = 0
    objectives = 0

    def number(n: float) -> str:
        return f"{n:.0f}" if n % 1 == 0 else f"{n:.1f}"

    for guide in data["guides"]:
        if guide.get("guideType") != "PlaneOfSkyQuest":
            continue
        guides += 1
        for stage in guide.get("stages", []):
            name = stage.get("name", "")
            for obj in stage.get("objectives", []):
                objectives += 1
                stages[name] += 1
                # P2's rule: the stage name first; the objective's Where ONLY when the
                # stage name parses empty AND the step is not the hand-in.
                islands = parse(name)
                source = "stage"
                if not islands and obj.get("objectiveType") != "TurnIn":
                    islands = parse(obj.get("where", ""))
                    source = "where"
                if obj.get("objectiveType") == "TurnIn":
                    buckets["turn-in (excluded)"] += 1
                    continue
                if not islands:
                    buckets["no island"] += 1
                    heading, sort = name, 99.0
                elif len(islands) == 1:
                    buckets["single isle"] += 1
                    heading, sort = "Island " + number(islands[0]), islands[0]
                else:
                    buckets["multi isle"] += 1
                    heading = "Islands " + " · ".join(number(i) for i in islands)
                    sort = 90.0
                row = headings.setdefault(heading, [sort, 0])
                row[1] += 1
                if name == "The Efreeti drop" and islands:
                    efreeti_sets["/".join(str(i) for i in islands) + f" ({source})"] += 1

    print(f"parser: {'SHIPPED (pre-DRA-164)' if args.shipped_parser else 'DRA-164 P3'}")
    print(f"PoS guides: {guides}   objectives: {objectives}")
    for key in ("single isle", "multi isle", "no island", "turn-in (excluded)"):
        print(f"  {key:22} {buckets[key]}")
    print("stage-name distinct count (the second parse of the same fact):")
    for name, n in sorted(stages.items(), key=lambda kv: -kv[1]):
        print(f"  {n:4}  {name}")
    print("Efreeti drop placements:")
    for key, n in efreeti_sets.items():
        print(f"  {n:4}  {key}")
    print("island-view groups (gathering rows only — hand-ins are excluded by P5):")
    for heading, n in sorted(headings.items(), key=lambda kv: (kv[1][0], kv[0])):
        print(f"  {n[1]:4}  {heading}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
