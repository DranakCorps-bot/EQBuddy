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

**Instances are copies, and a gap across one is not evidence** (David, 2026-08-20 — shipped
in the fix before this design, not deferred to it). Every instance of a zone shares one
timer key, so killing a named in D0 and again in a fresh private instance used to measure
a respawn that never happened. This is a different failure from the loose-bound one above:
a cross-stay gap in the open world is TRUE and merely weak, while a cross-instance gap is
not a bound in either direction. Taking an instance now drops that zone's countdowns, and
learning refuses any gap it cannot prove stayed inside one copy of the zone.

**What the log cannot say, and what we do about it.** The zone line states the difficulty,
so D0 → D2 is proof of a different instance. Re-entering the SAME difficulty is not proof
either way — it may be the instance you kept or a fresh one. There the countdown is kept
and the LEARNING is refused, which is the asymmetry the whole subsystem runs on: **cost a
measurement, never a camp.** If it turns out that instance membership is always lost on
zoning out, that case can be tightened to a clear; see the open question below.

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

## Open questions for David

**Does an instance survive zoning out while you stay logged in?** If you are camping in a
D2 Guk, step out to the adjacent zone and come straight back, is it the same D2 — or a new
one? The zone line looks identical either way, so EQBuddy cannot tell. Today that case
keeps the countdown and refuses to learn from it. If the answer is "always a new one", the
countdown should be cleared there too and the rule gets simpler; if it is "the same one
while you are online", today's behaviour is exactly right and should stay.



`ZoneShare.Export` currently exports any timer with a value, **including one you imported
from someone else and never verified**. That makes shared knowledge self-reinforcing
without new evidence — a rumour that gains a citation each time it is passed on. Excluding
imported-but-unverified values from export is a one-line change; keeping them propagates
useful data faster. Not decided, and deliberately not decided by me: it is a community
call, not a technical one.
