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
| D7 | *unfiled — §7* | The card's three terminal steps: Planner review, the email, the push to live | **NOT SIGNED — §7** |
| — | PARKED | S8 `+0..+10`, S9 exaltations, and the S12.3 completion conditions that depend on them | **PARKED by Helm — §5** |

D5 depends on D4. D1 is the only Jr-routable slice; under the DRA-179 router an
untagged delivery fails closed to Sr, and D2–D6 are tagged `hard` explicitly.

**D1 runs on Sr until the Jr seat proves it can start.** Helm's ruling is
fail-closed: seat `80e720ca` still reports `error` (`process` adapter, *"Process
adapter missing command"*), and D1 may only be kicked on Jr once DRA-201 clears
`errorReason` **and** a throwaway wake against no assigned work proves start. A
second Jr seat is not to be invented to get around this.

**Regression bar on every slice** — S27's list, notably Sky loot auto-check,
Sky reward completion, Epic auto-check, quest progress persistence,
`UpgradeWorn`/`ReplaceSlot`, map load and `/loc` position, and learned spawn
timers and overrides. A slice that risks one of these ships its regression test
first (S27).

---

## 5. Helm ruling — RULED 2026-09-19, LIVE ASK discharged

Both asks below are **answered**. `HELM.md` tip `5bc99b4f` is the ruling
(mirrored as a PR #705 comment). Do not re-ask either question inside this
program.

1. **§1.3 S8/S9 — PARKED.** S8 `+0..+10` and S9 exaltations stay parked for
   the whole DRA-216 program, together with the acceptance that depends on
   them: S12.3, S24 AC 5–7/9/10, and S26 AC 10–11. **No eqlwiki harvest and no
   other tier/exaltation data capture is authorized in this land.** A surface
   may honestly say a comparison is unavailable; it may **not** invent `+N`
   arithmetic or unverified exaltation compatibility. Un-parking needs a new
   Helm ruling, not an Executor judgement.
2. **D1–D6 — SIGNED** as tabled (DRA-217..222). The sequence is authorized in
   order, on green gates; a slice that outgrows its declared boundary stops and
   escalates rather than widening.

Plan PR #705 merged to `main` as `90b5a866` at the pinned head `20921566`.

## 6. Operational notes raised with this plan

- The **Jr Executor seat reports `error`** on the company agent list. D1 is the
  Jr-routed slice; the seat needs recovery or D1 fails closed to Sr. **Helm kept
  the DRA-201 carry-out ACCEPTED and ruled D1 fails closed to Sr** until that
  seat both clears `errorReason` and proves start. Re-read the seat through
  `GET /api/agents/{id}` — the company agent LIST is not a preflight, and a
  redacted `adapterConfig` reads as a plausible measurement rather than a fix.
- **`HELM.md` head entries are corrupted** with repeated `Soft LEAVE inventing`
  filler. The rulings remain extractable — Live Holds empty, Play Console OFF,
  DRA-196 arm (b) APPROVED — but the file wants a rotation/repair pass. Helm
  **authorized a Soft rotate/repair of the dated tips only**: Holds, Wakes,
  Retired and standing blocks stay, the baseline is not raised, and nothing may
  channel-wipe or `rotate --apply` history away.
- The DRA-196 arm (c) follow-up card Helm asked Planner to file **already
  exists and is discharged**: DRA-199, merged to `main` as `03de0b7f`.

---

## 7. D7 — the card's terminal steps, which §4 did not cover

**This is a gap in the plan Helm signed, and it is mine.** DRA-216's card does
not end at D6. After the work it asks for three more things, in order:

> *"I want you to review the finished changes. Once you are happy with them,
> email David.Edwards08@gmail.com (through Helm or Dranak, whomever is
> responsive) and let me know. Then push the changes to the live environment on
> my desktop."*

§4 tabled six implementation slices and stopped, so those three steps have no
slice, no owner and no gate anywhere. They are written down here so that the
last one cannot be reached by drift.

### 7.1 Step 1 — Planner review. Running already, per slice, not at the end.

Taken **per slice as each lands**, not once over the finished program. The
Founder asked to be told when I am happy with the changes; he did not ask for a
single review at the end, and a review held to the end arrives after the author
has lost the context that would let them answer it cheaply.

Already exercised, and it paid: D1 (DRA-217) was reviewed on 2026-09-20 against
its own six-point done bar and five of six were clean, with one change requested
— `RefreshMyClassesAction` sits above `Refresh`'s signature gate while
`QuestsRoom` calls `PaintNow()` every tick, so `SetActions` destroys and rebuilds
the new `My Classes` button once a second (trap 46). The hover cannot be held on
screen and a press that straddles a tick is swallowed. **The E2E suite could not
have caught it**: `ProbeLens("myclasses")` calls `SelectMyClasses()` directly and
never touches the Button that is destroyed.

**A defect found after its PR merged does not reopen the PR** — #707 merged on
green gates at `a23335f1` while the review was being written, which is the
signed-sequence rule working as designed. The finding goes back on the slice's
own card as a fresh branch off `main`, and the card returns to `in_progress`.
What it must NOT do is become a follow-up card nobody sequences: **a slice is
not finished for D7's purposes until its review findings are closed**, and D7
step 2 cannot honestly report a program the Founder would call done while one is
open.

### 7.2 Step 2 — the email. Pre-authorized; route is Helm, then Dranak.

**Not a consequence-list door.** Item 3 covers *"anything public under the
project's name"*; a private message to the Founder about his own project is the
reporting duty, not a public statement. No question goes to David to ask whether
to send David an email.

Route as the card says — *"through Helm or Dranak, whomever is responsive"*.
Helm first (it is the seat that already signs postures and it is the one with a
back-channel wake), Dranak second. **Responsiveness is measured, not assumed**:
read `lastHeartbeatAt` off `GET /api/agents/{id}`, never the agents LIST and
never `lastActiveAt`, which is `null` on every seat in this company including a
running one (DRA-201's own correction).

What the email must contain, so that it is a report and not an announcement:
every slice that landed with its card and commit, **every review finding still
open**, everything PARKED with the reason (S8, S9, S12.3 and the acceptance that
depends on them), and the one decision left for him — step 3.

### 7.3 Step 3 — "push the changes to the live environment on my desktop". NOT authorized by this plan.

This is a **release**, and it is the one hard gate. Recorded here as the standing
rule rather than as a new judgement:

1. **The release go is explicit and contemporaneous.** CLAUDE.md: *"Hold releases
   until David explicitly says ship"*, and the go *"is the one hard gate, and it
   stays."* The card's *"then push the changes"* was written on 2026-09-19,
   before a line of D1 existed. It cannot be a decision about what the work turned
   out to be, so **it is read as intent and not as the ship word.** The ship word
   is asked for in the step-2 email and given after it.
2. **Fable reviews the release before David is asked.** Order is fixed: gates
   green -> Fable reviews -> then ask. So step 2's email is what *starts* that
   order, not what ends it.
3. **Nothing ships unsigned.** Whatever *"the live environment on my desktop"*
   turns out to mean, it goes through `scripts/release.ps1 -Tag vX.Y.Z` with a
   `<Version>` bump and a `WhatsNew.json` entry, signed and verified through
   Azure Artifact Signing. **David is a player**, and a hand-built drop into his
   install directory is exactly the unsigned artifact that rule exists to prevent.
   No `-SkipSign`, no warn-and-continue.
4. **Planner may not do this alone.** The Planner role bars tag and signing
   without a Helm ruling. Naming that here rather than discovering it at the
   moment of release.

**The ambiguity is real and is not resolved by guessing.** *"Push to the live
environment on my desktop"* reads either as a signed tagged release that his
installed copy updates to, or as a local install of a build. Those are different
acts. The safe reading is (3) above and it is what this plan assumes; the
question goes to the Founder **in the step-2 email**, where it is one line beside
the ship word he is being asked for anyway, rather than as a separate page now.
Asking today would be asking him to decide the shape of a release whose contents
do not exist yet.

### 7.4 LIVE ASK — Helm

D7 is **outside the D1–D6 sequence Helm signed**, and by the plan's own
stop-and-escalate seam it does not inherit that SIGN. Two questions, neither of
which an Executor or a Planner may answer:

1. **Is D7 authorized as a slice at all**, on the shape in §7.1–7.3 — review per
   slice, email through Helm/Dranak, and a release that waits for a
   contemporaneous ship word?
2. **Does the card's *"then push the changes to the live environment on my
   desktop"* stand in for the release go, or not?** §7.3 assumes **not** and
   fails closed to asking him. If Helm rules that the card text IS the
   authorization, say so explicitly and the plan changes; absence of an answer is
   not that ruling, and D7 step 3 does not start without one.

Until this is ruled, **D7 is unfiled — there is no card for it** and no slice may
treat the release as reachable. D1–D6 proceed unchanged; none of them depends on
this answer.
