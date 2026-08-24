#!/usr/bin/env python3
"""Knowledge refresh — the cadence that keeps embedded catalogs in step with
eqlwiki.com as the community edits it.

Why this exists: EQBuddy ships wiki-derived catalogs (quests, fade messages,
zone graph) as embedded data, and the wiki is no longer static — the in-app
"Copy for wiki" flow (discussion #65) has players actively pushing loot/spawn
observations to it. This script turns "someone remembers to re-run the
harvesters" into a scheduled, reviewable loop.

What one run does:
  1. asks the wiki's MediaWiki API for changes since the last run
     (refresh-state.json; recentchanges is a handful of requests)
  2. evicts exactly the changed pages from the harvest caches and drops the
     title-list caches so NEW pages get enumerated
  3. re-runs the harvesters — their resume-safe cache discipline means work is
     proportional to what actually changed, not catalog size
  4. re-runs the deterministic promotions:
       spells.json  -> FadeMessages.json   (fades-harvest.py)
       spells.json  -> SlowSpells.json     (slows-harvest.py)
       spells.json  -> BuffDurations.json  (buffs-harvest.py)
       spells.json  -> DebuffLandings.json (debuffs-harvest.py)
       spells.json  -> CharmSpells.json    (charms-harvest.py)
       spells.json  -> SpellLevels.json    (spell-levels-promote.py)
       class pages  -> SpellLevels.json    (class-spells-harvest.py, then the promote:
                                            the CLASS page wins and the spell pages fill
                                            only levels it has no section for)
       quests.json  -> QuestCatalog.json   (quests-promote.py)
       zones.json   -> ZoneGraph.json      (../eqltools/zones-merge.py; the
                       eqltools half is a committed extract — browser-fetched,
                       see ../eqltools/README.md — and stays as-is)
     The first four are order-sensitive: the exact-match catalogs partition the
     message space, and each later one excludes lines the earlier ones claimed
     (fades exclude landing lines, buffs exclude fade/slow/regen claims,
     debuffs exclude everything the other three took). Issue #167 found the
     gap this closes: 170 new alchemy potion/spell pages landed on the wiki
     and no scheduled run would ever have carried them into BuffDurations.
  5. flags CURATED catalogs whose source pages changed (SpawnCatalog, AaCatalog,
     MezSpells, CcSpells, RegenSpells) — those carry human judgment and are
     never auto-written; the flag goes in the report for a person to act on
  6. writes eqlwiki/refresh-report.md and updates eqlwiki/refresh-state.json

Run it by hand any time, or let .github/workflows/knowledge-refresh.yml run it
weekly and open a PR with the report as its body. Exit code 0 always (no
changes is success); the workflow decides "PR or not" from git status.

Politeness contract (same as every harvester here): ~1 request/second,
identified User-Agent, exponential backoff. A refresh with no wiki changes
costs ~2 API requests total.
"""

import hashlib
import json
import re
import subprocess
import sys
import time
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
WIKI = HERE / "eqlwiki"
CACHE = WIKI / "cache"
DATA = HERE.parents[1] / "src" / "EQBuddy.Core" / "Data"
STATE = WIKI / "refresh-state.json"
REPORT = WIKI / "refresh-report.md"

BASE = "https://eqlwiki.com/api.php"
UA = "EQBuddy-harvest/1.0 (david.edwards08@gmail.com; polite MediaWiki client; ~1 req/s)"
PACE_SECONDS = 1.0

# Title-list caches: dropped every refresh so newly created pages enumerate.
TITLE_LISTS = ["quest-titles.json", "quest-items.json", "titles.json", "zone-titles.json"]

# Harvest, then promote — order matters (promotions read harvester output).
# items-harvest.py needs no cache eviction: it keys on revision ids itself and
# only refetches pages whose revision moved.
# class-spells-harvest.py runs with the harvesters (it fetches the sixteen class pages)
# and BEFORE the promote that consumes its class-spells.json. Its report therefore compares
# the class pages against the catalog as it stands BEFORE this refresh rewrites it, which
# is the diff a reviewer wants: what this run is about to change.
HARVESTERS = ["spells-harvest.py", "quests-harvest.py", "zones-harvest.py", "aas-harvest.py",
              "items-harvest.py", "class-spells-harvest.py"]
# fades -> slows -> buffs -> debuffs is load-bearing: each excludes messages
# the ones before it claimed (see module docstring; issue #167).
PROMOTIONS = [WIKI / "fades-harvest.py", WIKI / "slows-harvest.py",
              WIKI / "buffs-harvest.py", WIKI / "debuffs-harvest.py",
              WIKI / "charms-harvest.py",
              WIKI / "quests-promote.py",
              HERE / "eqltools" / "zones-merge.py",
              WIKI / "items-promote.py",
              WIKI / "spell-levels-promote.py"]

# Written by promotions above; diffed for the report.
PROMOTED = ["FadeMessages.json", "SlowSpells.json", "BuffDurations.json", "DebuffLandings.json",
            "CharmSpells.json", "QuestCatalog.json", "ZoneGraph.json", "ItemCatalog.json.gz",
            "SpellLevels.json"]
# Human-curated; never auto-written, only flagged when their sources move.
CURATED = ["SpawnCatalog.json", "AaCatalog.json", "MezSpells.json",
           "CcSpells.json", "RegenSpells.json"]

# Curated knowledge that lives in CODE rather than a Data JSON. Without these, a wiki
# correction to a quest we carry would never surface here — which matters because
# EQBuddy's rule is to match the wiki when a conflict cannot be settled, and we ask
# reporters to fix the wiki. That ask is only honest if a wiki fix reaches us.
CURATED_SOURCES = ["SkyQuestDefaults.cs", "EpicQuestChecklistCatalog.cs"]
SRC = HERE.parents[1] / "src" / "EQBuddy.Core"

_last_request = [0.0]


def api_get(params):
    params = dict(params, format="json")
    url = BASE + "?" + urllib.parse.urlencode(params)
    delay = 2.0
    for attempt in range(7):
        wait = PACE_SECONDS - (time.time() - _last_request[0])
        if wait > 0:
            time.sleep(wait)
        _last_request[0] = time.time()
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=60) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            if "error" in data:
                raise RuntimeError("API error: %s" % data["error"])
            return data
        except Exception as e:
            code = getattr(e, "code", None)
            print(f"  ! attempt {attempt + 1} failed ({e!r}); backing off {delay:.0f}s",
                  flush=True)
            if code is not None and code not in (429, 500, 502, 503, 504):
                raise
            time.sleep(delay)
            delay *= 2
    raise RuntimeError("Giving up after repeated failures: " + url)


def recent_changes(since_iso):
    """All edit/new changes since since_iso: [(title, ns)], newest timestamp."""
    changes, newest, cont = [], since_iso, {}
    while True:
        data = api_get({
            "action": "query", "list": "recentchanges",
            "rcprop": "title|timestamp", "rctype": "edit|new",
            "rcnamespace": "0|10", "rclimit": "500",
            "rcend": since_iso,     # rc lists newest-first; rcend is the OLD bound
            **cont,
        })
        for rc in data["query"]["recentchanges"]:
            changes.append((rc["title"], rc["ns"]))
            if rc["timestamp"] > newest:
                newest = rc["timestamp"]
        if "continue" not in data:
            return changes, newest
        cont = {"rccontinue": data["continue"]["rccontinue"]}


# Cache filename schemes, one per harvester (kept in sync with each script).
def quest_cache(title):
    return "quest-" + re.sub(r"[^A-Za-z0-9._-]", "_", title) + ".wikitext"


def zone_cache(title):
    return "zone-" + re.sub(r"[^A-Za-z0-9._-]", "_", title) + ".wikitext"


def lsth_cache(title):
    # quests-harvest.py caches {{#lsth:...}} transclusion sources under this
    # scheme; an edit to the SOURCE page must re-expand every shell using it.
    return "lsth-" + re.sub(r"[^A-Za-z0-9._-]", "_", title) + ".wikitext"


def class_cache(title):
    """class-spells-harvest.py's scheme: `class-{Title_With_Underscores}.wikitext` plus a
    `.json` holding the SERVED title (trap 3 — `redirects=1` means the page you get may not
    be the page you asked for).

    **This is the load-bearing half of putting that harvest on the weekly cadence.** Running
    the script every week achieves nothing if an edited class page still comes back from the
    cache — the catalog would freeze at the day it was first parsed while reporting success,
    which is exactly the silent decay the refresh exists to prevent."""
    stem = title.replace(" ", "_")
    return f"class-{stem}.wikitext"


def class_meta_cache(title):
    return f"class-{title.replace(' ', '_')}.json"


def spell_cache(title):
    h = hashlib.md5(title.encode("utf-8")).hexdigest()[:8]
    safe = re.sub(r"[^A-Za-z0-9 '`().,_-]", "_", title).strip(" .")
    return f"{safe}.{h}.wikitext"


def evict(titles):
    """Remove every cache file any harvester could hold for these pages."""
    evicted = []
    for title in titles:
        candidates = [quest_cache(title), zone_cache(title), spell_cache(title),
                      lsth_cache(title), class_cache(title), class_meta_cache(title)]
        if title == "Alternate Advancement":
            candidates.append("Alternate_Advancement.wikitext")
            candidates.append("revision_meta.json")
        for name in candidates:
            path = CACHE / name
            if path.exists():
                path.unlink()
                evicted.append(name)
    for name in TITLE_LISTS:
        path = WIKI / name
        if path.exists():
            path.unlink()
    return evicted


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else ""


def collect_strings(node, into):
    if isinstance(node, str):
        into.add(node)
    elif isinstance(node, list):
        for x in node:
            collect_strings(x, into)
    elif isinstance(node, dict):
        for v in node.values():
            collect_strings(v, into)


def curated_flags(changed_titles):
    """Curated catalogs mentioning a changed page, by name intersection."""
    flags = {}
    lowered = {t.lower() for t in changed_titles}
    for name in CURATED:
        path = DATA / name
        if not path.exists():
            continue
        strings = set()
        collect_strings(json.loads(path.read_text(encoding="utf-8")), strings)
        hits = sorted({s for s in strings if s.lower() in lowered})
        if name == "AaCatalog.json" and "Alternate Advancement" in changed_titles:
            hits.append("(the Alternate Advancement page itself)")
        if hits:
            flags[name] = hits

    # Same intersection over the code-resident catalogs. Their rows are pipe-delimited
    # inside one string literal, so split on "|" to get individual page-sized names.
    for name in CURATED_SOURCES:
        path = SRC / name
        if not path.exists():
            continue
        strings = set()
        for literal in re.findall(r'"([^"\n]*)"', path.read_text(encoding="utf-8")):
            for field in literal.split("|"):
                field = field.strip()
                if field:
                    strings.add(field)
        hits = sorted({s for s in strings if s.lower() in lowered})
        if hits:
            flags[name] = hits
    return flags


def run(script):
    print(f"== {script.name}", flush=True)
    r = subprocess.run([sys.executable, str(script)], cwd=str(script.parent))
    if r.returncode != 0:
        raise RuntimeError(f"{script.name} exited {r.returncode}")


def main():
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    if STATE.exists():
        since = json.loads(STATE.read_text(encoding="utf-8"))["since"]
    else:
        # First run: cover the current caches' age with margin.
        since = (datetime.now(timezone.utc) - timedelta(days=7)).strftime(
            "%Y-%m-%dT%H:%M:%SZ")

    print(f"Refresh window: {since} .. now")
    changes, newest = recent_changes(since)
    articles = sorted({t for t, ns in changes if ns == 0})
    templates = sorted({t for t, ns in changes if ns == 10})
    print(f"{len(articles)} changed pages, {len(templates)} changed templates")

    report = [
        "# Knowledge refresh report",
        "",
        f"- Window: `{since}` → `{now}`",
        f"- Changed wiki pages: {len(articles)}",
        f"- Changed templates: {len(templates)}",
    ]

    if not articles and not templates:
        report += ["", "No wiki changes in the window — catalogs already current."]
        REPORT.write_text("\n".join(report) + "\n", encoding="utf-8")
        STATE.write_text(json.dumps({"since": newest, "ranAt": now}, indent=1),
                         encoding="utf-8")
        print("No changes; done.")
        return

    evicted = evict(articles)
    report += [f"- Cache evictions: {len(evicted)}", ""]

    before = {name: file_hash(DATA / name) for name in PROMOTED}
    for script in HARVESTERS:
        run(WIKI / script)
    for script in PROMOTIONS:
        run(script)

    changed_catalogs = [n for n in PROMOTED if file_hash(DATA / n) != before[n]]
    report += ["## Promoted catalogs",
               ""] + [f"- `{n}`: {'UPDATED' if n in changed_catalogs else 'unchanged'}"
                      for n in PROMOTED]

    flags = curated_flags(articles)
    report += ["", "## Curated catalogs (never auto-written — review these)", ""]
    if flags:
        for name, hits in flags.items():
            report.append(f"- `{name}` mentions changed pages: " +
                          ", ".join(hits[:12]) + (" …" if len(hits) > 12 else ""))
    else:
        report.append("- No curated catalog mentions a changed page.")

    if templates:
        report += ["", "## Changed templates (parser shapes may have moved)", ""]
        report += [f"- {t}" for t in templates]

    report += ["", "## Changed pages", ""] + [f"- {t}" for t in articles]

    REPORT.write_text("\n".join(report) + "\n", encoding="utf-8")
    STATE.write_text(json.dumps({"since": newest, "ranAt": now}, indent=1),
                     encoding="utf-8")
    print(f"Done. Catalogs updated: {changed_catalogs or 'none'} — see {REPORT}")


if __name__ == "__main__":
    main()
