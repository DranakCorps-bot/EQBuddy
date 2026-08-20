# Spawn learning: the evidence store (design, not yet built)

Written 2026-08-20, after the three fixes in `104b1e2` and a design conversation with
David. This is the plan for the next gate. Nothing here is implemented yet; when it is,
this file becomes the description of what was built and [docs/TestPlan.md](TestPlan.md)
grows the rows.

The audit that produced it is in the commit above and in
[`SpawnTimers.cs`](../src/EQBuddy.Core/SpawnTimers.cs)'s own comments. Read those first —
this file only covers what changes.

---

## Why, in one paragraph

Learning today only ever *tightens*: `LearnFromRekill` refuses a gap at or above the
current duration, `LearnFromSighting` refuses an elapsed at or above it. That makes the
learned value **the running minimum of qualifying observations**, which is the right
estimator and is already what a good tracker does.

The problem is that **the ratchet turns one way and there is no path back.** One bad
sample lowers a timer permanently. The only recovery is the trusted self-heal, and the
shipped catalog has **3 `Trusted` entries and 1 `namedDefaultTrusted` zone out of 1,414
named** — so for 99.7% of the catalog there is no recovery at all except the player
typing over it.

Persisting observations is not about provenance display. It is what makes the minimum
**recomputable**, so a poisoned sample can be outvoted or aged out instead of being
permanent. That is the whole reason to do the work.

## Decisions already made

**Estimator: not standard deviation.** A gap is `true_respawn + how long you took to
notice and kill it`, and the second term is never negative — so the distribution has a
hard wall on the left and a long right tail. Mean ± SD describes a symmetric cloud we do
not have, and its lower half describes a region where observations cannot occur. SD is
also maximally sensitive to exactly the outlier we are defending against: one
went-to-make-tea gap detonates it, which makes using SD to *find* outliers circular. And
n is small — an evening's camp gives 5–20 samples for one named.

**Spread measure: count within a tolerance of the tightest observation.** "6 of 8 gaps
within 30s of 18m43s" is robust by construction, works at n=4, and is a sentence that can
go in front of the player, which an SD never could. It also supplies the corroboration
rule: accept a new low into the cluster when it lands near it, hold it as a *candidate*
when it sits alone below everything else.

> Note the corroboration rule is **not** "require two observations before promoting",
> which is what a first draft of this design proposed. Two samples do not protect a
> minimum estimator — if the bad one is the smaller, it still wins the moment the second
> arrives. What protects is a second observation *near* the candidate.

**DUE stays at the earliest observed; the typical is shown alongside** (David, 2026-08-20).
The two only disagree for a genuinely variable spawn. Alerting at the typical would mean
that roughly half the time the mob has already been up for minutes when the chip fires,
which on a contested named loses it. So: `DUE · usually by 21m`. The safe alert, plus the
number that says when to actually expect it.

**Sightings are the measurement that matters, and we currently throw them away.** A
pre-due sighting establishes *"it was up at T"* with **no reaction-time term in it at
all** — a fundamentally cleaner measurement than any kill-to-kill gap. It is also the only
way to separate the two things that produce identical spread: a spawn that genuinely
varies, and a player who was in the kitchen. No statistic over gaps can tell those apart;
retained sightings can. Today `LearnFromSighting` applies the value and discards the
event.

**A gap only teaches if the player never left the zone** (David, 2026-08-20 — shipped
alongside this design, not deferred to it). Two failures collapse into that one rule.

*Instances.* Every difficulty of a zone shares one timer key, so killing a named at D0,
changing to your own instance and killing it at the spawn point looked like a
twelve-minute respawn — and the mob never respawned, a different copy of it was standing
there. Changing instance means zoning, so the rule catches it without EQBuddy having to
decide which copy is which or pretend the zone line names one.

*Gaps that are not evidence.* "Killed it, went to sell, came back an hour later, killed
it" bounds the respawn at an hour, which is true and worth nothing.

Two earlier and more clever versions of this were tried and dropped, which is worth
recording so they are not reinvented. The first dropped a zone's countdowns on a
difficulty change; that is wrong because zoning in lands you at D0 *before* you rejoin
your own instance, so every trip out and back passes through another difficulty and the
rule would have deleted the camp timer of anyone who stepped out for two minutes. The
second kept countdowns but treated the difficulty as the copy's identity, learning across
a same-difficulty trip because an instance keeps its state. That one is defensible and
still lost: it buys a rare, low-quality measurement in exchange for reasoning nobody can
hold in their head, and the simple rule refuses only gaps that had a whole errand added to
them — rarely the tightest bound seen, rarely the one that would have won.

**Learning is what stops at the zone line, never the countdown.** The named goes on
respawning while you are at the bank and your instance keeps its state while you are away,
so the clock stays true. Cost a measurement, never a camp.

**Camp-specific learning: no** (concluded from the code, 2026-08-20). `CampFor` only pins
a camp when a `/loc` landed within `CampLocWindow` (3 minutes) of the kill. Players type
`/loc` rarely, so most observations would carry no camp and fall straight back to today's
behaviour — the complexity without the benefit. `MultiSpawn` already covers the 10 known
offenders by hand. Revisit only if position ever becomes available more often.

**Do not build a stronger-evidence-overturns-trusted mechanism.** It governs 4 rows.

## Shape

A separate player-data file — `spawn-observations.json`, never the shipped catalog —
holding a **bounded ring per `zone|name`**. Bounded matters for more than disk: today
`SpawnOverrides.Save()` rewrites the whole file on every learn, **on the watcher thread**.
An unbounded journal on that path during a farm session trades a knowledge bug for a
disk-churn bug.

Per observation, only what the log can actually establish: when, the gap or elapsed, which
kind of evidence (named-kill gap / sighting), what started the clock, whether it was one
continuous stay, and whether it was accepted — with the reason when it was not. A rejected
observation is worth keeping precisely because it is what lets the window answer *"why is
this not 4 minutes?"*.

Four states that today are all one field: `observation` → `candidate` → `confirmed` →
`manual`. `SpawnOverride.RespawnSeconds` remains the effective value so nothing downstream
changes; the store sits behind it.

**Migration:** an existing `Learned` value becomes a confirmed value with no observations
behind it — the evidence was never kept, and inventing plausible samples for it would be
the worst thing this design could do. `Sighted` and `Imported` carry across as the kind of
evidence they already record. A malformed observations file must fail exactly like a
malformed overrides file does: lose the evidence, never the player's edits.

## What this buys the player

- The self-heal stops being silent. Today it deletes a learned value on the next kill and
  the player never learns it happened; with observations retained it becomes a visible
  *"rejected: shorter than the measured catalog clock"*.
- A timer can say what it is: `Learned ≤ 18m43s · 6 of 8 within 30s` versus
  `Candidate ≤ 12m01s · 1 observation`.
- A variable spawn can be shown as the range it is instead of a single number that is
  wrong in a different way every cycle.

## Open question for David

`ZoneShare.Export` currently exports any timer with a value, **including one you imported
from someone else and never verified**. That makes shared knowledge self-reinforcing
without new evidence — a rumour that gains a citation each time it is passed on. Excluding
imported-but-unverified values from export is a one-line change; keeping them propagates
useful data faster. Not decided, and deliberately not decided by me: it is a community
call, not a technical one.
