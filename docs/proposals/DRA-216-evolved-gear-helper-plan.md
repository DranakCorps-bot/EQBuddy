# DRA-216 — Evolved Gear Helper, Map integration, POS quest updates

**Planner execution plan.** Red-team of the Founder requirements document
*"EQBuddy Evolved — Consolidated Helper, Quest, Gear Upgrade & Map Integration
Requirements"* (DRA-216, 2026-09-19) against `main` @ `03de0b7f`.

Section numbers below (`S3.2`, `S24 AC 5`) refer to that document. It asks
(S2, S28, S29) that its own **EXISTING / ENHANCEMENT / NEW** labels be
*"corrected where the repository proves a different state"*. Six are wrong in
the Founder's favour — already built — and two are wrong the other way: they
cannot be built at all from data this repo holds. Both directions are below,
each with the file and line that proves it.

---

## 1. Two requirements are DATA-BLOCKED, not merely hard

These are the only two that cannot be started, and what blocks them is the
Founder's own rules, not effort.

### 1.1 S8 `+0..+10` enhancement modelling — no data exists

- `Core/ItemDominance.cs:75` carries, as a committed measurement,
  **`0 of 11,196` catalog item names ending in `+N`**.
- `Core/ItemDominance.cs:23,66` — catalog stats are the wiki's **BASE**
  values. No enhanced-item stat exists anywhere in the repo.
- There is no enhancement rules table, no per-tier data file, and no
  harvester that produces one.

S8.1 requires enhancement behaviour come from *"verified EQL rules;
authoritative EQL data; or explicitly captured item-tier data with
provenance."* **We hold none of the three**, and S20 bans *"invent +N
enhancement math."*

So S8.2 (crossover tier), S8.3 (*"keep your +5 until this hits +6"*),
S24 AC 5/6/7, S26 AC 10, and the S30 question *"At what +N does it actually
become better?"* are **unbuildable today**. An Executor handed them either
guesses — violating S20 — or ships a permanent "comparison unavailable"
string in every row.

### 1.2 S9 exaltations — no catalog, no compatibility data

Every exaltation reference in `src/` is one of three things, none of them a
data model:

- an **import flag only** — `Core/AppSettings.cs:1799-1803` (`IsExaltation`,
  `ExaltationEffect`), filled by `Core/GearChecklistImporter.cs:107` off a
  `socketed-entry` tag. No stats, no compatibility.
- a **log proc line** — `Core/LogParser.cs:55`, `Core/GameEvent.cs:160`,
  `Core/SessionStats.cs:355`.
- a **deliberate refusal** — `Core/SkyRewardAutoComplete.cs:35` excludes the
  `(Exaltation)` aug copy.

There is no exaltation catalog and no compatibility table. S9.4 and S20
forbid claiming compatibility we cannot verify, so S9.2, S9.3, S24 AC 9/10
and S26 AC 11 are **unbuildable today**.

### 1.3 The decision this forces

Both gaps are a **data-sourcing question, not an implementation question**.
Closing either means expanding the eqlwiki harvest, or capturing tier data by
some other route — which is Helm-gated and outside any slice authorization.

**Recommendation: PARK S8 and S9 behind an explicit Founder/Helm ruling on
where verified enhancement and exaltation data come from, and ship the other
eleven sections now.** Parking costs the player nothing visible today, because
the only honest alternative is a surface that says "unavailable" in every row.
Raised as an ASK below; not decided here.

---

## 2. Corrected classification

`F` is the Founder's label, `V` the verified state. The rows where they differ
are the useful ones.

| Req | F | V | Proof |
|---|---|---|---|
| S3.1 three-class identity | EXISTING | **EXISTING** | `CharacterClasses.Resolve`, `.Max` |
| S3.2 stated-class override UI | VERIFY | **VERIFY — Core exists, UI unconfirmed** | `Stated` source in `CharacterClasses` |
| S4.2 multi-select class lens | ENHANCEMENT | **EXISTING — already shipped** | `UI.Shared/QuestClassLens.Offered` (DRA-181 D4, #689); `EqMultiPicker` |
| S4.3 `My Classes` quick-select | ENHANCEMENT | **NEW, but trivial** | no quest-lens match; `QuestClassLens` + `Resolve` make it one call |
| S5.1 Closest-to-Completion lens | NEW | **NEW — lens only** | zero matches in `src/` |
| S5.2 remaining-step math | NEW | **MOSTLY EXISTING** | `QuestChecklistLayout.cs:255,257,313` give `Done`/`Total`/`Progress`; `:270` gives `StateReady` |
| S5.3 group by island | NEW/ENH | **EXISTING — already shipped** | `Core/SkyIslands.cs`, `QuestChecklistLayout.SkyByIsland:805`, `AppSettings.SkyGroupByIsland:661` (DRA-164) |
| S6.1 full catalog coverage | ENHANCE | **EXISTING** | `ItemCatalog`, 11,196 records |
| S7.1 class-aware comparison | ENH | **EXISTING foundation** | `GearUpgrades.Sweep:248`, `ItemDominance` |
| S7.2 class stat relevance | NEW/ENH | **NEW** | no weight profile in `src/` |
| S8 `+0..+10` | NEW | **BLOCKED — §1.1** | `ItemDominance.cs:75` |
| S9 exaltations | NEW | **BLOCKED — §1.2** | §1.2 above |
| S10 acquisition sources | ENH | **PARTIAL** | `GearUpgradeFact.Who` list + `DropZones` (DRA-84 D4) |
| S12 Track Upgrade | NEW | **NEW** | zero matches |
| S13 map target layer | NEW | **NEW join; map EXISTS** | `EQBuddy/MapView.cs`, `Core/ZoneMap.cs` |
| S14 spawn integration | EXISTING | **EXISTING** | `SpawnPointLedger`, `SpawnTimers`, `SpawnCycleLedger` |

**Net correction:** S4.2 and S5.3 are already shipped, so items 1 and 2 of the
Founder's suggested sequence are partly done; and S5.2's arithmetic largely
exists, so Closest-to-Completion is a lens plus a blocker model, not a
calculation engine.

---

## 3. Answers to the S28 red-team questions that change the plan

**Q1 — which NEW items are already built?** S4.2 and S5.3 in full, S5.2 in
large part. See §2.

**Q3 — is `GearUpgrade` the right carrier for a tracked goal? No.** A tracked
goal must be its own domain object. `GearUpgrades.GearUpgrade:117` is a record
recomputed fresh by every `Sweep` from current worn gear; a tracked goal
outlives the sweep, survives restart (S18.3), and holds acquisition state the
sweep knows nothing about. Hanging durable state on a recomputed record ties
goal persistence to catalog and worn-gear churn.

**Q8 — can `SpawnPointLedger` produce gear map points? Yes, with a thin
join.** `SpawnPointLedger.SpawnPoint` already carries `LocX`/`LocY`/`Confirmed`
and a `Mobs` dictionary, so a mob name resolves to zero-or-more confirmed
points directly, and S13.5 (multiple known spawn points) falls out for free.
No second map engine (S13.1, S20).

**Q9 — cleanest representation of remaining Sky work?** Extend
`QuestChecklistGroup`, which already owns `Done`/`Total`/`Progress`/`State`.
The one genuinely missing fact is the **hard prerequisite blocker** (S5.2,
S23 AC 8). One producer (trap 4), read by the class lens, island lens,
ready-to-turn-in and the new lens alike.

**Q12 — better stats, no trustworthy source?** Withhold the row and say so by
count. `Recommendations.WhoRule` already withholds an offer nothing can answer
for and reports its own count (DRA-84 D4) — reuse it rather than inventing a
second rule.

---

## 4. Slice sequence

Ordered by dependency rather than by the requirements document's numbering,
which S29 invites. Each slice is a card with its own done bar.

| Slice | Card | Scope | Route |
|---|---|---|---|
| D1 | DRA-217 | `My Classes` quick-select in the Sky class lens (S4.3) | **Jr** / routine |
| D2 | DRA-218 | `Closest to Completion` lens + hard-blocker model (S5.1/5.2/5.4/5.5) | **Sr** / hard |
| D3 | DRA-219 | Acquisition-source completeness + the six-question join (S10, S11) | **Sr** / hard |
| D4 | DRA-220 | Tracked upgrade goal, base items only, own domain object (S12) | **Sr** / hard |
| D5 | DRA-221 | Map + spawn target layer for a tracked goal (S13, S14) | **Sr** / hard |
| D6 | DRA-222 | Class stat relevance + weapon-aware comparison (S7.2, S7.3) | **Sr** / hard |
| — | PARKED | S8 `+0..+10`, S9 exaltations, and the S12.3 completion conditions that depend on them | blocked on §1.3 |

D5 depends on D4. D1 is the only Jr-routable slice; under the DRA-179 router an
untagged delivery fails closed to Sr, and D2–D6 are tagged `hard` explicitly.

**Regression bar on every slice** — S27's list, notably Sky loot auto-check,
Sky reward completion, Epic auto-check, quest progress persistence,
`UpgradeWorn`/`ReplaceSlot`, map load and `/loc` position, and learned spawn
timers and overrides. A slice that risks one of these ships its regression test
first (S27).

---

## 5. Open asks

1. **Helm / Founder** — rule on §1.3: where verified `+0..+10` and exaltation
   compatibility data come from, or confirm S8/S9 stay PARKED for this
   program.
2. **Helm** — SIGN the D1–D6 sequence so Executor slices may start.

## 6. Operational notes raised with this plan

- The **Jr Executor seat reports `error`** on the company agent list. D1 is the
  Jr-routed slice; the seat needs recovery or D1 fails closed to Sr.
- **`HELM.md` head entries are corrupted** with repeated `Soft LEAVE inventing`
  filler. The rulings remain extractable — Live Holds empty, Play Console OFF,
  DRA-196 arm (b) APPROVED — but the file wants a rotation/repair pass.
- The DRA-196 arm (c) follow-up card Helm asked Planner to file **already
  exists and is discharged**: DRA-199, merged to `main` as `03de0b7f`.
