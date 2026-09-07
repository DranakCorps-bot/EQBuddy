#!/usr/bin/env python3
"""The one place a wiki spell NAME becomes a name the GAME writes.

A shipped catalog name is a token the game puts on a log line; a *label* is prose
we write. eqlwiki page TITLES are neither — they carry a disambiguator whenever the
wiki also holds an item, an AA or a proc of the same name:

    Shield of Thorns (Spell)      the wiki also has the ITEM "Shield of Thorns V"
    Kilva's Skin of Flame (Spell)
    Firestrike (Effect)

The game writes none of those. `SpellCatalog.BaseName` (C#) strips a trailing roman
rank and nothing else, so `Shield of Thorns (Spell)` can never meet the log's
`Shield of Thorns V` — and that single unreachable row silently disabled resolution,
Spell Casting Reinforcement, the learned-duration lookup and fade-learn all at once
(PR #407's audit; the owner's chip under-read a ~24 min damage shield as 15:00).

Every promotion that turns `spells.json` into a shipped catalog goes through here, so
a regenerate cannot re-introduce a disambiguator. The strip names the two shapes we
have actually seen; the C# side (`SpellCatalog.IsLogWritableName` +
`SpellNameHygieneTests`) refuses EVERY parenthetical, so a third shape fails a guard
loudly instead of shipping quietly. Strip what is known, guard against the class.
"""

import re

# Trailing wiki disambiguator. Anchored and specific on purpose: a greedy
# `\s*\(.*\)$` would happily eat a real name that happens to end in brackets.
DISAMBIGUATOR = re.compile(r"\s*\((?:Spell|Effect)\)$")


def strip_disambiguator(name: str) -> str:
    """Drop a trailing ` (Spell)` / ` (Effect)` wiki page-title disambiguator."""
    return DISAMBIGUATOR.sub("", name).strip()


def canonical(name: str) -> str:
    """The wiki carries both apostrophe styles (Turgur's / Turgur`s) — one spell.

    Used by the promotions that already folded apostrophes. `fades-harvest.py`
    deliberately takes `strip_disambiguator` alone: it has never folded apostrophes,
    22 harvested names carry a backtick, and changing how those meet a log line is a
    separate call from unblocking an unreachable name.
    """
    return strip_disambiguator(name).replace("`", "'")
