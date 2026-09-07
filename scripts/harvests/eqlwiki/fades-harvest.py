#!/usr/bin/env python3
"""Generate the embedded fade-message catalog from the eqlwiki spell harvest.

Offline: reads spells.json (produced by spells-harvest.py) plus the hand-curated
seed fades-curated.json, and writes the merged catalog straight to
src/EQBuddy.Core/Data/FadeMessages.json. Curated entries are authoritative —
their label/category/note survive, and harvested candidate spells are unioned in.

Born from discussion #64: Spirit of the Puma's self-fade line ("The spirit of
the puma departs.") was on the wiki all along, but the catalog was hand-fed 61
entries while the harvest carried ~400 distinct wear-off messages. Watch rules
can only fire on fades the catalog knows.

Exclusions (each listed in fades-report.md):
  - worn-off-shaped lines ("Your charm spell has worn off." — Befriend Animal):
    the parser's SpellWornOffRx already handles that shape, and the catalog's
    exact-match lookup runs FIRST, so an entry here would shadow it.
  - lines that are also some spell's cast-on-you message ("You feel better."):
    in a live log the line is ambiguous — treating it as a fade would fire
    rules every time the other spell LANDS.
  - "The portal shimmers and fades.": bystander-visible world emote (69 port
    spells), not a personal buff fade.

Outputs:
  ../../../src/EQBuddy.Core/Data/FadeMessages.json  - merged catalog
  fades-report.md                                   - summary + exclusions
"""

import json
import re
from pathlib import Path

import spellnames

HERE = Path(__file__).resolve().parent
SPELLS = HERE / "spells.json"
CURATED = HERE / "fades-curated.json"
OUT = HERE.parents[2] / "src" / "EQBuddy.Core" / "Data" / "FadeMessages.json"
REPORT = HERE / "fades-report.md"

JUNK = {"n/a", "none", "-", "?"}
WORN_OFF_SHAPE = re.compile(r"^Your .+ spell has worn off", re.IGNORECASE)
# Mirrors SpellCatalog.RankSuffixRx (C#): trailing roman-numeral rank.
RANK_SUFFIX = re.compile(r"\s+[IVX]{1,6}$")
BYSTANDER_LINES = {"the portal shimmers and fades."}

# Labels/categories for wear-off lines shared by many unrelated spells, where
# "first spell name" would mislead and the raw sentence is clunky as a label.
FAMILY_LABELS = {
    "the poison has run its course.": ("Poison", "Debuff"),
    "the poison dries from the blade.": ("Blade poison", "Other"),
    "the poison subsides.": ("Poison strike", "Debuff"),
    "you are no longer stunned.": ("Stun", "Debuff"),
    "your feet come free.": ("Root broke", "Debuff"),
    "the potion has worn off.": ("Potion", "Other"),
}

BUFF_TYPES = {
    "Beneficial", "Buff", "Statistic Buff", "Resist Buff", "Utility Beneficial",
    "Movement Buff", "Vision", "Invisibility", "Regen", "Block", "Proc Buff",
    "Beneficial (Group only)",
}


def clean(msg):
    """First line, trimmed, wiki-editor '(?)' suffix dropped; None if junk."""
    if not msg:
        return None
    msg = msg.split("\n")[0].strip()
    if msg.endswith(" (?)"):
        msg = msg[:-4].rstrip()
    if not msg or msg.lower() in JUNK or re.search(r"[\[\]{}<>|]", msg):
        return None
    return msg


def base_name(spell):
    stripped = RANK_SUFFIX.sub("", spell.strip())
    return stripped if stripped else spell.strip()


def canonical(name: str) -> str:
    """The wiki's page-title disambiguator, gone — see spellnames.py.

    `strip_disambiguator` alone, deliberately: this promotion has never folded the
    wiki's two apostrophe styles the way buffs/debuffs/slows do, 22 harvested names
    carry a backtick, and changing how those meet a log line is a separate call from
    unblocking a name no log line can ever reach.
    """
    return spellnames.strip_disambiguator(name)


def main():
    spells = json.loads(SPELLS.read_text(encoding="utf-8"))
    curated = json.loads(CURATED.read_text(encoding="utf-8"))

    cast_lines = set()
    for s in spells:
        c = clean(s.get("msg_cast_on_you"))
        if c:
            cast_lines.add(c.lower())

    # message(lower) -> {"message": display, "spells": {name}, "types": {raw}}
    groups = {}
    excluded = {"worn_off_shape": [], "cast_collision": [], "bystander": []}
    for s in spells:
        msg = clean(s.get("msg_wears_off"))
        if not msg:
            continue
        key = msg.lower()
        if WORN_OFF_SHAPE.match(msg):
            excluded["worn_off_shape"].append((msg, s["name"]))
            continue
        if key in BYSTANDER_LINES:
            excluded["bystander"].append((msg, s["name"]))
            continue
        if key in cast_lines:
            excluded["cast_collision"].append((msg, s["name"]))
            continue
        g = groups.setdefault(key, {"message": msg, "spells": set(), "types": set()})
        g["spells"].add(canonical(s["name"]))
        g["types"].add(s.get("spell_type_raw") or "")

    curated_by_msg = {e["message"].lower(): e for e in curated}

    merged = []
    added_to_curated = 0
    for entry in curated:
        e = dict(entry)
        g = groups.pop(e["message"].lower(), None)
        if g:
            extra = sorted(g["spells"] - set(e["spells"]))
            if extra:
                e["spells"] = e["spells"] + extra
                added_to_curated += len(extra)
        merged.append(e)

    generated = []
    for key, g in groups.items():
        names = sorted(g["spells"])
        bases = {base_name(n) for n in names}
        if key in FAMILY_LABELS:
            label, category = FAMILY_LABELS[key]
        elif len(bases) == 1:
            label = next(iter(bases))
            category = "Buff" if g["types"] <= BUFF_TYPES else "Debuff" \
                if not (g["types"] & BUFF_TYPES) else "Other"
        else:
            # No family name to speak of — the sentence itself is the honest label.
            label = g["message"].rstrip(".")
            category = "Other"
        generated.append({
            "message": g["message"],
            "spells": names,
            "label": label,
            "category": category,
            "confidence": "wiki",
            "note": "msg_wears_off on the spell's eqlwiki page (fades-harvest.py).",
        })

    merged += sorted(generated, key=lambda e: e["message"].lower())
    OUT.write_text(json.dumps(merged, indent=2, ensure_ascii=False) + "\n",
                   encoding="utf-8")

    lines = [
        "# Fade-message catalog report",
        "",
        f"- spells in harvest: {len(spells)}",
        f"- curated entries kept: {len(curated)} "
        f"(+{added_to_curated} harvested candidate spells unioned in)",
        f"- generated entries: {len(generated)}",
        f"- total catalog: {len(merged)} messages, "
        f"{sum(len(e['spells']) for e in merged)} spell candidates",
        "",
        "## Excluded",
        "",
    ]
    for reason, items in excluded.items():
        lines.append(f"### {reason} ({len(items)})")
        for msg, name in sorted(set(items)):
            lines.append(f"- `{msg}` ({name})")
        lines.append("")
    REPORT.write_text("\n".join(lines), encoding="utf-8")
    print(f"wrote {OUT} ({len(merged)} entries) and {REPORT}")


if __name__ == "__main__":
    main()
