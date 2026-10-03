"""DRA-754 plan survey: how many Exploration achievement rows name a place EQBuddy ships.

Re-measures DRA-749's Researcher numbers rather than inheriting them. Read-only: it reads
the two committed achievement fixtures and the shipped Core/Data files, writes nothing.

Rules, as the plan states them (and as an engine would have to apply them):
  * A PLACE ROW is a criterion (double-tab sub-row) of an "EverQuest: Exploration"
    achievement whose text ends in " Traveler"; the place is the text before it.
  * Matching is EXACT title (case-insensitive) then ZoneMapFiles.IdentityKey, and nothing
    looser -- the ZoneLevels / ZoneEras rule. No containment, no distance.
  * ItemCatalog DropZones values are admitted to the universe only through
    TradeskillMaterials.IsPlace (ported below).

Run:  python scripts/dra754-exploration-survey.py
"""
import gzip
import json
import os
import re
import sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "src", "EQBuddy.Core", "Data")
FIX = os.path.join(ROOT, "tests", "fixtures", "achievements")


def load(name):
    with open(os.path.join(DATA, name), encoding="utf-8-sig") as f:
        return json.load(f)


# --- ports of the two C# rules the plan names ----------------------------------------
def identity_key(zone):  # ZoneMapFiles.IdentityKey
    z = zone.strip().lower()
    p = z.find("(")
    if p > 0:
        z = z[:p]
    z = re.sub(r"\s+\d+\s*$", "", z)
    if z.startswith("the "):
        z = z[4:]
    z = z.strip()
    return z.replace(" ", "").replace("'", "").replace("-", "")


VAGUE = ["Various Zones", "Unknown", "D3+ Zones", "n/a", "?"]
NOT_A_PLACE = ["}}", ":*", "Category:", "N O T _", "NOT_", "ITEM REMOVED", "<br"]


def is_place(zone):  # TradeskillMaterials.IsPlace
    t = zone.strip()
    if not t:
        return False
    if any(t.lower() == v.lower() for v in VAGUE):
        return False
    if any(t.lower().startswith(p.lower()) for p in NOT_A_PLACE):
        return False
    return t != "/"


# --- the universe, one named source at a time ------------------------------------------
def sources():
    s = {}
    b = load("ZoneLevelBands.json")
    s["ZoneLevelBands"] = set(b["Bands"]) | set(b["NoBand"])
    e = load("ZoneEras.json")
    s["ZoneEras"] = set(e["Eras"]) | set(e["NoEra"])
    g = load("ZoneGraph.json")
    s["ZoneGraph"] = set(g) | {z for adj in g.values() for z in adj}
    sp = load("SpawnCatalog.json")
    s["SpawnCatalog"] = {z[k] for z in sp["zones"] for k in ("zone", "logZoneName") if z.get(k)}
    q = load("QuestCatalog.json")
    qs = set()
    for quest in q["quests"]:
        if quest.get("startZone"):
            qs.add(quest["startZone"])
        for z in quest.get("zones") or []:
            qs.add(z)
    s["QuestCatalog"] = qs
    with gzip.open(os.path.join(DATA, "ItemCatalog.json.gz"), "rt", encoding="utf-8-sig") as f:
        items = json.load(f)["Items"]
    raw = {z for it in items for z in (it.get("DropZones") or [])}
    s["ItemCatalog.DropZones(raw)"] = raw
    s["ItemCatalog.DropZones"] = {z for z in raw if is_place(z)}
    return s


class Index:
    def __init__(self, names):
        self.title = {n.strip().lower(): n for n in names}
        self.key = {}
        for n in names:
            self.key.setdefault(identity_key(n), set()).add(n)

    def resolve(self, place):
        if place.strip().lower() in self.title:
            return "exact"
        if identity_key(place) in self.key:
            return "key"
        return None


# --- the dump ---------------------------------------------------------------------------
def exploration_rows(path):
    rows, section, parent = [], "", None
    with open(path, encoding="utf-8-sig") as f:
        for raw in f:
            line = raw.rstrip()
            if not line:
                continue
            if re.match(r"^[CI]\t\t", line):
                if section == "EverQuest: Exploration":
                    rows.append((parent, line[3:].strip(), line[0] == "C"))
            elif re.match(r"^[CI]\t", line):
                parent = line[2:].strip()
            else:
                section, parent = line.strip(), None
    return rows


def main():
    src = sources()
    universe = set()
    for k, v in src.items():
        if k != "ItemCatalog.DropZones(raw)":
            universe |= v
    print("== universe (distinct names per source)")
    for k, v in src.items():
        print(f"  {k:28} {len(v)}")
    print(f"  {'UNION (IsPlace-filtered)':28} {len(universe)}")
    print(f"  raw DropZones refused by IsPlace: {len(src['ItemCatalog.DropZones(raw)'] - src['ItemCatalog.DropZones'])}")

    idx_all = Index(universe)
    idx_each = {k: Index(v) for k, v in src.items() if k != "ItemCatalog.DropZones(raw)"}
    for fx in sorted(os.listdir(FIX)):
        rows = exploration_rows(os.path.join(FIX, fx))
        places = [(p, t[: -len(" Traveler")], c) for p, t, c in rows if t.endswith(" Traveler")]
        # A standalone "<Place> Traveler" achievement (one "Visit" criterion) names its place
        # in the PARENT; one of them (The Oasis of Marr) is in no regional Explorer.
        places += [(p, p[: -len(" Traveler")], c) for p, t, c in rows
                   if t.startswith("Visit ") and p.endswith(" Traveler")]
        flags = {}
        for _, pl, c in places:
            flags.setdefault(pl, set()).add(c)
        disagree = sum(1 for v in flags.values() if len(v) > 1)
        other = [t for p, t, c in rows if not t.endswith(" Traveler")]
        res = Counter(idx_all.resolve(pl) for _, pl, _ in places)
        print(f"\n== {fx}: Exploration criteria {len(rows)}, place rows {len(places)}, "
              f"non-place criteria {len(other)}, complete {sum(c for *_, c in places)}")
        print(f"  union: exact {res['exact']}, key {res['key']}, unresolved {res[None]} "
              f"-> {100 * (res['exact'] + res['key']) / len(places):.1f}%")
        for k, ix in idx_each.items():
            n = sum(1 for _, pl, _ in places if ix.resolve(pl))
            print(f"  {k:28} {n}")
        incomplete = [pl for _, pl, c in places if not c]
        inc_res = sum(1 for pl in incomplete if idx_all.resolve(pl))
        print(f"  INCOMPLETE place rows {len(incomplete)}, resolved {inc_res}")
        g = idx_each["ZoneGraph"]
        print(f"  routable (ZoneGraph exact/key) {sum(1 for _, pl, _ in places if g.resolve(pl))}")
        # The section names most places TWICE: as a criterion of a regional "Explorer"
        # achievement and as its own "<Place> Traveler" achievement with one "Visit" row.
        # The unit an engine ranks is the distinct PLACE.
        distinct = {pl for _, pl, _ in places}
        instanced = {pl for pl in distinct if re.match(r"^(House|Guild Hall|Guild Lobby|Wedding Chapel)\b", pl)}
        world = distinct - instanced
        open_world = {pl for _, pl, c in places if not c} & world
        print(f"  DISTINCT places {len(distinct)}: player-instanced {len(instanced)}, world {len(world)}, "
              f"world resolved {sum(1 for pl in world if idx_all.resolve(pl))}, "
              f"routable {sum(1 for pl in world if g.resolve(pl))}, "
              f"banded {sum(1 for pl in world if idx_each['ZoneLevelBands'].resolve(pl))}, "
              f"era'd {sum(1 for pl in world if idx_each['ZoneEras'].resolve(pl))}")
        print(f"  world places still OPEN for this character {len(open_world)}, "
              f"resolved {sum(1 for pl in open_world if idx_all.resolve(pl))}, "
              f"routable {sum(1 for pl in open_world if g.resolve(pl))}")
        print(f"  world UNRESOLVED: {sorted(pl for pl in world if not idx_all.resolve(pl))}")
        print(f"  places whose rows DISAGREE on completion: {disagree}")
        standalone = sum(1 for p, t, c in rows if t.startswith("Visit "))
        print(f"  standalone '<Place> Traveler' achievements (one 'Visit' row each): {standalone}")
        if fx.startswith("averaj"):
            parents = Counter(p for p, _, _ in places)
            print("  parents:", dict(parents))
            print("  non-place criteria sample:", other[:8])
            print("  UNRESOLVED:", sorted({pl for _, pl, _ in places if not idx_all.resolve(pl)}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
