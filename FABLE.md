# Fable inbox

Plans for Claude, not a work order. **Claude: take an approved item, then delete it**
(or leave only what is still planned).

EQBuddy is the incubation lab. We refine the finished state here. The organization
iterates the same way as the software (observe → diagnose → change → verify).

## When this file is in play

**V2–V3 only.** Cross-cutting architecture, significant refactor, ambiguous root cause,
security/privacy/migration, complex parallel decomposition.

Fable 5 writes the plan. Helm last-looks. David marks it `approved`. Opus 5 executes
only approved items.

**V0–V1 does not belong here.** Cosmetic, mechanical, localized, straightforward work
stays one Claude loop. Do not pay a planning-handoff tax without reason.

This is not a fourth gate on Scribe intake or Bevel critique. Those files stay their
own inboxes. Org-level proposals do not go in this file.

There is no Fable Grok Bot. Point Fable 5 at this file.

## Item shape

- **Priority:** `approved` (David said build it) · `waiting` (needs Helm QA or David) · `someday`
- **Class:** `V2` or `V3` (if you cannot say why it is not V0–V1, it does not go here)
- **Source:** discussion/issue, Bevel/Scribe item, or David’s words
- **Plan:** architecture, risks, decomposition, verification, what is out of scope
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`.

---

## Per-page wiki re-check — the ✦ flags are stale for up to seven days

- **Priority:** `waiting` — needs a Fable 5 plan and David's `approved`. **Stub written by
  Claude (Opus 5), 2026-08-21.** I did not write the plan and did not implement it.
- **Class:** `V2`
- **Source:** #226 (Frankthetankk, LeBigNasty), continuing #65 where a re-check button was
  queued and never built.

### The problem, and the cause — confirmed, not hypothesised

The ✦ "new to wiki" marker on Drops rows compares your observed loot against a **7-day
per-page cache**. `EqlWikiMobs.CacheLifetime` and `EqlWikiItems.CacheLifetime` are both
`TimeSpan.FromDays(7)`. So when a player corrects a wiki page — which is *exactly what the
✦ asks them to do* — the flag they were trying to clear stays lit for up to a week, and
there is nothing in the app that can hurry it.

That is the part worth holding on to: **the feature's own call to action is what produces
the bug.** A contributor does the thing we asked, comes back, and the app tells them the
wiki still does not know. LeBigNasty reported it as false positives; Frankthetankk connected
it to #65.

**Ruled out:** the `+N` tier theory. `WikiContribution.Classify` folds both sides through
`QuestCatalog.BaseItemName`, which strips a trailing `+N`, and LeBigNasty's 092734 screenshot
has tiered items on **both** sides of the flag (Eyerazzia +4 unflagged; Fetid Skin flagged).

### Why this is not V0–V1

I would otherwise have just built it. It does not qualify because:

1. **It reaches Core plus both UIs plus two surfaces** — the cache in `EqlWikiMobs` /
   `EqlWikiItems`, the ✦ row in `EQBuddy/DropsCardView.cs` and
   `EQBuddy.Avalonia/DropsCardView.cs`, and `WikiPackWindow` in both.
2. **It puts on-demand network I/O behind a surface that today only reads a cache.** That is
   a new failure mode with real UX in it: offline, slow, page moved, redirect (trap 3 —
   record the SERVED title, never the requested one). None of those states exist yet.
3. **There is an external-consequence question I should not answer alone.** "Re-read
   everything before the pack exports" could mean dozens of requests to eqlwiki in a burst,
   from every user who opens that window. eqlwiki is a volunteer wiki and we are asking it
   for a favour. Per-row on demand, whole-window on open, and a rate limit are three
   different products.
4. **There is a product question underneath it:** is the answer a re-check button, a shorter
   TTL for pages the player has personally edited, or an "I just fixed this" action that
   invalidates one page? A button is the obvious answer and obvious answers to caching
   questions are frequently wrong.

### Already shipped (must not be fought)

- The ✦ marker, its tooltip and its three states (`PageMissing`, `PageHasNoLoot`,
  `NewToPage`) — `WikiContribution.Classify`.
- The wiki pack window, and creature names as real links (shipped 1.99.0, #226's other half).
- Trap 3: `redirects=1` means the served page is not the requested one; record
  `WikiPageText.Title`. This bug has been caused twice already by getting that wrong.
- The weekly wiki refresh pipeline, which only ever *flags* curated catalogs.

### Checked

Grepped and read: `EqlWikiMobs.cs`, `EqlWikiItems.cs`, `WikiContribution.cs`,
`QuestCatalog.BaseItemName`, both `DropsCardView.cs`. Confirmed both cache lifetimes and the
`+N` folding by reading the source, not by inference. **Not checked:** how the cache is keyed
on disk, whether a forced refresh path already exists anywhere, and what the pack window does
on open. Those are for the plan.

---

## Plane of Sky: spawns that are not timed at all need a spawn TYPE, not a data patch

- **Priority:** `waiting` — needs a Fable 5 plan and David's `approved`. **Stub written by
  Claude (Opus 5), 2026-08-21.** Investigation only; nothing implemented.
- **Class:** `V2`
- **Source:** #109 follow-up (Frankthetankk, 2026-08-19), with a screenshot from a personal
  Plane of Sky instance.

### What he reported

In an instanced Sky: **The Spiroc Guardian marked DUE**, and **Bzzazzt / Bzzzt / Bazzzazzt
each carrying ~1:01 countdowns** that are really just how long each kill took. Neither is
timer-driven at all — the bees are a chain (each spawns the moment the previous dies) and the
Spirocs are player-triggered (kill specific trash to summon). DUE is worse than imprecise
here: it says a passive timer just elapsed when in fact nothing happens until somebody acts.

### What I verified (and where he is wrong, which is the useful part)

His hypothesis was *"Plane of Sky isn't in the achievements-dump raid-boss list #109's
suppression keys off."* **It is.** `RaidTargets.json` carries `"The Plane of Sky"` with
eleven bosses. What is missing is narrower and it lines up exactly with his screenshot:

| In the spawn catalog's Sky zone | In the raid-boss list? |
|---|---|
| `The Spiroc Lord` | **yes** → suppressed |
| `The Spiroc Guardian` | **no** → gets `namedDefaultSeconds` |
| `Bazzt Zzzt` | **yes** → suppressed |
| `Bzzzt` | **no** → gets `namedDefaultSeconds` |

So the two names showing wrong countdowns are precisely the two absent from the list. That is
a confirmed mechanism for the per-entry half.

**Still unknown, and it needs HIS log:** whether the zone-level fallback should have caught
them anyway. `SpawnTimers` also suppresses when `_currentZoneInstanced && zone.RaidZone`, and
`SpawnCatalog.IsInstancedZoneName` only recognises `"<zone> N (Awakened|Adaptive|Fused|Refined)"`
and `"<zone> - Solo|Group"`. Nobody here knows what a personal Sky instance actually prints on
zone-in. He has offered the line; it has been asked for on the thread.

### Why this is not V0–V1, and why the obvious fix is a trap

The obvious fix is to add `The Spiroc Guardian` and `Bzzzt` to `RaidTargets.json`. **Do not.**
That list also drives the Raids card and the achievements cross-reference, so it means "this
is a raid target you can clear", and these are trigger-spawned adds. Using it to mean "do not
show a countdown" is trap 4 — one entry, two meanings — and the Raids card would start
listing trash as raid bosses.

What the domain actually lacks is a **spawn type**. Frankthetankk names the two honestly:
`chained` (spawns on the previous one's death, no interval) and `player-triggered` (requires
an action, never time). Each wants its own non-countdown presentation rather than being forced
through the timer UI. That is a new concept in `SpawnCatalog`/`SpawnTimers`, a curated-catalog
schema change, and a display decision on the widget, the map, the breakout AND EQBuddy Mobile.
It also has to be reconciled with #185's auto-discovery, which assumes an interval exists to
learn — a chained spawn would teach it a number that means nothing.

**And it is catalog data about the game**, so CLAUDE.md's rule applies: match the wiki, and
departing from it needs a confirmed observation rather than an expectation. He is explicit
that he has no overworld Sky data and is only describing instances.

### Already shipped (must not be fought)

- `#109` raid-instance suppression: per-entry `RaidInstanced` (from `RaidTargets`) plus the
  zone-level `_currentZoneInstanced && zone.RaidZone` rule. A player-TYPED duration outranks
  both, everywhere — that is deliberate and must survive.
- `SpawnCatalog.EffectiveSeconds` returns null for raid-instanced entries.
- Persisted pre-#109 countdowns heal at load through `SuppressedRaidInstance`.
- `#185` (bjstrange) auto-discovery of named mobs from combat text — still open, and the
  interaction above is real.

### Checked

Read: `SpawnCatalog.MarkRaidInstanced`, `MatchesZoneName`, `IsInstancedZoneName`,
`InstanceTier`, `SpawnTimers` lines 250–310 and `SuppressedRaidInstance`, plus the Sky entries
in `SpawnCatalog.json` and `RaidTargets.json`. **Not checked:** what a personal Sky instance
prints on zone-in (waiting on the reporter), and whether the overworld Sky bosses have real
timers at all.

---

*No other items yet.*
