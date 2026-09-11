#!/usr/bin/env python3
"""Turn the cached quest wikitext into `HarvestedGuides.json.gz` (DRA-45, Delivery 2 N1).

WHAT THIS IS
------------
A deterministic transformer. It runs inside the weekly refresh, AFTER `quests-promote.py`
has written `QuestCatalog.json`, over the wikitext already in `cache/`. It fetches nothing —
the request rate is `quests-harvest.py`'s and is unchanged by this script existing.

It writes ONE file, `src/EQBuddy.Core/Data/HarvestedGuides.json.gz`, which is refresh output
like `QuestCatalog.json` or `ItemCatalog.json.gz`. **`GuideCatalog.json` stays curated and is
never touched here** — the catalog merges the two at load and a curated guide wins on
`QuestName` (today that is the fourteen class epic pages, whose curated guides carry 486
hand-checked rows).

THE WHOLE JOB IS TO BE BORING
-----------------------------
Per page, in this order and nothing else:

  1. SECTION      `== Checklist ==` when the page has one, else `== Walkthrough ==`.
  2. STAGES       each `===`/`====`… heading inside that section, in document order. A
                  section with no sub-headings is one stage named for the section.
  3. OBJECTIVES   in document order, each `Transcribed` with WHAT = the line's own text:
                    * a `*` / `#` bullet (`:*` indented bullets included), or an `<li>` row
                      — which is how `{{CheckboxList}}` blocks write theirs;
                    * a wholly-bold line of six or more characters;
                    * a `You say, '…'` line.
                  NPC speech (`: X says '…'`), templates, categories, files, tables, HTML
                  scaffolding and italic editor notes are dropped. Wikilinks become their
                  text. Nothing is reordered, nothing is merged, and no bold line is dropped
                  for looking like flavour.
  4. SKELETON     a final "Turn-in pieces" stage on EVERY guide whose quest has turn-in
                  items: one `Collect` per item, then one `TurnIn`. Built from the page's
                  INFOBOX fields — quest giver and start zone — never from prose.
  5. SKELETON-ONLY  for a quest with no cached page (the 250 collection-split steps, which
                  live inside their parent's page and have none of their own) and for a
                  cached page with neither section: stage 4 alone, preceded by a
                  `TalkToNpc` when the infobox states both giver and start zone.

So all 1,178 quests end up guided, which is what the Founder decided.

WHAT IT MAY NEVER DO
--------------------
Infer WHO or WHERE from prose. That is trap 73 at 30,000×, and it is why every transcribed
row is `Transcribed` — the state whose only claim is "the page states this sentence", and
whose validation REFUSES a filled Who/Where/When/How. The skeleton rows are the one
exception and they are `Authored` only because their two fields come from a labelled infobox
row; where the infobox is blank the row is a `Stub` that says which field the page does not
state, rather than a sentence we made up.

BYTE-REPRODUCIBLE
-----------------
Same cache in, same bytes out — no clock, no locale, no dict ordering, no gzip mtime. Run it
twice and the second run changes nothing; `HarvestedGuidesTests` re-runs it from the test
host and diffs against the committed file, which is the only check that reads the 1,178.

    python scripts/harvests/eqlwiki/guides-transform.py [--check]

`--check` writes nothing and exits 1 when the file on disk is not what this would produce.
Also writes `guides-report.md` beside this script, so the weekly PR shows the drift.
"""

from __future__ import annotations

import argparse
import gzip
import html
import io
import json
import pathlib
import re
import sys
import unicodedata
from urllib.parse import unquote   # string work only — this script never opens a connection

HERE = pathlib.Path(__file__).resolve().parent
CACHE = HERE / "cache"
ROOT = HERE.parents[2]
DATA = ROOT / "src" / "EQBuddy.Core" / "Data"
QUEST_CATALOG = DATA / "QuestCatalog.json"
OUT = DATA / "HarvestedGuides.json.gz"
REPORT = HERE / "guides-report.md"
STATE = HERE / "refresh-state.json"

# ---------------------------------------------------------------- fixed vocabulary

# The state every transcribed row is in, and the only one it may be in.
TRANSCRIBED = "Transcribed"
AUTHORED = "Authored"
STUB = "Stub"

# A sentence off a page is not classified. "Custom" is the schema's word for that and the
# honest one — guessing Kill/Loot/TalkToNpc out of prose is the inference `Transcribed`
# exists to refuse. (`epic-guides-build.py` made the same call over its 486 rows.)
TRANSCRIBED_TYPE = "Custom"

SKELETON_STAGE_NAME = "Turn-in pieces"

# What the skeleton stage says about itself, on every guide that has one. The rows come from
# the quest's item list, which says WHAT to bring and never where it drops; a stage that did
# not say so out loud would read as directions it does not have.
SKELETON_NOTE = "from the quest's item list"

GUIDE_ID_PREFIX = "hq-"


def harvest_date() -> str:
    """The date to stamp every source with.

    `refresh-state.json`'s `ranAt` and not the clock, for two reasons that point the same way.

    It is TRUE, and conservatively so. The refresh evicts every page the wiki changed in its
    window and refetches it, so the moment a run COMPLETES the whole cache says what the wiki
    said — whether or not a given page was rewritten. `ranAt` is written at the END of a run,
    after the promotions this script sits among, so what it reads is the most recent refresh
    known to have finished. That makes the stamp at worst one cycle OLDER than the fetch, which
    is the safe direction: a fact aged faster than it needs to be is re-checked sooner, and a
    date that flatters us is worse than none.

    And it is REPRODUCIBLE: a committed file rather than a clock, so this script writes the
    same bytes on a fresh clone, next week, and inside a test host.
    """
    return json.loads(STATE.read_text(encoding="utf-8"))["ranAt"][:10]


# ---------------------------------------------------------------- wikitext cleaning

HEADING_RX = re.compile(r"^(=+)\s*(.+?)\s*=+\s*$")
# `[[Target|Label]]` shows the LABEL and `[[Target]]` the target: what a reader of the page
# sees is what the row says. (`quests-harvest.py` keeps the TARGET instead — it is resolving
# identities, we are carrying a sentence.)
WIKILINK_RX = re.compile(r"\[\[([^\]|]+)(?:\|([^\]]*))?\]\]")
# `{{:Item Name}}` is a transclusion of an item page — on a `{{CheckboxList}}` row it IS the
# item, so it becomes its name. Every other template is scaffolding and goes.
TRANSCLUDE_RX = re.compile(r"\{\{:\s*([^}|]+?)\s*\}\}")
TEMPLATE_RX = re.compile(r"\{\{[^{}]*\}\}")
EXTLINK_RX = re.compile(r"\[(?:https?|ftp)://[^\s\]]+\s+([^\]]*)\]")
BARE_EXTLINK_RX = re.compile(r"\[((?:https?|ftp)://[^\s\]]+)\]")
TAG_RX = re.compile(r"</?[A-Za-z][^>]*>")
# Greedy, and tolerant of the trailing apostrophe run a page leaves when it bolds a line that
# already ends in a quote: `'''You say, 'You can count on my help.''''` is four apostrophes,
# and a non-greedy close would leave one behind and drop the row.
BOLD_LINE_RX = re.compile(r"^'''(.+)'''[\s'.:,;!?]*$")
YOU_SAY_RX = re.compile(r"^You say,?\s*['\"]", re.IGNORECASE)
# A leading run of `:` or `;` is a list marker, not content. Pages write the player's line
# either way (`;You say, 'are you skinning those?'`) and indent their bullets with it
# (`:*[[Ancient Tarnished Plate Helmet]]`).
LIST_MARKER_RX = re.compile(r"^[:;]+\s*")
LI_RX = re.compile(r"^<li\b[^>]*>", re.IGNORECASE)
DIV_OPEN_RX = re.compile(r"^<div\b([^>]*)>", re.IGNORECASE)
DIV_CLOSE_RX = re.compile(r"^</div>", re.IGNORECASE)
ITALIC_LINE_RX = re.compile(r"^''(?!')")


def clean(text: str) -> str:
    """Wikitext → the sentence a reader sees. Markup only: not one word is added, removed or
    reordered, so the result is still the page's own line."""
    text = TRANSCLUDE_RX.sub(r"\1", text)
    for _ in range(3):   # templates nest two deep at worst in this corpus
        new = TEMPLATE_RX.sub("", text)
        if new == text:
            break
        text = new
    text = WIKILINK_RX.sub(lambda m: (m.group(2) or m.group(1)).strip(), text)
    text = EXTLINK_RX.sub(r"\1", text)
    text = BARE_EXTLINK_RX.sub(r"\1", text)
    text = re.sub(r"<br\s*/?>", " ", text, flags=re.IGNORECASE)
    text = TAG_RX.sub("", text)
    text = text.replace("'''", "").replace("''", "")
    text = html.unescape(text)
    # NBSP and friends are whitespace to a reader; leaving them in makes two identical rows
    # compare unequal and shows up as a stray box in the UI.
    text = "".join(" " if unicodedata.category(ch) == "Zs" else ch for ch in text)
    return " ".join(text.split()).strip()


def slug(text: str) -> str:
    out: list[str] = []
    for ch in text.lower():
        if ch.isalnum() and ch.isascii():
            out.append(ch)
        elif out and out[-1] != "-":
            out.append("-")
    return "".join(out).strip("-")


# ---------------------------------------------------------------- section + objectives

def section_body(wikitext: str, name: str) -> str | None:
    """The body of a LEVEL-2 section, or None. Level 2 because that is the section the rule
    names: a `=== Checklist ===` nested inside something else is a part of that something,
    not the page's own checklist."""
    lines = wikitext.splitlines()
    want = name.lower()
    start = None
    for i, line in enumerate(lines):
        m = HEADING_RX.match(line.strip())
        if not m:
            continue
        depth, title = len(m.group(1)), clean(m.group(2)).lower()
        if start is None:
            if depth == 2 and title == want:
                start = i + 1
        elif depth <= 2:
            return "\n".join(lines[start:i])
    return "\n".join(lines[start:]) if start is not None else None


def objectives_in(body: str) -> list[tuple[int, str]]:
    """Every (stage-heading-depth-or-0, text) event in the section, in document order.

    Yields `(depth, heading)` for a sub-heading and `(0, sentence)` for an objective. One
    pass, one line at a time — the only state is which structural block we are inside.
    """
    events: list[tuple[int, str]] = []
    div_stack: list[bool] = []   # True == this div is a faction block
    table_depth = 0

    for raw in body.splitlines():
        line = raw.strip()
        if not line:
            continue

        # Wiki tables are data, not instructions, and their rows start with markup that would
        # otherwise read as bold lines (`|+ '''Experience Gains'''`).
        if line.startswith("{|"):
            table_depth += 1
            continue
        if table_depth:
            if line.startswith("|}"):
                table_depth -= 1
            continue

        m = DIV_OPEN_RX.match(line)
        if m:
            div_stack.append("facblock" in m.group(1).lower())
            continue
        if DIV_CLOSE_RX.match(line):
            if div_stack:
                div_stack.pop()
            continue
        # `<div class="facblock">` is the wiki's OWN marker for the faction/reward block the
        # page prints after a hand-in — "Your faction standing with DaBashers has been
        # adjusted by 5". Those are results, not steps. Dropping them is structural (the page
        # labelled them) and not a judgement about whether a line looks like flavour: 3,332 of
        # the corpus's 5,271 bullets are inside one, and every single one of them is a faction
        # or experience line. Any other div's contents are kept.
        if div_stack and div_stack[-1]:
            continue

        heading = HEADING_RX.match(line)
        if heading:
            events.append((len(heading.group(1)), clean(heading.group(2))))
            continue

        text = objective_text(line)
        if text:
            events.append((0, text))
    return events


def objective_text(line: str) -> str:
    """The sentence this line contributes, or "" when it contributes none."""
    # A marker-led line is a bullet (`:*[[Ancient Tarnished Plate Helmet]]`) or the player's
    # own line (`;You say, 'are you skinning those?'`) and nothing else. Everything else it
    # leads is NPC speech or a quoted aside, which the rule drops — including the 32 lines in
    # this corpus that use `;` for "Give X 1 x Y", because those are page prose in a list
    # marker's clothes and answering "is that an instruction" is the inference we refuse.
    if LIST_MARKER_RX.match(line):
        body = LIST_MARKER_RX.sub("", line)
        if not (body.startswith("*") or body.startswith("#") or YOU_SAY_RX.match(body)):
            return ""
        line = body

    if line.startswith("*") or line.startswith("#"):
        return clean(line.lstrip("*#").strip())

    if LI_RX.match(line):
        return clean(LI_RX.sub("", line))

    if YOU_SAY_RX.match(line):
        return clean(line)

    bold = BOLD_LINE_RX.match(line)
    if bold:
        text = clean(bold.group(1))
        # Six characters is the rule's own floor: it keeps "'''Hand in three Fire Beetle
        # Eyes.'''" and drops the one-word section labels pages use as pseudo-headings.
        return text if len(text) >= 6 else ""

    return ""


# ---------------------------------------------------------------- guide assembly

def source_for(quest: dict, date: str) -> dict:
    """The page a reader can check, and the string the weekly refresh intersects with the
    week's changed pages (`GuideSource.Title`, one field with two jobs).

    For a page-backed quest the title IS the quest name. For one of the 250 collection-split
    steps there is no page of its own — its text lives inside the parent's — so the title is
    read back off the URL the harvest recorded, which is the served title (trap 3)."""
    title = unquote(quest["url"].rsplit("/", 1)[-1]).replace("_", " ")
    return {"url": quest["url"], "title": title, "retrievedAt": date}


def objective(oid: str, order: int, otype: str, *, what: str, authoring: str,
              title: str = "", short: str = "", who: str = "", where: str = "",
              item_names: list[str] | None = None, stub_note: str = "",
              source: dict | None = None) -> dict:
    """One objective, keys in a FIXED order so a refresh PR diffs as data."""
    out: dict = {"id": oid, "order": order, "objectiveType": otype}
    if title:
        out["title"] = title
    if short:
        out["shortInstruction"] = short
    if who:
        out["who"] = who
    if where:
        out["where"] = where
    if what:
        out["what"] = what
    if item_names:
        out["itemNames"] = item_names
    out["authoring"] = authoring
    if stub_note:
        out["stubNote"] = stub_note
    if source is not None:
        out["sources"] = [dict(source)]
    return out


def transcribed_stages(guide_id: str, body: str, section_name: str,
                       source: dict) -> list[dict]:
    """The page's own stages and rows. Empty when the section states nothing we carry."""
    buckets: list[dict] = []   # {"name", "rows": [text]}
    current = {"name": section_name, "rows": []}
    for depth, text in objectives_in(body):
        if depth:
            buckets.append(current)
            current = {"name": text or section_name, "rows": []}
        else:
            current["rows"].append(text)
    buckets.append(current)

    stages: list[dict] = []
    for bucket in buckets:
        if not bucket["rows"]:
            continue   # a heading with nothing under it is not a stage a player can walk
        stage_id = f"{guide_id}-s{len(stages) + 1}"
        stages.append({
            "id": stage_id,
            "name": bucket["name"],
            "order": len(stages) + 1,
            "objectives": [
                objective(f"{stage_id}-o{i}", i, TRANSCRIBED_TYPE,
                          what=text, authoring=TRANSCRIBED, source=source)
                for i, text in enumerate(bucket["rows"], start=1)
            ],
        })
    return stages


def skeleton_stage(guide_id: str, quest: dict, order: int, source: dict,
                   lead_talk: bool) -> dict | None:
    """"Turn-in pieces" — the stage the quest's own item list buys us.

    Every row's WHO and WHERE come from a labelled infobox row and nowhere else. Where the
    infobox is blank the sentence shrinks AND the row drops to `Stub` naming the field the
    page does not state: an `Authored` row must answer who and where, so a blank one wearing
    that flag would be a claim we cannot back."""
    items = quest.get("items") or []
    if not items and not lead_talk:
        return None

    giver = (quest.get("questGiver") or "").strip()
    zone = (quest.get("startZone") or "").strip()
    missing = [label for label, value in (("quest giver", giver), ("start zone", zone))
               if not value]
    stub_note = ("The page's infobox states no "
                 + " and no ".join(missing)
                 + ", so we cannot say who to bring these to or where.") if missing else ""
    authoring = STUB if missing else AUTHORED

    objectives: list[dict] = []
    order_n = 0

    if lead_talk and giver and zone:
        order_n += 1
        objectives.append(objective(
            f"{guide_id}-talk", order_n, "TalkToNpc",
            title=f"Speak to {giver}",
            short=f"Speak to {giver} in {zone}",
            who=giver, where=zone,
            what=f"Speak to {giver} in {zone}.",
            authoring=AUTHORED, source=source))

    seen: set[str] = set()
    for item in items:
        name = item["name"]
        qty = int(item.get("qty") or 1)
        piece = f"{name} ×{qty}"
        oid = f"{guide_id}-item-{slug(name)}"
        while oid in seen:
            oid += "-x"
        seen.add(oid)
        order_n += 1
        what = f"Collect {piece} for {giver}." if giver else f"Collect {piece}."
        objectives.append(objective(
            oid, order_n, "Collect",
            title=piece, short=f"Collect {piece}",
            who=giver, where=zone, what=what,
            item_names=[name], authoring=authoring, stub_note=stub_note,
            source=source))

    if items:
        order_n += 1
        what = f"Hand the pieces to {giver}." if giver else "Hand in the pieces."
        where_words = f" in {zone}" if zone else ""
        objectives.append(objective(
            f"{guide_id}-handin", order_n, "TurnIn",
            title=f"Hand in to {giver}" if giver else "Hand in the pieces",
            short=(f"Hand the pieces to {giver}{where_words}" if giver
                   else "Hand in the pieces"),
            who=giver, where=zone, what=what,
            authoring=authoring, stub_note=stub_note, source=source))

    if not objectives:
        return None
    return {
        "id": f"{guide_id}-turnin",
        "name": SKELETON_STAGE_NAME,
        "order": order,
        "arrivalNote": SKELETON_NOTE,
        "objectives": objectives,
    }


EMPTY_STAGE_NAME = "Not written up yet"


def empty_stage(guide_id: str, quest: dict, uncached: bool, source: dict) -> dict:
    """The one honest stage for a quest we have nothing at all for.

    Says which of the two reasons it is, because they call for different help: a missing
    section is a wiki edit somebody can make, and an unread page is ours to fetch."""
    note = ("This quest's steps live inside its parent page, which the harvest reads as one "
            "page, and the quest has no turn-in items of its own — so we have no steps for it."
            if uncached else
            "The wiki page has no Checklist and no Walkthrough section we could carry a step "
            "out of, and the quest has no turn-in items — so we have no steps for it.")
    return {
        "id": f"{guide_id}-unwritten",
        "name": EMPTY_STAGE_NAME,
        "order": 1,
        "objectives": [objective(
            f"{guide_id}-unwritten-o1", 1, "Verify",
            title="We have no steps for this quest",
            short="Nothing we could carry from the wiki",
            what="",
            authoring=STUB, stub_note=note, source=source)],
    }


def build_guide(quest: dict, wikitext: str | None, date: str) -> tuple[dict, str]:
    """One guide, plus the shape name the report counts it under."""
    guide_id = GUIDE_ID_PREFIX + slug(quest["name"])
    source = source_for(quest, date)

    stages: list[dict] = []
    shape = "uncached" if wikitext is None else "no walkthrough"
    if wikitext is not None:
        for section_name in ("Checklist", "Walkthrough"):
            body = section_body(wikitext, section_name)
            if body is None:
                continue
            stages = transcribed_stages(guide_id, body, section_name, source)
            if stages:
                shape = section_name.lower()
                if len(stages) > 1:
                    shape += " (subsectioned)"
                break
            shape = f"{section_name.lower()} (empty)"

    skeleton = skeleton_stage(guide_id, quest, len(stages) + 1, source,
                              lead_talk=not stages)
    if skeleton is not None:
        stages.append(skeleton)

    if not stages:
        # A page with no section we carry AND a quest with no turn-in items leaves nothing to
        # walk. That is a Stub — the state whose whole job is to say "we cannot tell you", in
        # the player's terms, with the share-back door on it. An empty guide would be a silent
        # no-op wearing a guide's clothes, and a guide validates only if it has a stage.
        stages.append(empty_stage(guide_id, quest, wikitext is None, source))
        shape = "nothing to carry"

    guide = {
        "id": guide_id,
        "name": quest["name"],
        "guideType": "NormalQuest",
        "questName": quest["name"],
    }
    if quest.get("minLevel"):
        guide["minLevel"] = quest["minLevel"]
    guide["sources"] = [dict(source)]
    guide["stages"] = stages
    return guide, shape


# ---------------------------------------------------------------- output

def cache_path(title: str) -> pathlib.Path:
    """`quests-harvest.py`'s scheme, and `refresh.py` evicts on the same one."""
    return CACHE / ("quest-" + re.sub(r"[^A-Za-z0-9._-]", "_", title) + ".wikitext")


NOTE = ("Auto-written by scripts/harvests/eqlwiki/guides-transform.py from the quest "
        "wikitext cache. NEVER hand-edit: GuideCatalog.json is the curated file and wins "
        "on questName. Every row here is either the page's own sentence (Transcribed) or "
        "the quest's item list wearing the infobox's giver and start zone.")


def render(guides: list[dict]) -> bytes:
    """One guide per line, so `gunzip | diff` reads as a data change rather than as one
    30,000-row line. JSON separators pinned, no ASCII escaping, no sorting (document order
    IS the product), and a gzip member with no mtime and no filename in its header — the
    three places a "deterministic" writer usually leaks the clock."""
    body = io.StringIO()
    body.write('{"note":')
    body.write(json.dumps(NOTE, ensure_ascii=False))
    body.write(',"guides":[\n')
    for i, guide in enumerate(guides):
        body.write(json.dumps(guide, ensure_ascii=False, separators=(",", ":")))
        body.write(",\n" if i + 1 < len(guides) else "\n")
    body.write("]}\n")

    raw = body.getvalue().encode("utf-8")
    buf = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", compresslevel=9, mtime=0, fileobj=buf) as gz:
        gz.write(raw)
    return buf.getvalue()


def report(guides: list[dict], shapes: dict[str, str], skeleton_only: list[str],
           date: str) -> str:
    counts: dict[str, int] = {}
    for shape in shapes.values():
        counts[shape] = counts.get(shape, 0) + 1

    rows = sum(len(s["objectives"]) for g in guides for s in g["stages"])
    transcribed = sum(1 for g in guides for s in g["stages"]
                      for o in s["objectives"] if o["authoring"] == TRANSCRIBED)
    authored = sum(1 for g in guides for s in g["stages"]
                   for o in s["objectives"] if o["authoring"] == AUTHORED)
    stub = rows - transcribed - authored

    lines = [
        "# Harvested guides report",
        "",
        "Written by `guides-transform.py`. `GuideCatalog.json` is the curated file and is",
        "never touched by it; a curated guide wins on `questName` at load.",
        "",
        f"- Guides written: **{len(guides)}** (one per `QuestCatalog.json` quest)",
        f"- Source date stamped on every row: `{date}` (`refresh-state.json` → `ranAt`)",
        f"- Objective rows: **{rows}** — {transcribed} Transcribed, {authored} "
        f"skeleton Authored, {stub} skeleton Stub",
        "",
        "## Per shape",
        "",
        "| Shape | Guides |",
        "| --- | ---: |",
    ]
    for shape in sorted(counts):
        lines.append(f"| {shape} | {counts[shape]} |")

    lines += [
        "",
        f"## Skeleton-only guides ({len(skeleton_only)})",
        "",
        "No `== Checklist ==` and no `== Walkthrough ==` we could carry a row out of — the",
        "turn-in stage is the whole guide, and its caption says so. The collection-split",
        "steps are here because their text lives inside their parent's page and they have",
        "no cache file of their own; the weekly refresh does not change that.",
        "",
    ]
    lines += [f"- {name}" for name in skeleton_only]
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="write nothing; exit 1 if the committed file would change")
    args = parser.parse_args()

    date = harvest_date()
    quests = json.loads(QUEST_CATALOG.read_text(encoding="utf-8"))["quests"]

    guides: list[dict] = []
    shapes: dict[str, str] = {}
    skeleton_only: list[str] = []
    ids: set[str] = set()
    for quest in quests:
        path = cache_path(quest["name"])
        wikitext = path.read_text(encoding="utf-8") if path.exists() else None
        guide, shape = build_guide(quest, wikitext, date)
        if guide["id"] in ids:
            raise SystemExit(f"two quests slug to '{guide['id']}' — ids must be unique")
        ids.add(guide["id"])
        guides.append(guide)
        shapes[quest["name"]] = shape
        if all(s["name"] == SKELETON_STAGE_NAME for s in guide["stages"]):
            skeleton_only.append(quest["name"])

    payload = render(guides)
    text = report(guides, shapes, skeleton_only, date)

    if args.check:
        stale = []
        if not OUT.exists() or OUT.read_bytes() != payload:
            stale.append(str(OUT.relative_to(ROOT)))
        if not REPORT.exists() or REPORT.read_text(encoding="utf-8") != text:
            stale.append(str(REPORT.relative_to(ROOT)))
        if stale:
            print("STALE: " + ", ".join(stale))
            return 1
        print(f"OK: {len(guides)} guides reproduce byte-for-byte.")
        return 0

    OUT.write_bytes(payload)
    REPORT.write_text(text, encoding="utf-8")
    rows = sum(len(s["objectives"]) for g in guides for s in g["stages"])
    # ASCII only: this runs under refresh.py on a Windows console whose default codec is
    # cp1252, and a decorative arrow there is a traceback after a successful write.
    print(f"Wrote {len(guides)} guides, {rows} objective rows "
          f"({len(skeleton_only)} skeleton-only) into {OUT.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
