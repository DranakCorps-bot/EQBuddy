"""Place the DRA-83 curated GuideAttachment references into the Sky guides.

ONE RUN, and then this script is the record of what it did — the catalog is CURATED and
this file is not part of any refresh. It is committed for the reason a harvest transform is:
"93 attachments appeared in a hand-authored file" is a claim somebody has to be able to
re-derive, and the two rules below are the whole of it.

Rule 1 — GearUpgrade on the TURN-IN step, keyed on the reward ITEM.
    The step hands the player that item, and the Helper's Farm Gear engine is what knows
    whether it beats what they are wearing. The key is the item's own name and NOT the
    objective's `rewardKey` ("Class|Item"): nothing may split a composite key to get at half
    of it (the GuideRowKey rule), and the two strings are different by design.
    Placed only where `ItemCatalog` knows the name. Two do not resolve and they are OUR
    naming bugs rather than gaps in the wiki, so they get no attachment and are named in
    `EpicGuideTests` as committed negatives.

Rule 2 — XpFarm on the open FARM step, keyed on the zone.
    Exactly the steps whose own `who` names no single creature ("any Plane of Sky mob") —
    an evening of killing trash, which is the one shape of step where "what does my own log
    say about levelling here" is the question a player is actually asking. The 127 Loot steps
    name a creature and get nothing: they are one pull, not a camp.

Neither rule invents a sentence. An attachment is `{ kind, key }` and the Helper answers it or
stays silent.
"""
import gzip
import io
import json

CATALOG = "src/EQBuddy.Core/Data/GuideCatalog.json"
ITEMS = "src/EQBuddy.Core/Data/ItemCatalog.json.gz"

# The catalog round-trips byte-identically under these three, which is what makes a targeted
# edit reviewable: the diff is the additions and nothing else.
DUMP = dict(indent=2, ensure_ascii=False)


def fold(name: str) -> str:
    """ItemCatalog.Fold: backticks fold to apostrophes both ways, trimmed."""
    return name.replace("`", "'").strip()


def main() -> int:
    raw = io.open(CATALOG, encoding="utf-8", newline="").read()
    catalog = json.loads(raw)

    items = json.loads(gzip.open(ITEMS, "rb").read().decode("utf-8"))["Items"]
    known = {fold(i["Name"]) for i in items}

    placed = {"GearUpgrade": 0, "XpFarm": 0}
    refused = []
    for guide in catalog["guides"]:
        if guide["guideType"] != "PlaneOfSkyQuest":
            continue
        zones = guide["zoneNames"]
        for stage in guide["stages"]:
            for obj in stage["objectives"]:
                if obj.get("attachments"):
                    continue
                reward = obj.get("rewardKey", "")
                if obj["objectiveType"] == "TurnIn" and reward:
                    item = reward.split("|", 1)[1]
                    if fold(item) not in known:
                        refused.append((guide["id"], item))
                        continue
                    obj["attachments"] = [{"kind": "GearUpgrade", "key": item}]
                    placed["GearUpgrade"] += 1
                elif obj["objectiveType"] == "Farm" and len(zones) == 1:
                    obj["attachments"] = [{"kind": "XpFarm", "key": zones[0]}]
                    placed["XpFarm"] += 1

    out = json.dumps(catalog, **DUMP).replace("\n", "\r\n") + "\r\n"
    io.open(CATALOG, "w", encoding="utf-8", newline="").write(out)
    print("placed:", placed)
    print("refused (our naming bugs):", refused)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
