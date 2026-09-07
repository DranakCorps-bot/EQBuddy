#!/usr/bin/env python3
"""Generate the embedded slow-debuff catalog from the eqlwiki spell harvest.

Offline: reads spells.json (produced by spells-harvest.py) and aas.json
(aas-harvest.py) and writes src/EQBuddy.Core/Data/SlowSpells.json — the catalog
behind the attack-speed-debuff alert (discussion #94, Frankthetankk).

A slow is any spell whose slot effects decrease Attack Speed / Melee Haste.
The alert keys on the spell's cast-on-you message ("You feel lethargic."), so
the catalog groups spells BY MESSAGE — several slows share a landing line
("You feel drowsy." belongs to the whole insect line), and honesty demands the
chip show the possible range rather than pick one.

Exclusions (each listed in slows-report.md):
  - beneficial slows (Torpor, Rejuvenation, Aura of Marr): self-chosen
    tradeoff buffs; alerting "you are slowed!" on your own Torpor is noise.
    This also removes the "Your wounds begin to heal." collision — that line
    is the regen tick (RegenSpells.json) far more often than it is a slow.
  - spells with no cast-on-you message on their wiki page (the Shackle line):
    nothing to match; they fade silently into the alert's blind spot and are
    reported so wiki edits can fill the field.
  - any landing line that is ALSO a fade line in FadeMessages.json or the
    regen tick line: exact-match catalogs must not fight over a message.

Ambiguity that can't be excluded (#116, Fennec-Halas): a landing line may also
be some OTHER spell's wear-off line — "You slow down." lands the Deeds slows
AND fades Selo's haste songs, and the fade catalog doesn't carry those songs so
the exclusion above never sees the clash. Dropping the entry would blind the
alert to real slows, so the entry instead carries "fadeOf": the haste spells
that wear off with this message. The tracker suppresses a landing that follows
"You forget <one of those>." — the haste ending, not a slow arriving.

Cures: spells whose effects decrease poison/disease/curse counters, with
counters-removed-per-cast, plus the activatable cure AAs (Purify Soul/Body)
carried as notes — their numbers live in prose, not slot effects.

Outputs:
  ../../../src/EQBuddy.Core/Data/SlowSpells.json  - the catalog
  slows-report.md                                 - summary + exclusions
"""

import json
import re
from pathlib import Path

import spellnames

HERE = Path(__file__).resolve().parent
SPELLS = HERE / "spells.json"
AAS = HERE / "aas.json"
FADES = HERE.parents[2] / "src" / "EQBuddy.Core" / "Data" / "FadeMessages.json"
REGENS = HERE.parents[2] / "src" / "EQBuddy.Core" / "Data" / "RegenSpells.json"
OUT = HERE.parents[2] / "src" / "EQBuddy.Core" / "Data" / "SlowSpells.json"
REPORT = HERE / "slows-report.md"

SLOW_RX = re.compile(r"Decrease (?:Attack Speed|Melee Haste) by (\d+)%(?:\s*\(L\d+\)\s*to\s*(\d+)%?)?")
COUNTER_RX = re.compile(r"Increase (Poison|Disease|Curse) Counter by (\d+)(?:\s*\(L\d+\)\s*to\s*(\d+))?")
CURE_RX = re.compile(r"Decrease (Poison|Disease|Curse) Counter by (\d+)(?:\s*\(L\d+\)\s*to\s*(\d+))?")


def canonical(name: str) -> str:
    """See spellnames.py — folds apostrophe styles (Turgur's / Turgur`s) AND strips the
    wiki's ` (Spell)` / ` (Effect)` page-title disambiguator, which the game never writes."""
    return spellnames.canonical(name)


def slow_range(spell):
    for e in spell.get("slot_effects") or []:
        m = SLOW_RX.search(e.get("effect") or "")
        if m:
            lo = int(m.group(1))
            hi = int(m.group(2)) if m.group(2) else lo
            return (min(lo, hi), max(lo, hi))
    return None


def counter_info(spell):
    for e in spell.get("slot_effects") or []:
        m = COUNTER_RX.search(e.get("effect") or "")
        if m:
            lo = int(m.group(2))
            hi = int(m.group(3)) if m.group(3) else lo
            return (m.group(1), min(lo, hi), max(lo, hi))
    return ("", 0, 0)


def main():
    spells = json.loads(SPELLS.read_text(encoding="utf-8"))
    aas = json.loads(AAS.read_text(encoding="utf-8"))["abilities"]
    fade_msgs = {e["message"].lower() for e in json.loads(FADES.read_text(encoding="utf-8"))}
    regen_msg = json.loads(REGENS.read_text(encoding="utf-8"))["message"].lower()

    excluded = []          # (name, reason)
    by_message = {}        # landing line -> {name -> spell record}

    for s in spells:
        rng = slow_range(s)
        if not rng:
            continue
        name = canonical(s["name"])
        if s.get("beneficial") is True:
            excluded.append((name, "beneficial — a self-chosen tradeoff buff, not an attack"))
            continue
        # The Torpor family (#132, bjstrange): Stoicism/Nonchalance/Impassivity
        # leave the wiki's beneficial field BLANK, so the guard above missed them
        # and a shaman buffing you fired "you are slowed!". A "slow" that also
        # HEALS its target is a tradeoff buff whatever the field says.
        if any(re.search(r"increase (current )?hit ?points", e.get("effect") or "", re.IGNORECASE)
               for e in s.get("slot_effects") or []):
            excluded.append((name, "heals its target — a Torpor-family tradeoff buff, not an attack"))
            continue
        msg = (s.get("msg_cast_on_you") or "").strip()
        if not msg:
            excluded.append((name, "no cast-on-you message on the wiki page — nothing to match"))
            continue
        if msg.lower() in fade_msgs:
            excluded.append((name, f"landing line {msg!r} is already a fade line — catalogs must not collide"))
            continue
        if msg.lower() == regen_msg:
            excluded.append((name, f"landing line {msg!r} is the regen tick line — catalogs must not collide"))
            continue

        ctype, clo, chi = counter_info(s)
        rec = {
            "name": name,
            "pctMin": rng[0],
            "pctMax": rng[1],
            "counterType": ctype,
            "counterMin": clo,
            "counterMax": chi,
            "durationSeconds": s.get("duration_seconds"),
        }
        group = by_message.setdefault(msg, {})
        old = group.get(name)
        if old:  # apostrophe-variant twin: union the ranges
            rec["pctMin"] = min(rec["pctMin"], old["pctMin"])
            rec["pctMax"] = max(rec["pctMax"], old["pctMax"])
            rec["counterMin"] = min(rec["counterMin"], old["counterMin"]) if old["counterMin"] else rec["counterMin"]
            rec["counterMax"] = max(rec["counterMax"], old["counterMax"])
            rec["durationSeconds"] = rec["durationSeconds"] or old["durationSeconds"]
        group[name] = rec

    # Spells (typically hastes) whose WEAR-OFF line collides with a slow landing
    # line — see the fadeOf note in the module docstring.
    wearoff_by_msg = {}
    for s in spells:
        w = (s.get("msg_wears_off") or "").strip()
        if w:
            wearoff_by_msg.setdefault(w.lower(), set()).add(canonical(s["name"]))

    messages = []
    haste_landings = set()
    for msg, group in sorted(by_message.items()):
        cands = sorted(group.values(), key=lambda r: r["name"])
        label = cands[0]["name"] if len(cands) == 1 else "Slow"
        entry = {"message": msg, "label": label}
        fade_of = sorted(wearoff_by_msg.get(msg.lower(), set()) - set(group))
        if fade_of:
            entry["fadeOf"] = fade_of
            # The colliding songs' LANDING lines (David, 2026-08-13: "Your feet
            # move faster." = a Selo's pulse on a groupmate, the only witness a
            # bard's songs leave in another player's log). The tracker reads a
            # shared slow line soon after one as the haste lapsing.
            fade_lower = {f.lower() for f in fade_of}
            for s in spells:
                if canonical(s["name"]).lower() in fade_lower:
                    on_you = (s.get("msg_cast_on_you") or "").strip()
                    if on_you:
                        haste_landings.add(on_you)
        entry["spells"] = cands
        messages.append(entry)

    # Cure spells: counter reducers, grouped by counter type, strongest first.
    # Target matters: pet mends (Renew Bones) also decrease counters — on the PET;
    # guidance for curing yourself must only name spells that can target you.
    cures = {}
    for s in spells:
        if s.get("beneficial") is False:
            continue
        if s.get("target_type") == "Pet":
            continue
        for e in s.get("slot_effects") or []:
            m = CURE_RX.search(e.get("effect") or "")
            if not m:
                continue
            lo = int(m.group(2))
            hi = int(m.group(3)) if m.group(3) else lo
            classes = "/".join(sorted({c["class"] for c in (s.get("classes") or []) if c.get("class")}))
            cures.setdefault(m.group(1), {})[canonical(s["name"])] = {
                "name": canonical(s["name"]),
                "perCastMin": min(lo, hi),
                "perCastMax": max(lo, hi),
                "classes": classes,
            }
    cure_list = [
        {"counterType": ctype, "options": sorted(opts.values(), key=lambda o: -o["perCastMax"])}
        for ctype, opts in sorted(cures.items())
    ]

    aa_cures = []
    for a in aas:
        text = a.get("effect_text") or ""
        if re.search(r"cures? .*(counters|detrimental effects)", text, re.IGNORECASE) and a.get("class"):
            m = re.search(r"Refresh Time: (\d+):(\d+):(\d+)", text)
            refresh = f"{int(m.group(1)) * 60 + int(m.group(2))} min refresh" if m else "activated ability"
            counters = re.search(r"cures? (?:a target[^.]*? of |you of )?(?:up to )?(\d+) ([a-z, ]+?)(?: counters| detrimental effects)", text, re.IGNORECASE)
            note = f"{counters.group(1)} {'counters' if 'counter' in text.lower() else 'detrimental effects'}, {refresh}" if counters else refresh
            aa_cures.append({"name": a["name"], "class": a["class"], "note": note})

    catalog = {
        "comment": "Attack-speed debuff (slow) catalog for the slow alert (#94). "
                   "Grouped by cast-on-you landing line; a shared line lists every "
                   "candidate and the alert shows the honest range. Generated by "
                   "scripts/harvests/eqlwiki/slows-harvest.py from the eqlwiki spell "
                   "harvest — regenerate, never hand-edit. Exclusions in slows-report.md.",
        "messages": messages,
        "cures": cure_list,
        "aaCures": aa_cures,
        "hasteLandings": sorted(haste_landings),
    }
    OUT.write_text(json.dumps(catalog, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")

    n_spells = sum(len(m["spells"]) for m in messages)
    shared = [m for m in messages if len(m["spells"]) > 1]
    lines = [
        "# Slow-debuff catalog report",
        "",
        f"- {n_spells} slow spells across {len(messages)} landing lines "
        f"({len(shared)} lines shared by several spells)",
        f"- cures: " + ", ".join(f"{c['counterType']} ×{len(c['options'])}" for c in cure_list),
        f"- AA cures: " + ", ".join(a["name"] for a in aa_cures),
        "",
        "## Shared landing lines (alert shows the range)",
        "",
    ]
    for m in shared:
        names = ", ".join(s["name"] for s in m["spells"])
        lines.append(f"- {m['message']!r}: {names}")
    lines += ["", "## Excluded", ""]
    for name, reason in sorted(excluded):
        lines.append(f"- {name}: {reason}")
    lines += [
        "",
        "## Verification notes",
        "",
        "- Landing lines are wiki msg_cast_on_you fields verbatim; in-game text wins",
        "  if they differ. Field reports welcome (#94 — Frankthetankk offered).",
        "- 'You are slowed by the  mist of the seas.' carries the wiki's double space",
        "  verbatim — verify against a real log line before trusting the match.",
    ]
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{n_spells} slows -> {OUT.name}; report -> {REPORT.name}")


if __name__ == "__main__":
    main()
