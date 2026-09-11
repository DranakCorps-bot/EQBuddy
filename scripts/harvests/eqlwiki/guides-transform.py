#!/usr/bin/env python3
"""Turn the cached eqlwiki quest wikitext into HARVESTED guides (DRA-45, Delivery 2 N1).

WHAT THIS IS, AND WHAT IT IS NOT
--------------------------------
`GuideCatalog.json` is CURATED and never auto-written. **This script never touches it.** It
writes a SECOND file, `src/EQBuddy.Core/Data/HarvestedGuides.json.gz`, which is refresh
OUTPUT in exactly the way `QuestCatalog.json` and `ItemCatalog.json.gz` are — regenerated
every week, reviewed as the diff of a knowledge-refresh PR, and never hand-edited.
`GuideCatalog.LoadEmbedded` merges the two, and **a curated guide wins on `QuestName`**: the
day someone authors a real guide for a quest, the harvested one steps aside.

It never fetches. Its whole input is `cache/quest-*.wikitext`, which `quests-harvest.py`
already keeps current, and `src/EQBuddy.Core/Data/QuestCatalog.json`. Consequence 7 (our
request rate at eqlwiki) is therefore not merely unchanged but untouched — this adds zero
requests. See the measurement note under "THE 250" below, which corrects the plan on the
one point where it guessed.

THE TRANSFORMER'S WHOLE JOB IS TO BE BORING
-------------------------------------------
Every objective it produces is one of exactly two things:

  * `Transcribed` — one line of the page's own prose, wikilinks reduced to their text and
    nothing else touched. WHO/WHERE/WHEN/HOW stay EMPTY, because parsing them out of prose
    is inference wearing the wiki's citation (trap 73), and `GuideCatalog.Validate()`
    refuses a Transcribed step that fills any of them.
  * A SKELETON row built from `QuestCatalog`'s structured fields — the item list and the
    infobox's quest giver and start zone. Nothing here reads prose.

It never infers WHO or WHERE, never reorders, never merges two lines, and never drops a
bold line because it reads like flavour.

THE 250 — measured, and it corrects the plan
--------------------------------------------
Fable's §3 expected "~250 uncached pages, fetched once by the next refresh". Measured here:
all 1,178 catalog quests resolve to a cached page already. 928 quests ARE pages; the other
250 are the per-step quests `quests-harvest.py` splits out of 57 COLLECTION pages (Trooper
Scale Armor, the Coldain ring chain), and those steps will never have a page of their own —
their `url` is the parent's. So nothing is waiting on a future fetch, and the skeleton-only
set is a permanent shape rather than a backlog.

A split step gets the SKELETON ONLY, never the parent's walkthrough: that walkthrough
describes the whole chain, and handing all seven Coldain steps the same seven-subsection
prose would be the loudest possible version of "merge lines you were told not to merge".
The parent collection page is itself a catalog quest and keeps the walkthrough.

BYTE-REPRODUCIBLE
-----------------
Compact JSON, one guide per line, fixed key order, gzipped with a zeroed mtime, so the same
cache produces the same bytes on any machine. `HarvestedGuidesTests` asserts exactly that.

    python scripts/harvests/eqlwiki/guides-transform.py [--check]

`--check` writes nothing and exits 1 if the committed file is not what this produces.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import io
import json
import pathlib
import re
import sys
import urllib.parse

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CACHE = HERE / "cache"
STATE = HERE / "refresh-state.json"
REPORT = HERE / "guides-report.md"
DATA = ROOT / "src" / "EQBuddy.Core" / "Data"
QUEST_CATALOG = DATA / "QuestCatalog.json"
OUT = DATA / "HarvestedGuides.json.gz"

# Every objective this produces is one of these two, and `HarvestedGuidesTests` holds that
# shut. `Authored` is reachable ONLY where the infobox answers who and where — see
# `skeleton_stage` for why a Collect row is never one of them.
TRANSCRIBED = "Transcribed"
AUTHORED = "Authored"
STUB = "Stub"

# We do not classify a sentence off a page. "Custom" is the schema's word for that and the
# honest one; guessing Loot/Kill/TalkToNpc from prose is the inference `Transcribed` exists
# to refuse. Same call `epic-guides-build.py` made for the 486 epic rows.
PROSE_TYPE = "Custom"

SECTION_CHECKLIST = "Checklist"
SECTION_WALKTHROUGH = "Walkthrough"

# The stage every guide with turn-in items ends on.
PIECES_STAGE = "Turn-in pieces"

# What a Collect row rests on, said in the player's terms. It is a claim about US — we have
# not recorded where this drops — and never about the page, which may well say in prose we
# are not allowed to parse into WHO and WHERE.
COLLECT_STUB_NOTE = ("We have not recorded who drops this or where — this row comes from "
                     "the quest's item list on eqlwiki.")
HANDIN_STUB_NOTE = ("eqlwiki's page for this quest does not name the quest giver and start "
                    "zone, so we cannot say who to hand these to, or where.")

HEADING_RX = re.compile(r"^(=+)\s*(.+?)\s*=+\s*$", re.MULTILINE)
# Deliberately WIDER than `quests-harvest.py`'s LINK, which excludes `#` because it is
# matching item names. A sentence may link a section anchor — `[[Bunker Cell #1]]` — and a
# transcribed row showing raw brackets is the page's markup leaking onto the player's screen.
LINK_RX = re.compile(r"\[\[([^\]|]+)(?:\|([^\]]*))?\]\]")
TEMPLATE_RX = re.compile(r"\{\{[^{}]*\}\}")
HTML_TAG_RX = re.compile(r"</?[A-Za-z][^>]*>")
# `<div class="facblock">` … `</div>` — the wiki's own marker for the faction adjustments a
# turn-in causes. 529 of the sections we read carry one, and every line inside is a bullet,
# so without this rule "Your faction standing with Clerics of Tunare has been adjusted by 9"
# becomes a step the player is asked to tick. An OUTCOME is not an instruction. This is the
# one drop rule beyond the plan's list; it is structural (a marked block, like a template),
# not a judgement about what a sentence means.
FACBLOCK_RX = re.compile(
    r"<div\s+class\s*=\s*[\"']?facblock[\"']?\s*>.*?</div>", re.IGNORECASE | re.DOTALL)
BULLET_RX = re.compile(r"^[:;\s]*([*#]+)\s*(.*)$")
BOLD_LINE_RX = re.compile(r"^'''+\s*(.+?)\s*'''+[.:]?$")
YOU_SAY_RX = re.compile(r"^you say[,:]?\s*['\"‘“]", re.IGNORECASE)
# NPC speech. A safety net rather than the main rule — dialogue is almost never a bullet or
# a whole-line bold — but the plan names it and a page that bullets its dialogue should not
# turn a quest giver's monologue into six steps.
NPC_SAYS_RX = re.compile(
    r"^.{0,60}?\b(says|said|tells you|shouts|yells|whispers|replies|responds|exclaims)\b"
    r"\s*[,:]?\s*['\"‘“]", re.IGNORECASE)
# A line that is nothing but an italic aside — "(note: the wiki is unclear here)". The plan
# names italic editor notes as droppable; this matches the whole-line form only.
ITALIC_LINE_RX = re.compile(r"^''[^']+''[.]?$")

MIN_BOLD_CHARS = 6


def slug(text: str) -> str:
    """Kebab-case, the same shape `epic-guides-build.py` produces."""
    out: list[str] = []
    for ch in text.lower():
        if ch.isalnum():
            out.append(ch)
        elif out and out[-1] != "-":
            out.append("-")
    return "".join(out).strip("-")


def norm_heading(text: str) -> str:
    text = re.sub(r"'''*", "", text)
    text = LINK_RX.sub(lambda m: m.group(2) or m.group(1), text)
    return " ".join(text.split()).lower()


def section_of(wikitext: str, want: str) -> str | None:
    """One top-level section's body, or None when the page has no such heading. Same
    heading semantics `quests-harvest.py` already uses (markup-blind, body runs to the next
    heading of the same or shallower level)."""
    heads = list(HEADING_RX.finditer(wikitext))
    target = norm_heading(want)
    for i, m in enumerate(heads):
        if norm_heading(m.group(2)) != target:
            continue
        level = len(m.group(1))
        end = next((m2.start() for m2 in heads[i + 1:] if len(m2.group(1)) <= level),
                   len(wikitext))
        return wikitext[m.end():end]
    return None


def plain(text: str) -> str:
    """A wikitext line as the sentence the page states. Wikilinks become their DISPLAY text,
    templates and tags go, quotes for bold/italic go, whitespace collapses. Nothing is
    added, reordered or rephrased."""
    text = LINK_RX.sub(lambda m: (m.group(2) or m.group(1)).strip(), text)
    prev = None
    while prev != text:                       # nested templates, innermost first
        prev = text
        text = TEMPLATE_RX.sub(" ", text)
    text = HTML_TAG_RX.sub(" ", text)
    text = re.sub(r"'{2,}", "", text)
    text = text.replace("&nbsp;", " ")
    return " ".join(text.split())


def objective_lines(body: str) -> list[str]:
    """The lines of one stage that are steps, in DOCUMENT ORDER, already reduced to text.

    Three shapes and no others: a `*`/`#` bullet (the `{{CheckboxList}}` sections mark their
    items this way), a whole-line bold of six or more characters, and a `You say, '…'` line.
    Everything else on the page — narrative, NPC speech, faction blocks, templates,
    categories, whole-line italic asides — is not a step."""
    steps: list[str] = []
    for raw in body.splitlines():
        line = raw.strip()
        if not line:
            continue
        if line.startswith("[[Category:") or line.startswith("[[File:"):
            continue
        if ITALIC_LINE_RX.match(line):
            continue

        bullet = BULLET_RX.match(line)
        bold = BOLD_LINE_RX.match(line)
        if bullet:
            text = plain(bullet.group(2))
        elif bold and len(plain(bold.group(1))) >= MIN_BOLD_CHARS:
            text = plain(bold.group(1))
        elif YOU_SAY_RX.match(line):
            text = plain(line)
        else:
            continue

        if not text:
            continue
        if not YOU_SAY_RX.match(text) and NPC_SAYS_RX.match(text):
            continue
        steps.append(text)
    return steps


def prose_stages(wikitext: str) -> tuple[list[dict], str]:
    """(stages, the section they came from — "" when the page has neither).

    **Chosen by PRESENCE, in the plan's order: Checklist, else Walkthrough, else none.** Not
    by which one yields more. A page whose Checklist section is present but empty gets no
    prose rows even where its Walkthrough has some — seven pages are in that state and two
    of them would gain rows from a fallback. They are named in `guides-report.md` rather
    than papered over, because "take whichever produces more" is a preference the
    transformer would then be exercising on every page, and the rule this file lives by is
    that it exercises none.

    `===`/`====` headings inside the section are stages in order; a body with none is one
    stage named for the section."""
    name = SECTION_CHECKLIST
    body = section_of(wikitext, SECTION_CHECKLIST)
    if body is None:
        name = SECTION_WALKTHROUGH
        body = section_of(wikitext, SECTION_WALKTHROUGH)
    if body is None:
        return [], ""

    body = FACBLOCK_RX.sub("\n", body)

    heads = list(HEADING_RX.finditer(body))
    chunks: list[tuple[str, str]] = []
    if heads:
        lead = body[:heads[0].start()]
        if lead.strip():
            chunks.append((name, lead))
        for i, m in enumerate(heads):
            end = heads[i + 1].start() if i + 1 < len(heads) else len(body)
            chunks.append((plain(m.group(2)) or name, body[m.end():end]))
    else:
        chunks.append((name, body))

    stages: list[dict] = []
    for heading, chunk in chunks:
        lines = objective_lines(chunk)
        if not lines:
            continue
        stage_id = f"stage-{len(stages) + 1}"
        stages.append({
            "id": stage_id,
            "name": heading,
            "order": len(stages) + 1,
            "objectives": [
                {
                    "id": f"{stage_id}-{n}",
                    "order": n,
                    "objectiveType": PROSE_TYPE,
                    "what": text,
                    "authoring": TRANSCRIBED,
                }
                for n, text in enumerate(lines, 1)
            ],
        })
    return stages, name


def skeleton_stage(quest: dict, order: int) -> dict | None:
    """"Turn-in pieces" — one `Collect` per catalog item, then the hand-in.

    **A Collect row is a STUB and not an Authored one, and that is a departure from the
    plan's word worth stating.** §3 called these Authored "from structured fields the page
    states in its infobox". The infobox answers who gives the quest and where it starts; it
    says nothing about who drops a turn-in item or where. Writing the quest giver into a
    Collect row's WHO would assert that Captain Tillin drops Blue Orc Heads — fabricated
    certainty wearing provenance, 855 times over, which is trap 73 exactly. `Stub` is the
    state whose whole meaning is "we cannot tell you where", it is TRUE here, and it is the
    state that opens the share-back door so a player who knows can fix the wiki.

    The hand-in IS Authored where the infobox answers both questions, and shrinks to a stub
    where it does not — never inventing a giver or a zone to fill the bar."""
    items = quest.get("items") or []
    if not items:
        return None

    stage_id = "turn-in-pieces"
    objectives = []
    used: set[str] = set()
    for item in items:
        base = f"{stage_id}-collect-{slug(item['name']) or 'item'}"
        oid = base
        bump = 2
        while oid in used:
            oid = f"{base}-{bump}"
            bump += 1
        used.add(oid)
        qty = int(item.get("qty") or 1)
        objectives.append({
            "id": oid,
            "order": len(objectives) + 1,
            "objectiveType": "Collect",
            "title": f"Collect {item['name']}" + (f" ×{qty}" if qty > 1 else ""),
            "shortInstruction": f"Collect {item['name']}"
                                + (f" ×{qty}" if qty > 1 else "") + ".",
            "what": f"Collect {item['name']}" + (f" ×{qty}" if qty > 1 else "") + ".",
            "itemNames": [item["name"]],
            "authoring": STUB,
            "stubNote": COLLECT_STUB_NOTE,
        })

    giver = (quest.get("questGiver") or "").strip()
    zone = (quest.get("startZone") or "").strip()
    known = bool(giver) and bool(zone)
    handin = {
        "id": f"{stage_id}-hand-in",
        "order": len(objectives) + 1,
        "objectiveType": "TurnIn",
        "title": f"Hand the pieces to {giver}" if giver else "Hand in the pieces",
        "shortInstruction": (f"Hand the pieces to {giver}." if giver
                             else "Hand in the pieces."),
        "what": (f"Hand the pieces to {giver}." if giver else "Hand in the pieces."),
        # The turn-in cannot be reached before its pieces are, and the prerequisite is the
        # only kind the shipped catalog carries (`OnlyATurnInCarriesPrerequisites…`).
        "prerequisiteObjectiveIds": [o["id"] for o in objectives],
        "authoring": AUTHORED if known else STUB,
    }
    if known:
        handin["who"] = giver
        handin["where"] = zone
    else:
        handin["stubNote"] = HANDIN_STUB_NOTE
    objectives.append(handin)

    return {"id": stage_id, "name": PIECES_STAGE, "order": order, "objectives": objectives}


def talk_stage(quest: dict) -> dict | None:
    """The one step a skeleton-only guide can honestly open with: go and get the quest. Only
    when the infobox answers BOTH who and where — anything less is a row that says nothing
    the heading does not."""
    giver = (quest.get("questGiver") or "").strip()
    zone = (quest.get("startZone") or "").strip()
    if not giver or not zone:
        return None
    return {
        "id": "start",
        "name": "Start the quest",
        "order": 1,
        "objectives": [{
            "id": "start-speak",
            "order": 1,
            "objectiveType": "TalkToNpc",
            "title": f"Speak to {giver}",
            "shortInstruction": f"Speak to {giver} in {zone}.",
            "who": giver,
            "where": zone,
            "what": f"Speak to {giver} in {zone}.",
            "authoring": AUTHORED,
        }],
    }


def page_title(quest: dict) -> str:
    """The eqlwiki page this quest's facts came from, recovered from the url the harvest
    recorded. For a quest that IS a page this is its own title; for one of the 250 steps
    split out of a collection page it is the PARENT's title, which is the honest answer and
    the reason those steps are skeleton-only."""
    path = quest["url"].split("eqlwiki.com/", 1)[1]
    return urllib.parse.unquote(path).replace("_", " ")


def cache_path(title: str) -> pathlib.Path:
    return CACHE / ("quest-" + re.sub(r"[^A-Za-z0-9._-]", "_", title) + ".wikitext")


def is_own_page(quest: dict) -> bool:
    expected = "https://eqlwiki.com/" + urllib.parse.quote(quest["name"].replace(" ", "_"))
    return quest["url"] == expected


def harvested_at() -> str:
    """The most recent date we can PROVE our cached wikitext matched the wiki: the last
    COMPLETED refresh. Every page edited in a refresh window is evicted and refetched, so
    after a run finishes every cached page is current as of that run.

    Deliberately the last completed run and not "today" — during a refresh this file is
    written before the state is stamped, so the date lags one cycle and understates our
    freshness rather than overstating it. It also makes the output byte-reproducible from
    committed files alone, which a clock or a git timestamp would not be."""
    return json.loads(STATE.read_text(encoding="utf-8"))["ranAt"][:10]


def build_guides(quests: list[dict], when: str) -> tuple[list[dict], dict]:
    guides: list[dict] = []
    stats = collections.Counter()
    shapes: dict[str, list[str]] = collections.defaultdict(list)
    fallback_would_help: list[str] = []
    ids: set[str] = set()

    for quest in quests:
        title = page_title(quest)
        source = {
            "url": quest["url"],
            "title": title,
            "retrievedAt": when,
        }

        own = is_own_page(quest)
        path = cache_path(title)
        wikitext = path.read_text(encoding="utf-8") if own and path.exists() else ""
        stages, section = prose_stages(wikitext) if wikitext else ([], "")

        if stages:
            shape = section.lower()
        else:
            start = talk_stage(quest)
            if start:
                stages = [start]
            if not own:
                shape = "split-step"
            elif not section:
                shape = "no-section"
            else:
                shape = section.lower() + "-yielded-nothing"
                # The one place the presence rule costs a page something. Named, so the
                # day it is worth changing the rule the evidence is already counted.
                other = section_of(wikitext, SECTION_WALKTHROUGH) \
                    if section == SECTION_CHECKLIST else None
                if other and objective_lines(FACBLOCK_RX.sub("\n", other)):
                    fallback_would_help.append(quest["name"])

        pieces = skeleton_stage(quest, len(stages) + 1)
        if pieces:
            stages.append(pieces)

        if not stages:
            # No prose, no items, and no giver-and-zone to open with: fourteen catalog
            # entries that are index or collection PAGES rather than quests. A guide with
            # no objectives is refused by lock 4a, and inventing one step so the count
            # reads 1,178 would be the invention this whole file exists to avoid. They are
            # named below and in `HarvestedGuidesTests`, so the day one gains content the
            # test fails and says which.
            stats["no-guide"] += 1
            shapes["none"].append(quest["name"])
            continue

        for stage in stages:
            for objective in stage["objectives"]:
                if objective["authoring"] != STUB:
                    objective["sources"] = [dict(source)]

        base = "harvested-" + (slug(quest["name"]) or "quest")
        gid = base
        bump = 2
        while gid in ids:
            gid = f"{base}-{bump}"
            bump += 1
        ids.add(gid)

        guides.append({
            "id": gid,
            "name": quest["name"],
            "guideType": "NormalQuest",
            "questName": quest["name"],
            "sources": [dict(source)],
            "stages": stages,
        })
        stats[shape] += 1
        shapes[shape].append(quest["name"])

    return guides, {"stats": stats, "shapes": shapes,
                    "fallbackWouldHelp": fallback_would_help}


# The key order every record is written in. A fixed order is what makes a regenerated file
# diff as DATA rather than as a reshuffle.
GUIDE_KEYS = ["id", "name", "guideType", "questName", "zoneNames", "applicableClasses",
              "minLevel", "sources", "stages"]
STAGE_KEYS = ["id", "name", "order", "arrivalNote", "objectives"]
OBJECTIVE_KEYS = ["id", "order", "objectiveType", "title", "shortInstruction",
                  "who", "where", "what", "when", "why", "how",
                  "prerequisiteObjectiveIds", "rewardKey", "itemNames",
                  "authoring", "stubNote", "sources"]
SOURCE_KEYS = ["url", "title", "retrievedAt"]


def ordered(record: dict, keys: list[str]) -> dict:
    unknown = set(record) - set(keys)
    if unknown:
        raise SystemExit(f"unknown keys {sorted(unknown)} — add them to the fixed key order")
    return {k: record[k] for k in keys if k in record}


def serialize(guides: list[dict], when: str) -> bytes:
    lines = []
    for guide in guides:
        record = ordered(guide, GUIDE_KEYS)
        record["sources"] = [ordered(s, SOURCE_KEYS) for s in record["sources"]]
        record["stages"] = [
            dict(ordered(stage, STAGE_KEYS),
                 objectives=[
                     dict(ordered(o, OBJECTIVE_KEYS),
                          **({"sources": [ordered(s, SOURCE_KEYS) for s in o["sources"]]}
                             if "sources" in o else {}))
                     for o in stage["objectives"]])
            for stage in record["stages"]
        ]
        lines.append(json.dumps(record, separators=(",", ":"), ensure_ascii=False))

    note = ("AUTO-WRITTEN by scripts/harvests/eqlwiki/guides-transform.py from the cached "
            f"eqlwiki quest pages, verified current as of {when}. Do not hand-edit: the "
            "weekly refresh overwrites it. Hand-authored guides live in GuideCatalog.json, "
            "which wins on questName.")
    text = ('{"note":' + json.dumps(note, ensure_ascii=False) + ',"guides":[\n'
            + ",\n".join(lines) + "\n]}\n")

    raw = io.BytesIO()
    # mtime=0 and no stored filename: a gzip header carrying either would make the bytes
    # depend on the clock and the path, and "byte-reproducible" is the assertion this file
    # is checked by.
    with gzip.GzipFile(filename="", mode="wb", fileobj=raw, compresslevel=9, mtime=0) as gz:
        gz.write(text.encode("utf-8"))
    return raw.getvalue()


def write_report(guides: list[dict], survey: dict, quests: list[dict], when: str) -> None:
    stats: collections.Counter = survey["stats"]
    shapes: dict[str, list[str]] = survey["shapes"]
    objectives = [o for g in guides for s in g["stages"] for o in s["objectives"]]
    by_authoring = collections.Counter(o["authoring"] for o in objectives)
    by_type = collections.Counter(o["objectiveType"] for o in objectives)
    skeleton_only = sorted(
        g["name"] for g in guides
        if all(s["id"] in ("turn-in-pieces", "start") for s in g["stages"]))
    prose_rows = collections.Counter(
        len([o for s in g["stages"] for o in s["objectives"]
             if o["authoring"] == TRANSCRIBED])
        for g in guides)

    lines = [
        "# Harvested guides report",
        "",
        "Auto-written by `guides-transform.py`. Nothing here is curated; "
        "`GuideCatalog.json` is, and wins on `questName`.",
        "",
        f"- Catalog quests: {len(quests)}",
        f"- Guides written: {len(guides)}",
        f"- Objectives: {len(objectives)}",
        f"- Distinct eqlwiki pages cited: {len({g['sources'][0]['title'] for g in guides})}",
        f"- Wikitext verified current as of: {when} (last completed refresh)",
        "",
        "## Per shape",
        "",
        "| Shape | Guides |",
        "|---|---|",
        *[f"| {k} | {v} |" for k, v in sorted(stats.items())],
        "",
        "`split-step` is one of the 250 per-step quests split out of a collection page; its",
        "page is the parent's, so it never gets the parent's walkthrough (see the script's",
        "header). `*-yielded-nothing` means the section is there and holds no bullet, bold",
        "line or `You say`.",
        "",
        f"### Where the presence rule costs a page rows ({len(survey['fallbackWouldHelp'])})",
        "",
        "A `Checklist` section that yielded nothing, on a page whose `Walkthrough` would",
        "have. The rule picks by presence, not by yield — see `prose_stages`.",
        "",
        *([f"- {n}" for n in sorted(survey["fallbackWouldHelp"])] or ["- none"]),
        "",
        "## Authoring states",
        "",
        *[f"- `{k}`: {v}" for k, v in sorted(by_authoring.items())],
        "",
        "## Objective types",
        "",
        *[f"- `{k}`: {v}" for k, v in sorted(by_type.items())],
        "",
        "## Transcribed rows per guide",
        "",
        *[f"- {n} rows: {c} guides" for n, c in sorted(prose_rows.items())],
        "",
        f"## Skeleton-only guides ({len(skeleton_only)})",
        "",
        "No walkthrough and no checklist reached these — the 250 collection split-steps,",
        "whose page is their parent's, plus the pages that carry neither section.",
        "",
        *[f"- {n}" for n in skeleton_only],
        "",
        "## Quests with no guide",
        "",
        *([f"- {n}" for n in sorted(shapes.get("none", []))] or ["- none"]),
    ]
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="write nothing; exit 1 if the committed file differs")
    args = parser.parse_args()

    quests = json.loads(QUEST_CATALOG.read_text(encoding="utf-8"))["quests"]
    when = harvested_at()
    guides, survey = build_guides(quests, when)
    payload = serialize(guides, when)

    current = OUT.read_bytes() if OUT.exists() else b""
    if args.check:
        if payload != current:
            print(f"{OUT.name} differs from what this produces "
                  f"({len(current)} bytes on disk, {len(payload)} generated).",
                  file=sys.stderr)
            return 1
        print(f"{OUT.name} is already what this produces ({len(payload)} bytes).")
        return 0

    OUT.write_bytes(payload)
    write_report(guides, survey, quests, when)
    print(f"wrote {OUT.name}: {len(guides)} guides, "
          f"{sum(len(s['objectives']) for g in guides for s in g['stages'])} objectives, "
          f"{len(payload)} bytes gz. Report: {REPORT.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
