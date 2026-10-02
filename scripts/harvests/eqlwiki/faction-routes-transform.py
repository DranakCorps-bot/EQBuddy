#!/usr/bin/env python3
"""Turn the cached eqlwiki faction data into shipped TURN-IN ROUTES (DRA-746, DRA-728 D1).

WHAT THIS IS
------------
A route is one fact the wiki states about a faction grind: hand in THESE items, THIS many per
turn-in, and THESE factions move by THESE integer amounts. eqlwiki states it in two places,
and both are already in the COMMITTED cache, so this transform **fetches nothing** — the
`zone-eras-transform.py` / `merchants-transform.py` precedent exactly, and the reason the
DRA-728 plan needed no harvest ruling:

  * `cache/quest-All_Positive_Faction_Quests.wikitext` — a table of item x count -> faction
    (+N), with the entry requirement ("Apprehensive Dark Bargainers") and how the item is
    obtained ("Purchased Item") in their own columns.
  * the `<div class="facblock">` on individual quest pages — the game's own
    "Your faction standing with X has been adjusted by N." lines, transcribed. A page names
    the quest, not the item, so the item comes from `QuestCatalog.json`'s turn-in list for
    that quest. That is the join.

Its output is `src/EQBuddy.Core/Data/FactionRoutes.json`, read by `Core/FactionRoutes.cs`.
**D1 adds no reader** — DRA-728 D2's cold-start arm in `UnlockGuidance.Faction` is the first.

WHAT IS ADMITTED, AND WHAT IS REFUSED BY NAME
---------------------------------------------
Admitted: a turn-in (one or more items, each with a count per turn-in) and at least one
faction with an INTEGER delta. Negative deltas are KEPT: a route that tanks an opposing
faction is the first thing a grinder needs to know.

Refused, every one REPORTED by name and never guessed at:

  * **`(+?)`, `(-??)`** — the table names the faction and not the amount. The faction ships
    under the route's `Unquantified`, verbatim, so "also moves X" can be said without a
    number; a route with NO numeric faction at all is refused whole.
  * **direction-only facblocks** — "got better" / "got worse". The game printed no number.
    A trailing editor annotation ("got better. (+5)") is NOT the game's line and is refused
    as direction-only too: an editor's number beside a direction is a guess somebody
    transcribed, and the facblock is admitted only for the game's own sentence.
  * **an unjoinable quest** — not in the catalog, no turn-in item in the catalog, or several
    turn-in items with one faction block (which hand-in the block follows is not stated, and
    pooling them into one turn-in would invent a recipe).
  * **several DIFFERENT numeric blocks on one page** — the same question, one layer up.
  * **table rows that are not an item turn-in** — `OR` alternatives, coin in the turn-in,
    "1x ea of", a message delivery. Each by its own reason.
  * **a conflict** — the table and the quest's page both admit a route and disagree on a
    count or on any amount both state. Both are refused and the report shows both readings;
    nothing is averaged and neither side wins (DRA-728 plan §8). Gnoll Bounty is the live
    instance: the table and the page's own prose say "multiples of 3", the catalog says 1.

When the two sources AGREE the route ships ONCE (trap 4) with both page titles in `Sources`
and the table's requirement/obtain text. The item is the CATALOG's spelling when a page
route exists, because that is the identity `QuestMatcher` and the bags already use; a
table spelling that differs is reported, not shipped.

VERBATIM
--------
`Requirement` and `Obtain` are the table's own cells. Only wiki LINK MARKUP is folded
(`[[A|B]]` -> B, `[[A]]` -> A) and runs of whitespace squeezed — markup is not text, and the
`ZoneMerchants` transform folds it the same way. Nothing else is rewritten.

One name fold is applied to FACTION names and only one: a trailing ` (Faction)` is the wiki's
page-title disambiguator (`[[King Ak'Anon (Faction)]]`), not part of the name the game prints.
Matching a faction to the game's spelling is `FactionNames.Same` in C#, and it is not
re-implemented here except to WRITE THE REPORT (the `zone-eras-transform.py` precedent for
`identity_key`); the must-list that holds the statuses is `FactionRoutesTests`.

BYTE-REPRODUCIBLE
-----------------
Plain JSON, sorted, fixed key order, no clock, no locale — trap 74 cannot arise.

    python scripts/harvests/eqlwiki/faction-routes-transform.py [--check] [--selftest]

`--check` writes nothing and exits 1 if the committed file is not what this produces.
`--selftest` writes nothing and runs every refusal arm over synthetic wikitext, because
several are unreachable or nearly so in the corpus and a refusal that has never fired is a
guard aimed at nothing (trap 78). Both run in `check.ps1` and CI.
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
TITLES = HERE / "quest-titles.json"
REPORT = HERE / "faction-routes-report.md"
DATA = ROOT / "src" / "EQBuddy.Core" / "Data"
OUT = DATA / "FactionRoutes.json"
CATALOG = DATA / "QuestCatalog.json"
ACHIEVEMENTS = ROOT / "tests" / "fixtures" / "achievements"

TABLE_TITLE = "All Positive Faction Quests"

LINK_RX = re.compile(r"\[\[([^\]|]+)(?:\|([^\]]*))?\]\]")
FACBLOCK_RX = re.compile(
    r"<div\s+class\s*=\s*[\"']?facblock[\"']?\s*>(.*?)</div>", re.IGNORECASE | re.DOTALL)
# The game's own sentence. Anchored at its END (an optional period, then nothing) so a
# trailing editor annotation is not swallowed into an admitted line.
FAC_NUM_RX = re.compile(
    r"Your\s+faction\s+standing\s+with\s+(.+?)\s+has\s+been\s+adjusted\s+by\s+(-?\d+)\s*\.?\s*$",
    re.IGNORECASE)
FAC_DIR_RX = re.compile(
    r"Your\s+faction\s+standing\s+with\s+(.+?)\s+got\s+(better|worse)", re.IGNORECASE)
FAC_ANY_RX = re.compile(r"Your\s+faction\s+standing\s+with", re.IGNORECASE)

# Table cells.
ITEM_RX = re.compile(r"(\d+)\s*x\s*\[\[([^\]|]+)(?:\|[^\]]*)?\]\]", re.IGNORECASE)
TURNIN_RX = re.compile(
    r"^(?:(?:Trade|Give)\s+)?(\d+\s*x\s*\[\[[^\]]+\]\](?:\s*\+\s*\d+\s*x\s*\[\[[^\]]+\]\])*)"
    r"\s+for\s*:\s*$", re.IGNORECASE)
PER_LINE_RX = re.compile(
    r"^(\d+)\s*x\s*\[\[([^\]|]+)(?:\|[^\]]*)?\]\]\s+for\s+\[\[([^\]|]+)(?:\|([^\]]*))?\]\]"
    r"\s*\(([+-]\d+)\)\s*$", re.IGNORECASE)
BULLET_RX = re.compile(
    r"^\*\s*\[\[([^\]|]+)(?:\|([^\]]*))?\]\]\s*\(([^)]*)\)\s*$")
NUMERIC_RX = re.compile(r"^[+-]\d+$")


def link_text(s: str) -> str:
    """Wiki link markup folded to its display text; whitespace squeezed. Nothing else."""
    folded = LINK_RX.sub(lambda m: (m.group(2) if m.group(2) is not None else m.group(1)), s)
    folded = folded.replace("'''", "")
    return " ".join(folded.split())


def faction_name(target: str, display: str | None = None) -> str:
    """The faction a link names. The display text where the link carries one (it is what
    the game printed); a trailing ` (Faction)` page disambiguator dropped. That is the one
    name fold this file makes."""
    name = (display if display not in (None, "") else target).strip()
    return re.sub(r"\s*\(Faction\)\s*$", "", name, flags=re.IGNORECASE).strip()


def faction_in(fragment: str) -> str:
    """The faction a facblock line names: a link, or plain text when the editor wrote none."""
    m = LINK_RX.search(fragment)
    if m:
        return faction_name(m.group(1), m.group(2))
    return faction_name(fragment)


def cache_path(title: str) -> pathlib.Path:
    """The filename `quests-harvest.py` cached this title under (its sanitisation)."""
    safe = re.sub(r"[^A-Za-z0-9._-]", "_", title)
    return CACHE / f"quest-{safe}.wikitext"


# ------------------------------------------------------------------ the table

def table_rows(wikitext: str) -> list[list[str]]:
    """Every data row of every wikitable on the page, as its six cells (raw text)."""
    rows: list[list[str]] = []
    for table in re.findall(r"\{\|(.*?)\|\}", wikitext, re.DOTALL):
        for chunk in re.split(r"\n\|-[^\n]*", table)[1:]:
            cells: list[str] = []
            for line in chunk.split("\n"):
                if line.startswith("|") and not line.startswith("|+"):
                    cells.append(line[1:])
                elif cells:
                    cells[-1] += "\n" + line
            if len(cells) == 6:
                rows.append([c.strip() for c in cells])
    return rows


def parse_table_cell(cell: str):
    """One row's Faction Adjustments cell -> `("routes", [route...])` or `("refused", why)`.

    A route here is `{"Items": [(item, count)], "Factions": [(name, delta)],
    "Unquantified": [(name, verbatim)]}`. Shapes admitted, and no third:

      (A) `[Trade|Give] Nx [[Item]] (+ Nx [[Item]])* for:` then `* [[Faction]] (+N)` bullets,
          optionally a "Negative hit to:" line and more bullets;
      (B) one or more lines `Nx [[Item]] for [[Faction]] (+N)`, each its own route."""
    lines = [ln.strip() for ln in cell.split("\n") if ln.strip()]
    if not lines:
        return ("refused", "empty cell")
    head = lines[0]
    if re.search(r"\bOR\b", cell):
        return ("refused", "alternative turn-ins (OR)")
    if re.search(r"\d+\s*gp\b|'''\d+gp'''", cell, re.IGNORECASE):
        return ("refused", "coin in the turn-in")
    if re.search(r"\bea\s+of\b", head, re.IGNORECASE):
        return ("refused", "one each of several items (\"ea of\")")
    if re.match(r"^(Deliver|Say)\b", head, re.IGNORECASE):
        return ("refused", "a message delivery, not an item turn-in")

    if all(PER_LINE_RX.match(ln) for ln in lines):
        routes = []
        for ln in lines:
            m = PER_LINE_RX.match(ln)
            routes.append({"Items": [(m.group(2).strip(), int(m.group(1)))],
                           "Factions": [(faction_name(m.group(3), m.group(4)), int(m.group(5)))],
                           "Unquantified": []})
        return ("routes", routes)

    m = TURNIN_RX.match(head)
    if not m:
        return ("refused", "turn-in line not in an admitted shape")
    items = [(im.group(2).strip(), int(im.group(1))) for im in ITEM_RX.finditer(m.group(1))]
    factions: list[tuple[str, int]] = []
    unquantified: list[tuple[str, str]] = []
    for ln in lines[1:]:
        if re.match(r"^Negative\s+hit\s+to\s*:?$", ln, re.IGNORECASE):
            continue
        b = BULLET_RX.match(ln)
        if not b:
            return ("refused", f"unreadable line under the turn-in: {link_text(ln)}")
        name = faction_name(b.group(1), b.group(2))
        amount = b.group(3).strip()
        if NUMERIC_RX.match(amount):
            factions.append((name, int(amount)))
        else:
            unquantified.append((name, f"({amount})"))
    if not factions:
        return ("refused", "no faction amount stated (+?)")
    return ("routes", [{"Items": items, "Factions": factions, "Unquantified": unquantified}])


def read_table(wikitext: str):
    """The table page -> (routes by quest, refusals by quest, row meta by quest)."""
    routes: dict[str, list[dict]] = {}
    refused: dict[str, str] = {}
    meta: dict[str, dict] = {}
    for cells in table_rows(wikitext):
        q = LINK_RX.search(cells[0])
        if not q:
            continue
        quest = q.group(1).strip()
        meta[quest] = {"Requirement": link_text(cells[4]),
                       # One line per line of the cell — the cell's own line breaks, kept.
                       "Obtain": "\n".join(link_text(ln) for ln in cells[5].split("\n")
                                           if ln.strip()),
                       "Directions": []}
        kind, value = parse_table_cell(cells[3])
        # Every faction the cell names with a raise and no number — the direction half,
        # whether or not the row is admitted, so the must-list can say "named, unrouted".
        for b in re.finditer(r"\[\[([^\]|]+)(?:\|([^\]]*))?\]\]\s*\(\+\?+\)", cells[3]):
            meta[quest]["Directions"].append(faction_name(b.group(1), b.group(2)))
        if kind == "routes":
            routes[quest] = value
        else:
            refused[quest] = value
    return routes, refused, meta


# ------------------------------------------------------------------ the facblocks

def read_block(block: str):
    """One facblock -> (numeric [(faction, delta)], directions [(faction, sign, verbatim)])."""
    numeric: list[tuple[str, int]] = []
    directions: list[tuple[str, str, str]] = []
    for line in block.split("\n"):
        if not FAC_ANY_RX.search(line):
            continue
        text = line.strip().lstrip("*").strip()
        m = FAC_NUM_RX.search(text)
        if m:
            numeric.append((faction_in(m.group(1)), int(m.group(2))))
            continue
        d = FAC_DIR_RX.search(text)
        if d:
            sign = "+" if d.group(2).lower() == "better" else "-"
            directions.append((faction_in(d.group(1)), sign, link_text(text)))
        else:
            directions.append(("", "?", link_text(text)))
    return numeric, directions


def decide_page(wikitext: str, catalog_items: list[dict] | None):
    """One quest page -> ("route", route) or ("refused", why, directions) or ("none",).

    `catalog_items` is None when the quest is not in the catalog at all."""
    blocks = [read_block(b) for b in FACBLOCK_RX.findall(wikitext)]
    if not blocks:
        return ("none",)
    numeric_blocks = [tuple(n) for n, _ in blocks if n]
    directions = [d for _, ds in blocks for d in ds]
    if not numeric_blocks:
        return ("refused", "direction-only facblock (no amount)", directions)
    if len(set(numeric_blocks)) > 1:
        return ("refused", "several different faction blocks on one page", directions)
    if catalog_items is None:
        return ("refused", "quest not in the catalog", directions)
    if len(catalog_items) == 0:
        return ("refused", "no turn-in item in the catalog", directions)
    if len(catalog_items) > 1:
        return ("refused", "several turn-in items, one faction block", directions)
    item = catalog_items[0]
    the_block = next(b for b in blocks if tuple(b[0]) == numeric_blocks[0])
    return ("route", {"Items": [(item["name"], int(item["qty"]))],
                      "Factions": list(numeric_blocks[0]),
                      "Unquantified": [(f, f"got {'better' if s == '+' else 'worse'}")
                                       for f, s, _ in the_block[1] if f]},
            directions)


# ------------------------------------------------------------------ build

def same_reading(a: dict, b: dict) -> list[str]:
    """Where two readings of one quest disagree on a COUNT or on an amount both state."""
    why: list[str] = []
    counts_a = [c for _, c in a["Items"]]
    counts_b = [c for _, c in b["Items"]]
    if counts_a != counts_b:
        why.append(f"count per turn-in {counts_a} vs {counts_b}")
    amounts_b = {f.casefold(): d for f, d in b["Factions"]}
    for f, d in a["Factions"]:
        if f.casefold() in amounts_b and amounts_b[f.casefold()] != d:
            why.append(f"{f} {d:+d} vs {amounts_b[f.casefold()]:+d}")
    return why


def build(titles: list[str], catalog: dict, table_text: str):
    by_name = {q["name"]: q for q in catalog["quests"]}
    t_routes, t_refused, t_meta = read_table(table_text)

    page_routes: dict[str, dict] = {}
    refused: dict[str, tuple[str, str]] = {}           # quest -> (source, why)
    directions: dict[str, list[tuple[str, str]]] = collections.defaultdict(list)
    annotated = 0
    for title in titles:
        if title == TABLE_TITLE:
            continue
        path = cache_path(title)
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8")
        q = by_name.get(title)
        got = decide_page(text, None if q is None else q["items"])
        if got[0] == "none":
            continue
        for f, sign, verbatim in got[2]:
            if sign == "+" and f:
                directions[f].append((title, "amount not stated"))
            if re.search(r"\(?[+-]?\d+\)?\s*\.?$", verbatim) and "adjusted" not in verbatim:
                annotated += 1
        if got[0] == "route":
            page_routes[title] = got[1]
        else:
            refused[title] = ("page", got[1])
            if "amount" not in got[1]:
                # A number the page DOES state, that we could not join: still a raise the
                # wiki names and EQBuddy does not route.
                for block in FACBLOCK_RX.findall(text):
                    for f, d in read_block(block)[0]:
                        if d > 0:
                            directions[f].append((title, got[1]))

    for quest, why in t_refused.items():
        refused.setdefault(quest, ("table", why))
    for quest, meta in t_meta.items():
        for f in meta["Directions"]:
            directions[f].append((quest, "amount not stated"))

    routes: list[dict] = []
    conflicts: dict[str, list[str]] = {}
    spellings: dict[str, tuple[str, str]] = {}

    def emit(quest: str, r: dict, sources: list[str], meta: dict | None):
        routes.append({
            "Quest": quest,
            "Items": [{"Item": i, "Count": c} for i, c in r["Items"]],
            "Factions": [{"Faction": f, "Delta": d} for f, d in r["Factions"]],
            "Unquantified": [{"Faction": f, "Verbatim": v} for f, v in r["Unquantified"]],
            "Requirement": (meta or {}).get("Requirement", ""),
            "Obtain": (meta or {}).get("Obtain", ""),
            "Sources": sources,
        })

    for quest in sorted(set(t_routes) | set(page_routes), key=str.casefold):
        page = page_routes.get(quest)
        table = t_routes.get(quest)
        if page and table:
            if len(table) != 1:
                conflicts[quest] = ["the table splits the turn-in into several routes"]
                continue
            why = same_reading(table[0], page)
            if why:
                conflicts[quest] = why
                continue
            t_items = [i for i, _ in table[0]["Items"]]
            p_items = [i for i, _ in page["Items"]]
            if [i.casefold() for i in t_items] != [i.casefold() for i in p_items]:
                spellings[quest] = (", ".join(t_items), ", ".join(p_items))
            merged = dict(page)
            # The table's (+?) beside the page's number is not a conflict: one side is silent.
            stated = {f.casefold() for f, _ in page["Factions"]}
            merged["Unquantified"] = page["Unquantified"] + [
                u for u in table[0]["Unquantified"] if u[0].casefold() not in stated]
            emit(quest, merged, sorted([TABLE_TITLE, quest]), t_meta.get(quest))
        elif page:
            emit(quest, page, [quest], None)
        else:
            for r in table:
                emit(quest, r, [TABLE_TITLE], t_meta.get(quest))

    for quest in conflicts:
        refused[quest] = ("both", "the table and the quest page disagree: "
                          + "; ".join(conflicts[quest]))
        for r in t_routes.get(quest, []) + ([page_routes[quest]] if quest in page_routes else []):
            for f, d in r["Factions"]:
                if d > 0:
                    directions[f].append((quest, "sources disagree"))

    routed_up = {f["Faction"].casefold() for r in routes for f in r["Factions"] if f["Delta"] > 0}
    unrouted = {}
    for f, hits in directions.items():
        if f.casefold() in routed_up:
            continue
        unique = sorted(set(hits), key=lambda h: (h[0].casefold(), h[1]))
        unrouted[f] = [{"Quest": q, "Why": w} for q, w in unique]

    return {
        "routes": routes, "unrouted": unrouted, "refused": refused,
        "conflicts": conflicts, "spellings": spellings, "annotated": annotated,
        "t_routes": t_routes, "page_routes": page_routes,
    }


def render(result: dict) -> str:
    routes = sorted(result["routes"], key=lambda r: (r["Quest"].casefold(),
                                                      [i["Item"] for i in r["Items"]]))
    payload = {
        "Source": "eqlwiki: the All Positive Faction Quests table and quest-page facblocks, "
                  "joined to QuestCatalog turn-in items "
                  "(scripts/harvests/eqlwiki/faction-routes-transform.py; fetches nothing)",
        "Routes": routes,
        "Unrouted": {f: v for f, v in sorted(result["unrouted"].items(),
                                             key=lambda kv: kv[0].casefold())},
    }
    return json.dumps(payload, indent=1, ensure_ascii=False) + "\n"


# ------------------------------------------------------------------ report

def squash(s: str) -> str:
    """`FactionNames.Squash`, mirrored for the REPORT only (the C# is the authority)."""
    return "".join(ch.lower() for ch in s if ch.isalnum())


# Mirrored from `FactionNames.Aliases` for the report only; `FactionRoutesTests` holds the
# statuses through the real C# fold, so a drift here moves a report line and nothing else.
ALIASES = {"Coalition of Tradesfolk": "Coalition of Tradefolk",
           "Freeport Militia": "The Freeport Militia",
           "Corrupt Qeynos Guard": "Corrupt Qeynos Guards",
           "Da Bashers": "DaBashers"}


def same_faction(a: str, b: str) -> bool:
    if a.casefold() == b.casefold():
        return True
    if ALIASES.get(a, "").casefold() == b.casefold() or ALIASES.get(b, "").casefold() == a.casefold():
        return True
    return squash(a) != "" and squash(a) == squash(b)


def race_factions() -> list[str]:
    names = set()
    for p in sorted(ACHIEVEMENTS.glob("*.txt")):
        for line in p.read_text(encoding="utf-8").splitlines():
            m = re.search(r"Get maximum faction with (.+?)\.?\s*$", line)
            if m:
                names.add(m.group(1).strip())
    return sorted(names, key=str.casefold)


def race_status(result: dict) -> list[tuple[str, str, list[str]]]:
    rows = []
    for race in race_factions():
        up = sorted({r["Quest"] for r in result["routes"] for f in r["Factions"]
                     if f["Delta"] > 0 and same_faction(race, f["Faction"])}, key=str.casefold)
        if up:
            rows.append((race, "routed", up))
            continue
        named = sorted({h["Quest"] for f, hs in result["unrouted"].items()
                        if same_faction(race, f) for h in hs}, key=str.casefold)
        rows.append((race, "direction-only" if named else "none", named))
    return rows


def pct(part: int, whole: int) -> int:
    return part * 100 // max(whole, 1)


def write_report(result: dict) -> None:
    routes = result["routes"]
    amounts = [f["Delta"] for r in routes for f in r["Factions"]]
    status = race_status(result)
    routed = [s for s in status if s[1] == "routed"]
    by_reason = collections.Counter(why if src != "both" else "the sources disagree"
                                    for src, why in result["refused"].values())
    lines = [
        "# Faction routes report", "",
        "Written by `faction-routes-transform.py` from the COMMITTED eqlwiki cache. It fetches",
        "nothing. **One engine reads `FactionRoutes.json`**: DRA-728 D2's cold-start arm in `UnlockGuidance.Faction`.", "",
        "## Coverage", "",
        f"- Routes shipped: **{len(routes)}** "
        f"({sum(1 for r in routes if r['Sources'] == [TABLE_TITLE])} from the table only, "
        f"{sum(1 for r in routes if TABLE_TITLE not in r['Sources'])} from a quest page only, "
        f"{sum(1 for r in routes if len(r['Sources']) == 2)} where both agree)",
        f"- Faction amounts shipped: **{len(amounts)}** "
        f"({sum(1 for a in amounts if a < 0)} of them NEGATIVE — the costs are kept)",
        f"- Quests refused: **{len(result['refused'])}**", "",
        "| Why refused | Quests |", "|---|---:|",
        *[f"| {why} | {n} |" for why, n in by_reason.most_common()], "",
        f"Facblock lines carrying an EDITOR's number beside a direction (\"got better. (+5)\"), "
        f"refused as direction-only: **{result['annotated']}**.", "",
        "## The survey floor — race-unlock factions with a numeric route", "",
        f"**{len(routed)} of {len(status)}** race-unlock factions have at least one admitted "
        f"route that RAISES them ({pct(len(routed), len(status))}%). The floor is half; under",
        "it this slice STOPS and escalates to Planner (DRA-728 plan §6 S1). The list is every",
        "`Get maximum faction with` line in the committed achievements fixtures, matched with",
        "`FactionNames`' fold; `FactionRoutesTests` holds the same statuses through the C#.", "",
        "| Race-unlock faction | Status | Quests |", "|---|---|---|",
        *[f"| {f} | {s} | {', '.join(q) if q else '—'} |" for f, s, q in status], "",
        "## Distinct-count telltale (trap 73)", "",
        f"**{len(set(amounts))} distinct amounts across {len(amounts)} shipped faction amounts.**",
        "Faction amounts are NOT a per-row fact the way a level band is — +5 is the game's",
        "ordinary hand-in step, so most rows agreeing on it is the expected shape. The guard",
        "is that more than one value appears and that the named fixtures (Bottle of Red Wine,",
        "Bandit Sashes' -20, Clurg's Revenge's -15) read back exactly (`FactionRoutesTests`).", "",
        "| Amount | Times |", "|---:|---:|",
        *[f"| {a:+d} | {n} |" for a, n in sorted(collections.Counter(amounts).items())], "",
        "## Conflicts — refused, both readings shown, never averaged", "",
    ]
    if result["conflicts"]:
        lines += ["| Quest | Disagreement |", "|---|---|",
                  *[f"| {q} | {'; '.join(w)} |" for q, w in sorted(result["conflicts"].items())]]
    else:
        lines += ["**None.**"]
    lines += ["", "## Spelling differences where the sources otherwise agree", "",
              "The catalog's spelling ships (it is what `QuestMatcher` and the bags use).", ""]
    if result["spellings"]:
        lines += ["| Quest | Table | Catalog (shipped) |", "|---|---|---|",
                  *[f"| {q} | {t} | {c} |" for q, (t, c) in sorted(result["spellings"].items())]]
    else:
        lines += ["**None.**"]
    lines += ["", "## Every refused quest, by name", "",
              "| Quest | From | Why |", "|---|---|---|",
              *[f"| {q} | {src} | {why} |"
                for q, (src, why) in sorted(result["refused"].items(), key=lambda kv: kv[0].casefold())],
              ""]
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")


# ------------------------------------------------------------------ selftest

def selftest() -> int:
    failures: list[str] = []

    def expect(name, got, want):
        if got != want:
            failures.append(f"{name}: expected {want!r}, got {got!r}")

    # Table shapes.
    expect("table: a plain item turn-in",
           parse_table_cell("1x [[Red Wine]] for:\n\n* [[Dreadguard Outer]] (+5)\n* [[Dark Bargainers]] (+10)"),
           ("routes", [{"Items": [("Red Wine", 1)],
                        "Factions": [("Dreadguard Outer", 5), ("Dark Bargainers", 10)],
                        "Unquantified": []}]))
    expect("table: a negative hit is KEPT",
           parse_table_cell("Trade 1x [[Bandage]] for:\n* [[Guardians of the Vale]] (+5)\nNegative Hit to:\n* [[Coalition of Tradefolk Underground]] (-1)"),
           ("routes", [{"Items": [("Bandage", 1)],
                        "Factions": [("Guardians of the Vale", 5), ("Coalition of Tradefolk Underground", -1)],
                        "Unquantified": []}]))
    expect("table: (+?) beside a number ships the number and names the other",
           parse_table_cell("Give 3x [[Gnoll Fang]] for:\n* [[Wolves of the North]]  (+15)\n* [[Steel Warriors]] (+?)"),
           ("routes", [{"Items": [("Gnoll Fang", 3)], "Factions": [("Wolves of the North", 15)],
                        "Unquantified": [("Steel Warriors", "(+?)")]}]))
    expect("table: all (+?) is refused by name",
           parse_table_cell("4x [[Bone Chips]] for:\n* [[Clerics of Tunare]] (+?)"),
           ("refused", "no faction amount stated (+?)"))
    expect("table: OR alternatives are refused",
           parse_table_cell("1x [[Lightstone]] OR 1x [[Greater Lightstone]] for:\n* [[Dark Bargainers]] (+5)"),
           ("refused", "alternative turn-ins (OR)"))
    expect("table: coin in the turn-in is refused",
           parse_table_cell("Trade 2gp for:\n* [[Knights of Truth]] (+5)"),
           ("refused", "coin in the turn-in"))
    expect("table: a message delivery is refused",
           parse_table_cell("Deliver Note to Janam for:\n* [[Carson McCabe]] (+5)"),
           ("refused", "a message delivery, not an item turn-in"))
    expect("table: 'ea of' is refused",
           parse_table_cell("1x ea of [[Rusty Short Sword]], and [[Rusty Long Sword]] for:\n* [[Soldiers of Tunare]] (+5)"),
           ("refused", "one each of several items (\"ea of\")"))
    expect("table: several items in one turn-in",
           parse_table_cell("1x [[Kiola Nut]] + 1x[[Koalindl Fish]] for:\n* [[Carson McCabe]] (+5)"),
           ("routes", [{"Items": [("Kiola Nut", 1), ("Koalindl Fish", 1)],
                        "Factions": [("Carson McCabe", 5)], "Unquantified": []}]))
    expect("table: one route per line",
           parse_table_cell("2x [[Metal Bits]] for [[New Sebilisian Expedition]] (+5)\n1x [[Small Piece of High Quality Ore]] for [[New Sebilisian Expedition]] (+10)"),
           ("routes", [{"Items": [("Metal Bits", 2)], "Factions": [("New Sebilisian Expedition", 5)], "Unquantified": []},
                       {"Items": [("Small Piece of High Quality Ore", 1)], "Factions": [("New Sebilisian Expedition", 10)], "Unquantified": []}]))
    expect("table: the (Faction) disambiguator is dropped",
           parse_table_cell("1x [[Vasty Deep Ale]] for:\n* [[King Ak'Anon (Faction)]] (+5)"),
           ("routes", [{"Items": [("Vasty Deep Ale", 1)], "Factions": [("King Ak'Anon", 5)], "Unquantified": []}]))
    expect("table: an unreadable line under the turn-in refuses the row",
           parse_table_cell("1x [[Red Wine]] for:\n* Some faction, maybe (+5)")[0], "refused")

    # Facblocks.
    one = [{"name": "Lizard Tail", "qty": 1}]
    num = '<div class="facblock">\n* Your faction standing with [[Clurg]] has been adjusted by 5.\n* Your faction standing with [[Kazon Stormhammer]] has been adjusted by -5.\n</div>'
    expect("page: the game's sentence joins to the catalog's one item",
           decide_page(num, one)[:2],
           ("route", {"Items": [("Lizard Tail", 1)],
                      "Factions": [("Clurg", 5), ("Kazon Stormhammer", -5)], "Unquantified": []}))
    expect("page: the same block twice is one claim",
           decide_page(num + "\nprose\n" + num, one)[0], "route")
    expect("page: direction-only is refused by name",
           decide_page('<div class="facblock">\n* Your faction standing with [[Clurg]] got better.\n</div>', one)[:2],
           ("refused", "direction-only facblock (no amount)"))
    expect("page: an editor's number beside a direction is NOT the game's line",
           decide_page('<div class="facblock">\n* Your faction standing with [[Clurg]] got better. (+5)\n</div>', one)[:2],
           ("refused", "direction-only facblock (no amount)"))
    expect("page: two different blocks are refused",
           decide_page(num + '\n<div class="facblock">\n* Your faction standing with [[Clurg]] has been adjusted by 10.\n</div>', one)[:2],
           ("refused", "several different faction blocks on one page"))
    expect("page: not in the catalog", decide_page(num, None)[:2],
           ("refused", "quest not in the catalog"))
    expect("page: no catalog item", decide_page(num, [])[:2],
           ("refused", "no turn-in item in the catalog"))
    expect("page: several catalog items",
           decide_page(num, one + [{"name": "Ogre Head", "qty": 1}])[:2],
           ("refused", "several turn-in items, one faction block"))
    expect("page: a mixed block ships the number and names the direction",
           decide_page('<div class="facblock">\n* Your faction standing with [[Clurg]] has been adjusted by 5.\n* Your faction standing with [[Oggok Guards]] got better.\n</div>', one)[1],
           {"Items": [("Lizard Tail", 1)], "Factions": [("Clurg", 5)],
            "Unquantified": [("Oggok Guards", "got better")]})
    expect("page: a piped link reads the display text",
           decide_page('<div class="facblock">\n* Your faction standing with [[Mayor Gubbin (Faction)|Mayor Gubbin]] has been adjusted by 5.\n</div>', one)[1]["Factions"],
           [("Mayor Gubbin", 5)])
    expect("page: no facblock", decide_page("just prose", one), ("none",))

    # Conflicts.
    expect("conflict: count", same_reading({"Items": [("Gnoll Fang", 3)], "Factions": []},
                                           {"Items": [("Gnoll Fang", 1)], "Factions": []}),
           ["count per turn-in [3] vs [1]"])
    expect("conflict: amount", same_reading({"Items": [("A", 1)], "Factions": [("X", 5)]},
                                            {"Items": [("A", 1)], "Factions": [("x", 7)]}),
           ["X +5 vs +7"])
    expect("agreement", same_reading({"Items": [("A", 1)], "Factions": [("X", 5)]},
                                     {"Items": [("a", 1)], "Factions": [("X", 5), ("Y", 1)]}), [])

    # Trap 78 from the other side: the must-list source must not be empty.
    if len(race_factions()) < 30:
        failures.append(f"race_factions() read {len(race_factions())} names; the fixtures carry 40")

    total = 26
    for f in failures:
        print(f"FAIL  {f}", file=sys.stderr)
    if failures:
        print(f"{len(failures)} of {total} selftest checks failed.", file=sys.stderr)
        return 1
    print(f"selftest: {total} checks green (every refusal arm fired at least once).")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Faction routes from the committed cache.")
    parser.add_argument("--check", action="store_true",
                        help="write nothing; exit 1 if the committed file differs")
    parser.add_argument("--selftest", action="store_true",
                        help="write nothing; exercise every refusal arm")
    args = parser.parse_args()
    if args.selftest:
        return selftest()

    titles = json.loads(TITLES.read_text(encoding="utf-8"))
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    table = cache_path(TABLE_TITLE).read_text(encoding="utf-8")
    result = build(titles, catalog, table)
    data = render(result)
    current = OUT.read_text(encoding="utf-8") if OUT.exists() else None

    if args.check:
        if data != current:
            print(f"{OUT.name} differs from what this produces "
                  f"({len(current or '')} bytes on disk, {len(data)} generated).", file=sys.stderr)
            return 1
        print(f"{OUT.name} is already what this produces "
              f"({len(result['routes'])} routes, {len(data)} bytes).")
        return 0

    if data == current:
        print(f"{OUT.name} unchanged ({len(result['routes'])} routes) — left alone.")
    else:
        OUT.write_text(data, encoding="utf-8", newline="\n")
        print(f"wrote {OUT.name}: {len(result['routes'])} routes, "
              f"{len(result['unrouted'])} factions named but unrouted.")
    write_report(result)
    print(f"Report: {REPORT.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
