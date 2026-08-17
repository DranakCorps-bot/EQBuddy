#!/usr/bin/env python3
"""Harvest the EverQuest Legends quest catalog from eqlwiki.com (MediaWiki API).

Same polite client discipline as spells-harvest.py: ~1 request/second, exponential
backoff, resume-safe wikitext cache in cache/. Rerun any time; cached pages are not
refetched.

Outputs (all in this script's directory):
  quest-titles.json   - enumerated members of Category:Quests
  cache/quest-*.wikitext - raw wikitext per page
  quests.json         - parsed quest array (full)
  quests-report.md    - summary report incl. pages the parser couldn't read

Quest page shape (verified on Animal Skin Armor, 2026-08-07):
  - Infobox wiki-table rows: Start Zone / Quest Giver / Minimum Level / Classes
  - Turn-in items inside "Give <NPC> ..." lines as: N x [[Item Name]]
  - Rewards as {{:Item Name}} transclusions
"""

import json
import re
import time
import urllib.parse
import urllib.request

BASE = "https://eqlwiki.com/api.php"
UA = "EQBuddy-harvest/1.0 (david.edwards08@gmail.com; polite MediaWiki client; ~1 req/s)"
HERE = __import__("pathlib").Path(__file__).resolve().parent
CACHE = HERE / "cache"
CACHE.mkdir(exist_ok=True)
PACE_SECONDS = 1.0

_last_request = [0.0]
backoff_events = []


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
            msg = f"attempt {attempt+1} failed ({e!r}); backing off {delay:.0f}s"
            print("  ! " + msg, flush=True)
            backoff_events.append(msg)
            if code is not None and code not in (429, 500, 502, 503, 504):
                raise
            time.sleep(delay)
            delay *= 2
    raise RuntimeError("Giving up after repeated failures: " + url)


# ---------------------------------------------------------------- enumeration

def enumerate_titles():
    titles_path = HERE / "quest-titles.json"
    if titles_path.exists():
        titles = json.loads(titles_path.read_text(encoding="utf-8"))
        print(f"quest-titles.json cached: {len(titles)} titles")
        return titles
    titles = []
    cont = {}
    while True:
        data = api_get({
            "action": "query", "list": "categorymembers",
            "cmtitle": "Category:Quests", "cmlimit": "200",
            "cmnamespace": "0", **cont,
        })
        titles += [m["title"] for m in data["query"]["categorymembers"]]
        print(f"  enumerated {len(titles)} quest titles", flush=True)
        if "continue" not in data:
            break
        cont = {"cmcontinue": data["continue"]["cmcontinue"]}
    titles_path.write_text(json.dumps(titles, indent=1), encoding="utf-8")
    return titles


def fetch_wikitext(title):
    safe = re.sub(r"[^A-Za-z0-9._-]", "_", title)
    path = CACHE / f"quest-{safe}.wikitext"
    if path.exists():
        return path.read_text(encoding="utf-8")
    data = api_get({
        "action": "query", "prop": "revisions", "rvprop": "content",
        "redirects": "1", "titles": title,
    })
    pages = data["query"]["pages"]
    page = next(iter(pages.values()))
    revs = page.get("revisions")
    text = revs[0]["*"] if revs else ""
    path.write_text(text, encoding="utf-8")
    return text


# --------------------------------------------------------- lsth transclusion
# 2026-08-14: the wiki restructured the per-class "Plane of Sky Tests" pages
# into {{#lsth:Plane of Sky|<Class> Tests}} shells transcluding the zone page,
# which now owns the checklist tables. The API hands back raw wikitext, so the
# shells parse as empty quests unless the transclusion is expanded here.
# Expansion is deliberately narrow: shell pages only (nothing but files,
# templates, headings, and categories), never index pages — a page with its
# own prose keeps its own story.

LSTH_RX = re.compile(r"\{\{\s*#lsth:\s*([^}|]+?)\s*(?:\|\s*([^}]*?)\s*)?\}\}")


def fetch_lsth_source(title):
    safe = re.sub(r"[^A-Za-z0-9._-]", "_", title)
    path = CACHE / f"lsth-{safe}.wikitext"   # evicted by refresh.py's lsth scheme
    if path.exists():
        return path.read_text(encoding="utf-8")
    data = api_get({
        "action": "query", "prop": "revisions", "rvprop": "content",
        "redirects": "1", "titles": title,
    })
    page = next(iter(data["query"]["pages"].values()))
    revs = page.get("revisions")
    text = revs[0]["*"] if revs else ""
    path.write_text(text, encoding="utf-8")
    return text


def _norm_heading(s):
    s = re.sub(r"'''*", "", s)
    s = re.sub(LINK, r"\1", s)
    return " ".join(s.split()).lower()


def extract_section(text, section):
    """Wikitext of one section (or the lead when section is empty), matching
    #lsth semantics closely enough: heading text compared markup-blind, body
    runs to the next heading of the same or shallower level."""
    heads = list(HEADING_RX.finditer(text))
    if not section:
        return text[:heads[0].start()] if heads else text
    want = _norm_heading(section)
    for i, m in enumerate(heads):
        if _norm_heading(m.group(2)) != want:
            continue
        level = len(m.group(1))
        end = next((m2.start() for m2 in heads[i + 1:] if len(m2.group(1)) <= level),
                   len(text))
        return text[m.end():end]
    return ""


SHELL_LINE_RX = re.compile(r"^(\[\[(File|Category):|\{\{|=+[^=]+=+\s*$)")


def expand_lsth(title, wikitext):
    """Returns (wikitext, expanded). Only expanded pages get the widened item
    extraction below — the Sky tables mark requirements as {{:Item}} boxes on
    <li> lines, but applying those rules to ordinary pages leaks rewards into
    turn-in items (verified on Deck of Spontaneous Generation, Gleed's Bow)."""
    if title in INDEX_PAGES or "#lsth:" not in wikitext:
        return wikitext, False
    if not all(SHELL_LINE_RX.match(ln.strip())
               for ln in wikitext.splitlines() if ln.strip()):
        return wikitext, False
    def repl(m):
        return extract_section(fetch_lsth_source(m.group(1).strip()),
                               (m.group(2) or "").strip())
    return LSTH_RX.sub(repl, wikitext), True


# ------------------------------------------------------------------- parsing

# Infobox rows arrive in several table syntaxes; match "Label ... value" leniently.
INFOBOX_FIELDS = {
    "startZone": r"Start\s*Zone",
    "questGiver": r"Quest\s*Giver",
    "minLevel": r"(?:Minimum|Min\.?)\s*Level",
    "classes": r"Classes",
}

LINK = r"\[\[([^\]|#]+)(?:\|[^\]]*)?\]\]"


def strip_links(text):
    return re.sub(LINK, r"\1", text).strip(" '\"|{}")


def parse_infobox_field(wikitext, label_pattern):
    # Actual questTopTable shape (verified in cache): header cell on one line,
    # value cell on the next:
    #   ! ''' Start Zone: '''
    #   | [[Iceclad Ocean]]
    m = re.search(
        r"!\s*'*\s*" + label_pattern + r"\s*:?\s*'*\s*\n\|\s*([^\n]*)", wikitext,
        re.IGNORECASE)
    if not m:   # fallback: same-line "| Start Zone: value" variants
        m = re.search(label_pattern + r"\s*:\s*([^\n|!]+)", wikitext, re.IGNORECASE)
    return strip_links(m.group(1)) if m else ""


def parse_turnin_items(wikitext, quest_giver, quest_item_set, widened=False):
    # Turn-in items: lines that hand something to an NPC, plus requirement BULLETS —
    # pages like The Falchion list what to collect as "* [[Blue Orc Head]] (from …)"
    # with no give-verb at all (David's Crushbone pass, 2026-08-07). Reward-section
    # bullets are excluded so prize lists don't read as requirements.
    items = {}
    section = ""
    for line in wikitext.splitlines():
        heading = re.match(r"^=+\s*(.+?)\s*=+\s*$", line)
        if heading:
            section = heading.group(1).lower()
            continue
        # Bullets are requirement lists ("* [[Blue Orc Head]] (from the Orc Prophet)")
        # EXCEPT inside the reward section, where they're prizes. On widened
        # (lsth-expanded) pages HTML <li> lines count too — the Sky checklist
        # tables mark their requirements that way.
        in_rewards = "reward" in section
        stripped = line.lstrip()
        is_bullet = (stripped.startswith("*")
                     or (widened and stripped.lower().startswith("<li"))) \
            and not in_rewards
        # Verb shapes from the field (#79, Kobold Molars): "each [[X]] that is TURNED IN",
        # "has a chance to DROP a [[X]]", "COLLECT/BRING me [[X]]" — repeatable turn-in
        # loops phrase their one item this way and never say "give". Safe to widen: bare
        # links still need the wiki's own Quest Items category to vouch for them.
        if not is_bullet and not re.search(
                r"\b(give|hand|turn(?:ed)?\s*in|return|bring|collect|drop(?:s|ped)?)\b",
                line, re.IGNORECASE):
            continue
        for qty, name in re.findall(r"(\d+)\s*x\s*" + LINK, line):
            name = name.strip()
            items[name] = max(items.get(name, 0), int(qty))
        # Bare links on a give-line with no "N x" prefix count as quantity 1, but
        # ONLY when the wiki's Quest Items category vouches for the name — give-lines
        # also link the receiving NPC, zones, and related mobs, and the category is
        # the cheap arbiter of "this is actually an item".
        for name in re.findall(LINK, line):
            name = name.strip()
            if name in items or name == quest_giver:
                continue
            if re.search(r"\d+\s*x\s*\[\[" + re.escape(name), line):
                continue
            if name not in quest_item_set:
                continue
            items.setdefault(name, 1)
        # {{:Item}} transclusions on requirement lines — the Sky checklist
        # tables list their runes and boss drops as item boxes, not links.
        # Widened pages only; category vouching gates like bare links above.
        if widened:
            for name in re.findall(r"\{\{:\s*([^}|]+?)\s*\}\}", line):
                name = name.strip()
                if name in items or name == quest_giver \
                        or name not in quest_item_set:
                    continue
                items.setdefault(name, 1)
    return items


def parse_quest(title, wikitext, quest_item_set, widened=False):
    q = {"name": title,
         "url": "https://eqlwiki.com/" + urllib.parse.quote(title.replace(" ", "_"))}
    for key, pat in INFOBOX_FIELDS.items():
        q[key] = parse_infobox_field(wikitext, pat)
    lvl = re.search(r"\d+", q["minLevel"] or "")
    q["minLevel"] = int(lvl.group(0)) if lvl else 0

    items = parse_turnin_items(wikitext, q["questGiver"], quest_item_set, widened)
    q["items"] = [{"name": n, "qty": c} for n, c in sorted(items.items())]

    # Rewards: {{:Item}} transclusions plus links on lines under a Reward heading,
    # plus {{Gear Set|A|B|…}} params — the armor-set pages (Trooper Scale, Dreadscale)
    # list their pieces only through that template.
    rewards = set(re.findall(r"\{\{:\s*([^}|]+?)\s*\}\}", wikitext))
    reward_section = re.search(r"=+\s*Rewards?\s*=+([^=]*)", wikitext, re.IGNORECASE)
    if reward_section:
        rewards |= {n.strip() for n in re.findall(LINK, reward_section.group(1))}
    # …and the rows of any wikitable in that section. The capture above ends at the
    # next "=" character, which a table hits on its own opening line ({| class="…"),
    # so a rewards list written as a table was read as no rewards at all: Soldier's
    # Brooch Quest lost all eight of them on 2026-08-17 when the wiki reformatted
    # its bullet list into a stat-comparison table. Deliberately additive — the
    # narrative capture is left exactly as it was, because pages do also name real
    # rewards in prose (Bulthar Trunks' gems, Rathmana's spell list), and a rule
    # tight enough to drop the prose junk drops those with it.
    full_section = re.search(r"=+\s*Rewards?\s*=+[^\n]*\n(.*?)(?=\n=+[^=\n]|\Z)",
                             wikitext, re.IGNORECASE | re.DOTALL)
    if full_section:
        for table in re.findall(r"^\{\|.*?^\|\}", full_section.group(1),
                                re.DOTALL | re.MULTILINE):
            for row in table.splitlines():
                if row.startswith("|") and not row.startswith("|}"):
                    rewards |= {n.strip() for n in re.findall(LINK, row)}
    for gearset in re.findall(r"\{\{\s*Gear\s*Set\s*\|(.*?)\}\}", wikitext, re.DOTALL):
        rewards |= {p.strip() for p in gearset.split("|") if p.strip()}
    q["rewards"] = sorted(r for r in rewards if r)

    # Zone categories double as related-zone hints.
    q["categories"] = sorted(set(re.findall(r"\[\[Category:\s*([^\]|]+)\]\]", wikitext))
                             - {"Quests"})

    # Where the quest happens: the Related Zones infobox row (same two-line table
    # shape as the other fields) plus the start zone. Zone categories join in the
    # converter — non-zone categories can never collide with a real zone name.
    related = re.search(
        r"!\s*'*\s*Related\s*Zones?\s*:?\s*'*\s*\n\|\s*([^\n]*)", wikitext, re.IGNORECASE)
    q["relatedZones"] = sorted({z.strip() for z in
                                re.findall(LINK, related.group(1))} if related else set())

    # Era banner template ({{Velious Era}}, {{Classic Era}}, …) → normalized era name.
    # Case and naming drift on the wiki: "kunark Era", "EpicQuests Era", "Chardok Era".
    era = re.search(r"\{\{\s*([A-Za-z' ]+?)\s+Era\s*\}\}", wikitext, re.IGNORECASE)
    raw_era = era.group(1).strip().title() if era else ""
    q["era"] = {
        "Epicquests": "Epics", "Chardok": "Chardok Revamp", "Unknown": "",
    }.get(raw_era, raw_era)

    # Repeatable: the category is the reliable marker; a "Repeatable:" infobox row
    # exists on a couple of pages as backup.
    q["repeatable"] = ("Repeatable Turn-in Quests" in q["categories"]
                       or bool(re.search(r"Repeatable\s*:?\s*'*\s*(?:\n\|\s*)?\s*yes",
                                         wikitext, re.IGNORECASE)))
    return q


# ------------------------------------------------ collection section splitting
# The durable fix behind CatalogHygiene's Collection flag (keep these two name
# lists in sync with CatalogHygiene.cs): a page that documents a whole chain or
# armor set splits into per-step quests here, reward-anchored — a section heading
# that names exactly one of the page's rewards is one step ("Copper Coldain
# Insignia Ring (#1)", "3rd: Woven Coldain Prayer Shawl", "Boots"). Pages whose
# sections don't match stay whole and keep the Collection flag at load.

INDEX_PAGES = {
    "Popular Quests by Level", "Class Race Quest List",
    "Velious Class Armor Comparisons", "Faction Quests",
    "All Positive Faction Quests",
}

EXTRA_COLLECTIONS = {
    "Plane of Sky Keys", "Custom Plate Helms - Kael Drakkel",
    "Custom Plate Helms - Skyshrine", "Custom Plate Helms - Thurgadin",
    "Trooper Scale Armor", "Dreadscale Armor", "Animal Skin Armor",
    "Crusader's Tests", "Emerald Warriors' Items",
}


def is_collection(title):
    return (title.endswith("Quests") or title in EXTRA_COLLECTIONS) \
        and title not in INDEX_PAGES


STOPWORDS = {"the", "of", "a", "an", "and"}


def _tokens(s):
    return {w.rstrip("s") for w in re.findall(r"[a-z0-9`']+", s.lower())} - STOPWORDS


def clean_heading(raw):
    """Heading → (label, order). Strips bold/link/transclusion markup and the
    order decorations chains use — "(#3)" suffixes, "3rd:" prefixes — keeping
    the number so the step can carry it in its name."""
    t = re.sub(r"'''*", "", raw.strip())
    t = re.sub(r"\{\{:\s*([^}|]+?)\s*\}\}", r"\1", t)
    t = re.sub(LINK, r"\1", t)
    order = None
    m = re.search(r"\(\s*#\s*(\d+)\s*\)", t)
    if m:
        order, t = int(m.group(1)), t.replace(m.group(0), " ")
    m = re.match(r"(\d+)(?:st|nd|rd|th)\s*[:.]?\s+", t, re.IGNORECASE)
    if m:
        order = order or int(m.group(1))
        t = t[m.end():]
    t = re.sub(r"\s*Quests?\s*$", "", t, flags=re.IGNORECASE)
    return " ".join(t.split()), order


def claimed_rewards(label, rewards):
    """Which rewards a heading names: the reward inside the heading ("Copper
    Coldain Insignia Ring (#1)") or the heading inside the reward ("Boots" for
    "Trooper Scale Boots" — token match, singular/plural-blind)."""
    if len(label) < 4:
        return []
    lt = _tokens(label)
    if not lt:
        return []
    low = label.lower()
    return [r for r in rewards if r.lower() in low or lt <= _tokens(r)]


HEADING_RX = re.compile(r"^(=+)\s*(.+?)\s*=+\s*$", re.MULTILINE)


def split_collection(page, wikitext, quest_titles, quest_item_set, taken_names):
    """Per-step quests out of one collection page. Returns (steps, notes);
    notes explain skipped sections for the harvest report."""
    notes = []
    heads = list(HEADING_RX.finditer(wikitext))
    found = {}   # reward -> {"order", "items"}
    for i, m in enumerate(heads):
        label, order = clean_heading(m.group(2))
        claims = claimed_rewards(label, page["rewards"])
        if len(claims) > 1:   # keep the most specific when one contains the rest
            claims = [c for c in claims
                      if not any(c != o and _tokens(c) < _tokens(o) for o in claims)]
        if len(claims) != 1:
            if claims:
                notes.append(f"ambiguous heading '{label}' -> {sorted(claims)}")
            continue
        reward = claims[0]
        level = len(m.group(1))
        end = next((m2.start() for m2 in heads[i + 1:] if len(m2.group(1)) <= level),
                   len(wikitext))
        body = wikitext[m.end():end]
        # A section that transcludes another quest PAGE delegates to it — the
        # standalone page is already its own catalog entry ("Ring of Dain
        # Frostreaver IV (#10)" is just "{{: 10th Coldain Ring Quest}}").
        delegated = [t.strip() for t in re.findall(r"\{\{:\s*([^}|]+?)\s*\}\}", body)
                     if t.strip() in quest_titles]
        if delegated:
            notes.append(f"'{label}' delegated to standalone page {delegated}")
            continue
        items = parse_turnin_items(body, page["questGiver"], quest_item_set)
        items.pop(reward, None)   # "…to receive your [[Gold Ring]]" is the prize
        got = found.setdefault(reward, {"order": order, "items": {}})
        got["order"] = got["order"] or order
        for n, c in items.items():   # short+long walkthroughs merge (Trooper Scale)
            got["items"][n] = max(got["items"].get(n, 0), c)

    steps = []
    for reward, got in found.items():
        if reward in quest_titles or f"{reward} Quest" in quest_titles:
            notes.append(f"'{reward}' already a standalone quest page")
            continue
        if not got["items"]:
            notes.append(f"'{reward}': no turn-in items parsed")
            continue
        name = reward + (f" (#{got['order']})" if got["order"] else "")
        if name.lower() in taken_names:
            name = re.sub(r"\s*Quests$", "", page["name"]) + ": " + name
        steps.append({
            "name": name, "url": page["url"], "parent": page["name"],
            "startZone": page["startZone"], "questGiver": page["questGiver"],
            "minLevel": page["minLevel"], "classes": page["classes"],
            "items": [{"name": n, "qty": c} for n, c in sorted(got["items"].items())],
            "rewards": [reward],
            "categories": page["categories"], "relatedZones": page["relatedZones"],
            "era": page["era"], "repeatable": page["repeatable"],
        })
    # One matching section is not a chain; dozens is an index page in disguise.
    if len(steps) < 2 or len(steps) > 25:
        if steps:
            notes.append(f"not split: {len(steps)} usable steps")
        return [], notes
    steps.sort(key=lambda s: (found[s["rewards"][0]]["order"] or 99, s["name"]))
    for s in steps:
        taken_names.add(s["name"].lower())
    return steps, notes


def enumerate_quest_items():
    """Category:Quest Items — the wiki's own 'part of a quest' marker (4,148 pages).
    Broader than the turn-in sets we parse: includes quest REWARDS and multi-step
    intermediates, which is exactly the 'don't vendor this' signal the Loot views need."""
    path = HERE / "quest-items.json"
    if path.exists():
        items = json.loads(path.read_text(encoding="utf-8"))
        print(f"quest-items.json cached: {len(items)} items")
        return items
    items = []
    cont = {}
    while True:
        data = api_get({
            "action": "query", "list": "categorymembers",
            "cmtitle": "Category:Quest Items", "cmlimit": "200",
            "cmnamespace": "0", **cont,
        })
        items += [m["title"] for m in data["query"]["categorymembers"]]
        print(f"  enumerated {len(items)} quest items", flush=True)
        if "continue" not in data:
            break
        cont = {"cmcontinue": data["continue"]["cmcontinue"]}
    path.write_text(json.dumps(items, indent=1), encoding="utf-8")
    return items


def main():
    quest_items = enumerate_quest_items()
    titles = enumerate_titles()
    quest_item_set = set(quest_items)
    title_set = set(titles)
    taken_names = {t.lower() for t in titles}
    quests, empty, splits = [], [], []
    for i, title in enumerate(titles, 1):
        text = fetch_wikitext(title)
        if not text.strip():
            empty.append(title)
            continue
        text, widened = expand_lsth(title, text)
        # {{CheckboxList}} is the wiki's own checklist marker (the not-yet-
        # restructured Sky Tests pages carry it); those pages list requirements
        # as <li>{{:Item}} rows exactly like the transcluded tables do.
        widened = widened or "CheckboxList" in text
        q = parse_quest(title, text, quest_item_set, widened)
        quests.append(q)
        if is_collection(title):
            steps, notes = split_collection(q, text, title_set, quest_item_set,
                                            taken_names)
            quests += steps
            if steps or notes:
                splits.append((title, steps, notes))
        if i % 25 == 0:
            print(f"  parsed {i}/{len(titles)}", flush=True)

    (HERE / "quests.json").write_text(
        json.dumps(quests, indent=1, ensure_ascii=False), encoding="utf-8")

    with_items = [q for q in quests if q["items"]]
    no_items = [q["name"] for q in quests if not q["items"]]
    no_giver = [q["name"] for q in quests if not q["questGiver"]]
    unique_items = {i["name"] for q in quests for i in q["items"]}
    report = [
        "# Quest harvest report",
        "",
        f"- Quest Items category members: {len(quest_items)}",
        f"- Pages enumerated: {len(titles)}",
        f"- Parsed: {len(quests)} (empty pages: {len(empty)})",
        f"- With turn-in items: {len(with_items)}",
        f"- Unique turn-in item names: {len(unique_items)}",
        f"- Missing quest giver: {len(no_giver)}",
        f"- Collection pages split: {sum(1 for _, s, _ in splits if s)}"
        f" ({sum(len(s) for _, s, _ in splits)} step quests)",
        f"- Backoff events: {len(backoff_events)}",
        "",
        "## Collection page splits",
        *[line
          for title, steps, notes in splits
          for line in ([f"- **{title}** -> {len(steps)} steps" if steps
                        else f"- **{title}** -> not split"]
                       + [f"  - {s['name']} ({len(s['items'])} items)" for s in steps]
                       + [f"  - note: {n}" for n in notes])],
        "",
        "## Quests with no turn-in items parsed (review these)",
        *[f"- {n}" for n in no_items],
        "",
        "## Missing quest giver",
        *[f"- {n}" for n in no_giver],
    ]
    (HERE / "quests-report.md").write_text("\n".join(report), encoding="utf-8")
    print(f"Done: {len(quests)} quests, {len(unique_items)} unique turn-in items.")
    print(f"No-item pages: {len(no_items)} (see quests-report.md)")


if __name__ == "__main__":
    main()
