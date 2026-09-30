# DRA-42 — Delivery 4a delta: contextual intelligence (post-DRA-40 plan)

Planner, 2026-09-29. Card: DRA-42. This is the plan step the 2026-09-12 weekend
bag §7 declared ("Phase 5/6 plans written after Delivery 2"); DRA-40 landed, so
it is written now — as a DELTA. The 2026-09-12 scope that has since shipped
under other signed cards is subtracted with the evidence, and only the live
remainder is planned.

route: hard — Sr Executor. Cross-cutting by construction: a new Core producer,
a UI.Shared presentation file, the Guide room, the map, and phone parity. Well
past the Jr small-card bar, and the class of the work (what the app claims a
player can do HERE) is why a plan exists at all.

challenge: dra-42-contextual-intelligence -> NOT-ENGAGED (no C-test fires) as of 2026-09-29

Gate walk, recorded: evaluated C1–C5 (ops `CHALLENGER_PROCESS_GATE_SPEC.md` §3)
against this ask on 2026-09-29. C1 does not fire — the bag plan (Founder in
session = plan SIGN) itself declared this plan step, so this is the anticipated
escalation, DRA-251's shape, not a reversal or narrowing; the subtracted halves
shipped under their own cards (DRA-46, DRA-216 D5) and subtracting them records
delivery rather than un-deciding anything. C2/C3/C4 plainly absent — no seat or
routing change, no spend, and every slice extends a shipped surface, discarding
none. C5 does not fire — a product feature plan; no gate, guard, or authority
rule moves (D4's enumeration test ADDS a pinned must-list, loosening nothing).
No wake; the Challenger seat bills nothing; this line is the Planner
gate-status record.

## Delta audit — what the 2026-09-12 scope already shipped (subtracted, with evidence)

- **Auto-tick from logs via `ItemNames`: SHIPPED as a mechanism.**
  `UI.Shared/GuideProgressRouter.cs`'s six homes (DRA-46) with the persisted
  ledger gate (trap 85 — `QuestLedgerFeed`/`ChecklistLedgerSync`; ticks only on
  loot `QuestLedgerStore.RecordLoot` accepts). The residual is an AUDIT slice
  (D4), not a build.
- **Map targets for tracked upgrades: SHIPPED** (DRA-216 D5,
  `Core/GearTargets.cs` + the dashed ring + phone parity). Guide-step
  `Where` → map is a DIFFERENT join — that layer answers "is this dot one of
  the upgrades I decided to go get", not "does this dot serve the guide step I
  am on" — and remains in scope (D3).
- **while-you're-here / do-not-leave-yet: NOT shipped.** `git grep` over
  `src/` and `tests/` on `main` matches nothing (measured 2026-09-29). This is
  the card's live scope (D1/D2).

## Slices

### D1 — while-you're-here (requirements §18)

One producer in Core: given the character's CURRENT ZONE — the zone name the
log printed, the `PrimaryZone` observation; no real-time location is implied
beyond what the log legitimately states (§20's own lock) — and the open
guide/quest state, answer "open objectives actionable in this zone".

- **The join key is the ZONE**, exact title then `ZoneMapFiles.IdentityKey`
  and nothing looser — the `ZoneLevels`/`ZoneEras`/`GearTargets` rule
  verbatim. A non-place is refused through `TradeskillMaterials.IsPlace`.
- **Sources are the producers that already exist** (trap 4 — recompute
  nothing): the drawn objective set (`GuideStores` / `Drawn()`), authored
  steps' zone, and harvested `Collect` items' drop zones through
  `ItemCatalog`. Creature matching, where it is needed at all, is
  `SpawnCatalog.NameMatches`, deliberately not fuzzy.
- **Grouping is §18's** — required-here / relevant-rewards / optional — driven
  by shared structured references, never separately authored duplicate text.
- **Words live in one UI.Shared presentation file**; every cap says what it
  held back (trap 50); nothing calls a camp safe, easy or survivable
  (HOME-006's ban). Drawn in the Guide room; the phone inherits through the
  shared layer, READ-ONLY, with the page-side must-list row added in the same
  slice (trap 32 / DRA-84 D5 — a sentence the page is sent but never draws
  passes every projection test there is).

### D2 — do-not-leave-yet (requirements §19), the log-only reading

Log-only means EQBuddy learns a transition AFTER "You have entered …" prints.
§19's pre-transition prompt is not honestly buildable from the log, so this
slice ships the two shapes that are:

- **A "Before you leave <zone>" block on the D1 surface while in-zone** — the
  standing checklist of unresolved objectives here, D1's answer re-grouped by
  the SAME producer (trap 4: one producer, two groupings).
- **On an observed zone CHANGE, a notice on the desktop/phone Guide surface**
  naming what was left unresolved in the zone just departed, with the door
  back to its rows. NOT an overlay chip (no deadline-with-an-action — the
  surface table's test) and NOT a modal "Continue Anyway" dialog (the game is
  on the player's monitor; everything else goes somewhere else).
- Applies to ordinary zones as §19 asks, because it is the same producer over
  the same join — Sky islands are not special-cased.

### D3 — map targets from guide-step `Where` (requirements §20)

- **No second map engine** — the S13.1/S20 rule verbatim: `SpawnPointLedger`
  already archives the points, `ZoneMap.FromLoc` already places them, the map
  already draws. The one new question is "does this dot serve an open guide
  objective", computed by a reader, never a store.
- Join: the objective's zone exact-then-`IdentityKey`; the creature via
  `SpawnCatalog.NameMatches` and deliberately not `NameMatchesFuzzy` — the
  `GearTargets` reason holds verbatim (a false ring is a dot a player travels
  to).
- **Visually distinct from the gear-target dashed ring** (a second meaning may
  not reuse the first's mark); the executor picks the mark with `BEVEL.md`
  read first. The display toggle follows `ShowGearTargetsOnMap`'s shape
  exactly: one `AppSettings` switch, applied in the one producer both views
  read (trap 33), written by an always-visible `EqChip` in the map toolbar.
- Phone: READ-ONLY parity, and the guide-target flag joins the map fingerprint
  (trap 72 — tracking moves no coordinate).

### D4 — auto-tick residual audit (no build unless the audit finds a gap)

- Enumerate every objective shape the catalogs can emit against
  `GuideProgressRouter`'s six homes, and pin the enumeration in a test so the
  next shape added must declare its home (trap 34's must-list shape).
- Assert `TalkToNpc` and `Transcribed` are manual-by-design — no log signature
  exists for them, and §5.9 (never fabricate certainty) plus §17 (manual state
  beats weak inference) is the reason, stated in the test name.
- If the audit finds a genuinely tickable shape left unticked, that is a stop
  point: it escalates as its own ask rather than growing this slice.

## Order, verification, constraints

- Order: **D1 → D2 → D3**, with **D4 free to land any time** (it is
  independent and small). D2 consumes D1's producer, so it never starts first.
- Each slice verifies to its class (V1: relevant unit + targeted E2E — all
  four are user-visible except D4, which is V0), carries its `WhatsNew.json`
  entry per player-noticeable delivery, and updates `docs/TestPlan.md`.
- Card constraints restated, unchanged: Claude Code CLI only; claim key is
  this card (`claim-seat.ps1`, resolved form); merge bar `build-and-test` +
  `e2e-windows`; Play Console OFF; no tag, no `release.ps1`, no signing
  change.
- A slice that outgrows this declared boundary stops and escalates — that
  escalation is a new plan-SIGN ask and takes the gate there.

**Ask:** SIGN this plan (the whole declared slice sequence, Sr Executor
route), or HOLD. Not needs-david — no consequence-list door is touched: no
other-player measurement, no off-machine send, no release, no public prose, no
wiki-policy change.
