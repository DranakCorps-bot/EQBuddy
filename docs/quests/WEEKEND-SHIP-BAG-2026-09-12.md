# The Quests rewrite — Opus delivery plan, with the Sat/Sun 2026-09-12/13 checkpoint

**Fable 5 (Planner), Wed 2026-09-09 ~12:30 PM CT. Founder in session = the SIGN for this
plan. Helm still SIGNs every Opus PR. Supersedes the DRA-27 draft of ~10:34 AM CT
(`.claude/worktrees/planner-weekend-shipbag-20260909/WEEKEND-SHIP-BAG-DRA-27.md`), which was
written before P1b landed and could not read the locks doc. Paperclip document
`weekend-ship-bag` on DRA-27 carries this same text.**

**Founder direction, in session, 2026-09-09 (three statements, all recorded):**

1. *"I want to implement all class quests for PoS with the new guided model."* Phase 2 authors
   all sixteen classes; Helm's lock-1 proof set (WAR/MNK/DRU) stays the order and what the
   Founder verifies first.
2. *"Helm was looking for an MVP of just War/Monk/Dru."* Acknowledged: that is the proof set,
   not the scope.
3. *"I would like your plan for Claude Code Opus to be the development and delivery of the
   entire Quests rewrite. We can iterate changes if needed post delivery."* So this is a
   **delivery plan for the whole rewrite**, not a bag of cuts. The weekend is **Delivery 1's
   checkpoint**, not its boundary. Bevel and Founder critique land as **post-delivery
   iteration** — including the active-step card, which Opus builds now from the Founder's own
   §12 mock rather than waiting on faces.

Binding sources, newest first: this document; `FABLE.md` 2026-09-08 entry (the Helm-signed
#445 plan: §2 schema, §4 progress, §5 UI, §6 share-back, §8 migrate table, §9 ladder);
`docs/quests/GUIDED-PROGRESSION-LOCKS-2026-09-08.md` with the 09-09 Founder amendment;
`docs/quests/GUIDED-PROGRESSION-REQUIREMENTS.md` (the Founder's requirements, §§4–6, 12–19,
32–35); `HELM.md:1-12`; `DECISIONS.md` 2026-09-09.

**Out of band for every slice:** Play Console OFF. No tag, no store push, no `release.ps1`, no
signing change, no Evolved settings restore, no TEL/Version invention. The owner's build is
the local AppData publish by Soft's existing recipe. Claude Code CLI only — not the API, not
hermes_local/Qwen.

## 0. Standing state, measured this seat (main `4ec7c26c`)

| Fact | Evidence |
|---|---|
| P1a on main: `Core/GuideCatalog.cs` + one Warrior seed (`pos-warrior-runed-wind-amulet`, 3 objectives, 1 Stub), `Validate()` = lock 4a, `refresh.py` CURATED row | #454 |
| P1b on main: `QuestLedgerStore.GuideProgress` per character + `UI.Shared/GuideProgressRouter` (the one door; reward key → Sky store, else ledger; skip always ledger; store refuses a reward tick) | #472 merged `511d221c`, SSC #474; #473 closed duplicate |
| Nothing renders a guide. `QuestsView.RenderChecklist` (Sky tab) builds groups from `QuestChecklistLayout.Sky`; the phone's `CompanionProjection.BuildSky` calls the same | `src/EQBuddy/QuestsView.xaml.cs:2223`, `Companion/CompanionProjection.Checklists.cs:77` |
| `QuestChecklistRow` has no stub or guide field; row ids are `sky-NNN` item ids and the tick setters are keyed by row id | `Core/QuestChecklistLayout.cs:6` |
| Bevel has NOT faced the active-step card; no guide item in `BEVEL.md`/`BEVEL-FEEDBACK.md` | grep, this seat |
| Owner Desktop binary `2.0.0+fcd357f2` (pre-P1b) at `%LOCALAPPDATA%\EQBuddy Evolved\publish\EQBuddy.exe` | soft-captures README 2026-09-08 |
| Live Holds: empty. Only `needs-david`: Evolved profile restore (already paged) | `HELM.md:5,244` |
| Soft seats: all abandoned; one open channel PR #475 (P1b loop-close) | claims.json, `gh pr list` |
| `SkyQuestDefaults`: 16 classes, **95 rewards, 222 turn-in item rows**. WAR/MNK/DRU = 18 rewards / 43 rows. The seed covers 1 of 95 | grep, this seat |
| `EpicQuestChecklistCatalog` (Phase 4's input): ~193 KB embedded JSON of ordered, sectioned per-step rows | signed plan §0 |

## 1. The deliveries

Every slice: one Soft seat, one canonical claim key `DRA-NN` (Helm hygiene, `HELM.md:8`),
claim before kick, LIVE ASK in `HELM-FEEDBACK.md` when BUILT, Helm SIGNs, Soft merges on
`build-and-test` + `e2e-windows`. **Soft ≤3 is a count, not a mutex** (trap 70) — never two
seats on one card. Verify to the class. **The Founder kicks each card**; this plan starts
nothing.

### Delivery 1 — Phase 1 complete + Phase 2 all classes. Checkpoint: Founder smoke Fri 09-11, play Sat/Sun

| # | Slice | Card | Weekend checkpoint? |
|---|---|---|---|
| D1 | **P1c-a — guide rows on the Sky tab (desktop, both hosts)** | DRA-29 | yes |
| D2 | **P1c-b — phone parity + E2E + `shell-quests-sky-guide` shot** | DRA-30 | yes |
| D3 | **P1d-lite — "Improve this step" on every guide row** (lock 4's 1-click share-back) | DRA-31 | yes |
| D4 | **Phase 2 proof set: WAR → MNK → DRU**, one PR per class | DRA-32 / DRA-34 / DRA-35 | yes |
| D5 | **Desktop republish + Founder smoke** | DRA-33 | yes — Fri evening |
| D6 | **P1d-card — the active-step NEXT card + stub banner + skip verb**, from the Founder's §12 mock | DRA-36 | if it lands before Fri republish; else first thing next week |
| D7 | **Phase 2 batches 1–3** — the other thirteen classes | DRA-37 / DRA-38 / DRA-39 | whatever merges by Fri; the rest are next week's first seats |
| D8 | **Second republish + Founder smoke** when D6 + D7 are on main | DRA-33 (reopen) | next week |

**Definition of Delivery 1 done:** every class's Plane of Sky rewards render as guides on the
Sky tab (desktop, shell, phone); every stub says what the wiki does not and has a one-click
share-back; the NEXT card names the next actionable step; ticks live in one store each and
survive restart; `GuideCatalog.Validate()` and the must-list test hold on all 95 rewards.
Then Bevel critiques the card and the captions, the Founder plays, and iteration cards are
filed against what was seen — not against what was imagined.

### Delivery 2 — Phase 3: normal-quest conversion + store consolidation (own plan section, §7)

| # | Slice | Card |
|---|---|---|
| D9 | Store consolidation: per-profile Sky/Epic ticks → per-character `QuestLedgerStore`, with the `MigrateSkyRewardRenames` precedent | DRA-40 |
| D10 | Normal-quest conversion: `QuestCatalog` entries rendered through the guide engine where a guide exists; `SkyQuestDefaults` retirement into guide `Sources` + `refresh.py CURATED_SOURCES` in the same change | DRA-40 (second PR) |

### Delivery 3 — Phase 4: Epic 1.0 conversion (§7)

| # | Slice | Card |
|---|---|---|
| D11 | `EpicQuestChecklistCatalog` → `GuideType.EpicQuest` guides, one class per PR, the Epic tab rendering through the same projection | DRA-41 |

### Delivery 4 — Phase 5 contextual intelligence, Phase 6 gear layer (scoped, planned after Delivery 2)

| # | Slice | Card |
|---|---|---|
| D12 | Phase 5: auto-tick from logs via `ItemNames` (`SkyLootAutoCheck` family), while-you're-here, do-not-leave-yet, map integration | DRA-42 |
| D13 | Phase 6: gear recommendation layer over guide rewards (hooks already in the model) | DRA-43 |

Phase 5/6 cards carry the requirements-doc scope and a pointer here; their plan is written
after Delivery 2 lands, because both depend on what the consolidated store and the
converted catalogs look like. That is the one place this plan deliberately stops short of
Opus-ready detail, and it says so.

## 2. Delivery 1 slices, in the detail Opus needs

### D1 — P1c-a: `GuideChecklistProjection` + Sky-tab rows, desktop (DRA-29) — Wed night → Thu noon

**Lands in `UI.Shared/GuideChecklistProjection.cs`, not Core.** §5 of the signed plan wrote
`Core/`; the read side of "is this done" is `GuideProgressRouter.IsDone`, which lives in
`UI.Shared` because the write side needs `SkyCompleteToggle` (P1b's logged deviation, same
reason). Core cannot reference UI.Shared. The phone already references UI.Shared. Framework-
free is enforced by the existing UI.Shared test. *(FABLE-FEEDBACK P1b: "say which project a
mechanism lands in" — this is that line.)*

1. **Row shape** — `QuestChecklistRow` gains two optional trailing fields, both `""` by default:
   `StubNote` and `GuideRowKey`. Additive, positional-record style like `IslandHeading`. A guide
   row's `Id` is `guide:{guideId}/{objectiveId}` so it can never collide with a `sky-NNN` item
   id and `DistinctBy(Id)` counts still hold. Every existing test that constructs rows compiles
   unchanged.
2. **Matching** — a guide attaches to the Sky group whose `CompletionKey` equals the guide's
   TurnIn objective's `RewardKey` (`Class|Reward`). Not by `QuestName` and not by page title:
   `Guide.QuestName` is the `SkyTestSplit` runtime name (`HELM.md:140` KEEP) and stays the
   catalog link; the group key is what the checklist already speaks.
3. **Guide-backed group** — when a group has a guide, its rows become the guide's objectives in
   reading order (`Guide.AllObjectives`), `IslandHeading` = the stage `Name`, `Title` =
   `ShortInstruction`, `Detail` = who · where (what goes in the tooltip; the `Where` prose is
   the one the requirements doc's §5.3 "Better" example set). Groups with no guide are returned
   untouched — a class not yet authored renders exactly today (lock 5).
4. **Where each row's tick lives — three homes, one writer each.** The router grows a third
   home and a group-aware overload; `GuideProgressRoutingTests` grows the matching rows and
   the one-writer source scan keeps its shape:
   - `SkyTurnIn` — objective with `RewardKey` → `SkyCompleteToggle` (P1b, unchanged).
   - **`SkyItem` (new)** — a `Loot`/`Farm`/`Collect` objective whose `ItemNames` names exactly
     one of THIS group's `SkyQuestChecklistItem` rows (case-insensitive) is **item-backed**:
     `Acquired` reads from that item and the tick writes through the item's existing setter
     (`Acquired = done; AcquiredUnassigned = false`, the same two lines the checklist runs).
     The loot auto-tick (`SkyLootAutoCheck`) therefore lights the guide row for free, and the
     item row it replaces is not drawn twice (trap 4: one fact, one store, one row).
   - `GuideLedger` — everything else (Travel, TalkToNpc, a Loot step whose item is not a
     turn-in row), through `SetObjectiveDone` as P1b built it.
   A `SkyItem` match is by the group's own items only; `Wind Rune Azia` for Bard and for
   Warrior are two rows in two groups and never cross.
5. **Stub rows** — `Authoring == Stub` → `StubNote` carries the note; the WPF row draws it as
   a wrapped dim caption under the title, led by `GuidePresentation.StubLead` ("Wiki
   incomplete —"), tickable like any row (manual state beats weak inference; requirements §17).
   Never mapped onto `QuestPresentation.State`. New `UI.Shared/GuidePresentation.cs` holds the
   words only: `StubLead`, `GuidedCaption(done, skipped, total, stubs)` ("Guide · 1 of 3 ·
   1 stub"); D3 adds the share-back label and URL; D6 adds the card's strings.
6. **The TurnIn row** — reads/writes through the Sky store; when its prerequisites are not done
   the `Detail` says "after: Stone Amulet, Wind Rune Azia" (names from the prerequisite
   objectives' titles). The heading's existing "Turned in" button stays; two controls, one
   store, same as the phone and the checklist today.
7. **Heading caption** — under a guided group's heading, one caption line from
   `GuidePresentation.GuidedCaption`, counts from `GuideProgressRouter.Counts` extended to the
   three homes. `LastUpdated` is UTC; nothing renders it in D1 (DECISIONS 2026-09-09 #3).
8. **Skip** is not drawn in D1 (D6's verb). `IsSkipped` rows render as open.
9. **Dump facts** — `QuestsView.DebugFacts` gains `questsGuideGroups`, `questsGuideRows`,
   `questsGuideStubs`, `questsGuideDone`; the shell re-prefixes them mechanically (trap 58).
10. **WhatsNew** — ONE entry in the unreleased `2.0.0` block for the guide surface, naming P1b
    as its foundation (DECISIONS 2026-09-09 #4's reversal condition is met: P1b and P1c ship in
    the same release). Later Delivery-1 slices extend that one entry; they do not add four.
11. **Tests** — `GuideChecklistProjectionTests` (UI.Shared): a guided group's rows are the
    objectives in reading order; an unguided group is returned reference-equal; an item-backed
    row reads the item's `Acquired`; a stub row carries its note; ids never collide with
    `sky-` ids; prove-fail: a projection over a catalog whose turn-in key matches no group
    attaches nothing. `GuideProgressRoutingTests`: `SkyItem` home chosen only on a one-item
    match in the same group; a two-item objective goes to the ledger. `QuestChecklistLayoutTests`
    unchanged.

**Prediction to check on the shot:** Warrior · Runed Wind Amulet reads three rows under three
stage headings (Isle 4: Keeper of Souls / The wind rune / Turn in to Torgon Blademaster), the
middle one with the stub caption, the caption "Guide · 0 of 3 · 1 stub" under the heading.

### D2 — P1c-b: phone parity, E2E, shots (DRA-30) — Thu

1. **One entry point for both surfaces.** `CompanionProjection.BuildSky` calls
   `GuideChecklistProjection.Apply(groups, GuideCatalog.Default, settings, ledger, characterKey)`
   exactly where `QuestsView.RenderChecklist` does. Parity by shared module, not by feature list.
2. **Phone tick** — `CompanionActions` resolves a `guide:` row id through
   `GuideChecklistProjection.Resolve(rowId)` → (guide, objective, home) and calls
   `GuideProgressRouter.SetDone`; item-backed rows go through the item setter the phone already
   has. The phone never touches the ledger directly (the one-writer scan covers `src/`).
3. **Phone stub** — `StubNote` rides the row JSON; `index.html` draws it as the same dim
   caption. Footer version bump so the page re-fetches (trap 32).
4. **`SurfaceParityTests`** + `ThePhoneShowsTheGuideRowsTheDesktopShows`,
   `AGuideStepTickedOnEitherScreenIsTickedOnBoth`, `AStubNoteReadsTheSameOnBothScreens`,
   `AClassWithNoGuideIsUnchangedOnThePhoneToo`.
5. **E2E** (`tests/EQBuddy.E2E`, `EQBUDDY_SHELL=quests:sky` on a Warrior fixture): assert
   `shellQuestsGuideRows=3`, `shellQuestsGuideStubs=1`; then `AppendLogLines` a Stone Amulet
   loot line and wait for `shellQuestsGuideDone=1` — a positive event that only happens after
   the item-backed rule ran (trap 62). Build the app first (trap 64).
6. **Shot** — `shell-quests-sky-guide` in `scripts/shoot.ps1`: `EQBUDDY_SHELL=quests:sky`, the
   Warrior lens, the seed profile plus one looted Stone Amulet seeded through the real key
   (traps 22/23). Name checked free this seat. Run the batch, shoot `Solarized` once.
   Companion: `mobile-harness.ps1` capture of the Sky section with guide rows.

### D3 — P1d-lite: "Improve this step" (DRA-31) — Thu/Fri, after D1 is on main

Signed §6 verbatim, minus the card: `GuidePresentation.ImproveUrl(guide, objective, rowFacts)`
→ `discussions/new?category=q-a&title=Guide step: {guide.Name} / {objective.Title}` with guide
id, objective id, what EQBuddy shows (who/where/what or the stub note), a blank "What you saw in
game:", the wiki-first sentence and the source page's edit link (`WikiContribution.EditUrl`) when
a source title exists. Nothing auto-attached — no logs, no character, no inventory; the whole
body is in the browser before anything posts (consequence 8 untouched, same as `ReportUrl`).

Desktop: a `DesignSystem.InlineIconButton` at the row end (trap 16 hit size), tooltip
"Improve this step — opens a prefilled discussion; nothing is sent until you post". Phone: a
plain link with the same words (the phone browser honours it; trap 35 needs no substitute).
`GuidePresentationTests`: URL carries every field; a stub row's body carries its note; an
authored row's body carries who/where/what; nothing in the body comes from the log or the
character. No in-game command is involved, so `GameCommandsTests` does not apply.

Helm's #472 posture said LEAVE P1d until Bevel faces. The Founder's direction (statement 3
above) is that Bevel iterates post-delivery; the LIVE ASK to Helm carries that as a Founder
ruling, not a question. D3 needs no face regardless.

### D4 — Phase 2, one PR per class: WAR (DRA-32) → MNK (DRA-34) → DRU (DRA-35) — Thu → Fri

The same recipe for every class, WAR first because its seed exists and the rules get proved on
it. Warrior: five new guides (Azure Ruby Ring, Belt of the Four Winds, Dagas, Fangol, Pauldrons
of the Blue Sky) plus finish the Runed Wind Amulet seed if the wiki says more than it did on
08-31. Monk: six. Druid: six. Data only — no code, so a class PR may run beside D1/D2 on a
second seat. Rules, and they bind D7's batches identically:

- Source: eqlwiki `{Class} Plane of Sky Tests` (title as SERVED, trap 3) first; other online
  sources allowed where it is silent and **marked as such** in `Sources`; eqlwiki wins a conflict.
- Stages = islands; objectives = the turn-in item rows in `SkyQuestDefaults` (that is the
  must-list — **every item row of every reward of a class that has ANY guide has an
  objective**, and the `GuideCatalogTests` row that checks it is new in the WAR PR) plus the
  TurnIn objective with the `RewardKey`. A class is either fully on the guided model or not
  on it; half a class is the worst of both.
- "Trash mobs" wind runes stay **Stub** unless a source names an isle; the Efreeti drops carry
  the three named mobs the checklist already names (Hand of Veeshan / Overseer of Air / Noble
  Dojorn) as `Who`.
- `QuestName` = `{Class} Sky Test: {Reward}` (the split runtime name) — the existing
  `EveryGuideQuestNameResolvesInTheHarvestedQuestCatalogAsTheAppLoadsIt` test fails anything
  else. `RewardKey` = `{Class}|{Reward}` exactly as `SkyQuestDefaults` spells both.
- The provenance comments already in `SkyQuestDefaults.cs` (the bard #139/#150 match-the-wiki
  story, the Paladin #176 Isle-7 correction) are copied into the affected guides' `Sources`
  or `StubNote` — a guide must not be more confident than the checklist row it wraps.
- **Stub inventory in the PR body**, every hollow spot named. No ★, no `EffortNote` invention.
- The seat does not fetch from inside the app; authoring is a human/agent reading the page.
  Request rate at eqlwiki: one page read per class, by hand (consequence 7 unchanged).
- The class's shot: the batches do not each need a new shot; the WAR one proves the render.
  A class PR's evidence is `Validate()` green + the must-list test + the stub count in the PR
  body.

### D5 — Republish + Founder smoke (DRA-33) — Fri afternoon

Soft's existing signed-local recipe (`HELM-FEEDBACK.md:937-958`) into
`%LOCALAPPDATA%\EQBuddy Evolved\publish\`, from `main` after D1–D4 merge. No new mechanism.
If `az login` has expired, that is the one human step and it is the Founder's; the plan adds
no bypass. Then the §4 smoke list, by the Founder, on monitor 2. Reopened for D8.

### D6 — P1d-card: the active-step card + stub banner + skip verb (DRA-36) — Fri if D1–D3 are on main, else Monday

Built from the Founder's requirements §12 mock (L925–953) and signed §5, not from Bevel
faces; Bevel critiques the delivered card (ask filed in `BEVEL-FEEDBACK.md`), and the
iteration is a follow-up card.

1. **Selection rule** in `GuidePresentation.NextObjective(guide, isDone, isSkipped)`: the first
   objective in reading order that is not done, not skipped, and whose prerequisites are all
   done. None → "all done" or "all skipped" state. Pure, unit-tested, including the cycle-free
   guarantee `Validate()` already gives.
2. **The card**, drawn above the guided group's rows, under the tab chrome (trap 44), one per
   guided group in view: `NEXT:` + `ShortInstruction`; then Where / What / Who lines; `Why` =
   the reward name ("for: Runed Wind Amulet"); "⚠ before leaving" only when the stage's next
   objective is on another stage and this stage still has open objectives (the §5.5
   `DoBeforeLeaving` intent without a new field). Two buttons: **Done** (router `SetDone`) and
   **Skip** (router `SetSkipped`; strike-through the row; P1b stores it; a skipped reward
   objective stays skippable per P1b's call). The stub banner replaces the Where/What lines when
   NEXT is a stub: `StubLead` + the note + the D3 link.
3. **Phone**: the same strings projected as a `CompanionGuideCard` block above the group;
   `SurfaceParityTests` + `ThePhoneNamesTheSameNextStepAsTheDesktop`.
4. **Dump facts** `questsGuideNext=` (objective id length, not text — the E2E compares dumps),
   `questsGuideSkipped=`. E2E: seed → NEXT is `stone-amulet`; loot line → NEXT becomes
   `wind-rune-azia` and the stub banner fact flips to 1.
5. **Shot** `shell-quests-sky-guide-card` (name checked free), same staging as D2's plus the
   loot line so the card shows the stub state. Solarized once.
6. Words: every string in `GuidePresentation`; nothing spelled in XAML or JS.

### D7 — Phase 2 batches, the other thirteen classes (DRA-37 / DRA-38 / DRA-39)

Same rules as D4, one PR per class inside a batch, smallest reward count first (a new class
page's authoring surprises land before the next). Batch 1: Paladin (4), Rogue (6), Ranger (6),
Berserker (6), Shadow Knight (7). Batch 2: Beastlord (5), Cleric (6), Shaman (6), Bard (6).
Batch 3: Enchanter (6), Necromancer (6), Wizard (6), Magician (7). A batch that misses the
Friday republish is Monday's first seat, not a failure. After batch 3, decide the P1a open
question (`ObjectiveType` string → enum) with sixteen files as evidence.

## 3. Sequence

| When (CT) | Seat | Gate |
|---|---|---|
| Wed evening | DRA-29 (D1) claim → build → LIVE ASK | Helm SIGN; Soft merge on green |
| Thu morning | DRA-30 (D2) after D1 on main | Helm SIGN; Soft merge on green |
| Thu morning, parallel (2nd seat) | DRA-32 WAR (D4) — data only, no code overlap | Helm SIGN; Soft merge on green |
| Thu afternoon | DRA-31 (D3) after D1 on main | Helm SIGN; Soft merge on green |
| Thu afternoon → Fri morning (2nd seat, in turn) | DRA-34 MNK, then DRA-35 DRU (one file, sequential) | Helm SIGN; Soft merge on green |
| Fri morning | DRA-36 (D6) if D1–D3 are on main; else Monday | Helm SIGN; Soft merge on green |
| Fri morning (3rd seat, if free) | DRA-37 batch 1 | as above |
| Fri afternoon | DRA-33 republish; Founder smoke §4 | Founder go / no-go Fri evening |
| Sat morning fallback | if D1 slipped: owner plays `2.0.0+fcd357f2`; **no half-rendered Guide republished to beat the clock** | Founder |
| Mon → Wed next week | D6 if not landed; DRA-37/38/39 remainder; second republish (D8); Bevel critique lands as iteration cards | as above |
| After Delivery 1 | Delivery 2 (DRA-40), then Delivery 3 (DRA-41); Phase 5/6 plans written after Delivery 2 | Fable plan per §7, Helm SIGN, Founder iterates |

Three seats maximum at any moment (Soft ≤3): D1 → D2 → D3 → D6 chain on one, D4 → D7 on a
second, the third free for a Helm-asked fix or a batch.

## 4. Founder smoke — Desktop, monitor 2, Fri evening (15 minutes)

Run on the republished build against the live Evolved profile (isolation rules bind tests, not
his play). Tick the box or write what you saw instead.

1. Title bar of the shell reads `EQBuddy — Guide` when you take the widget's `Guide…` row; the
   rail row still reads `Quest`. Close with ✕, take `Guide…` again — it comes back.
2. Plane of Sky tab, Warrior lens: **Warrior · Runed Wind Amulet** shows three stage headings
   and three rows; the middle row's caption starts "Wiki incomplete —"; the caption under the
   heading reads `Guide · 0 of 3 · 1 stub` (or your real counts).
3. Tick "Loot the Stone Amulet" → close EQBuddy → relaunch → still ticked. Untick it → the
   item's box in the classic list (any class not yet authored) is untouched.
4. On the phone, same tab: the same three rows, the same caption. Tick a row on the phone;
   it shows ticked on the desktop within a second, and back.
5. Any guide row → the small "Improve this step" link → browser opens a prefilled discussion
   draft. **Do not post it** unless you mean to. Confirm no character name and nothing from
   your log is in the body.
6. Every Warrior, Monk and Druid Sky reward has a guide (eighteen), each with at least one
   authored step and its stubs honestly captioned. Pick one and read it as if you were on the
   isle.
7. If D6 landed: the NEXT card above the group names "Loot the Stone Amulet"; tick it and the
   card moves to the wind rune and shows the stub banner; Skip strikes a row through and the
   card moves on.
8. Regression eyes: expanded gear menu is still four rows; Options → Cards & windows prose
   still true (#466 hover ceiling); one real play session tail: DPS/XP move, Mobile pairing
   alive.
9. Anything wrong → note it on DRA-33 with the step number. Red on 1–3 = no-go (play
   `2.0.0+fcd357f2`); red on 4–8 = go with the row noted.

## 5. Collisions and calls

- **Helm's "LEAVE P1d until Bevel faces" (#472 ruling) meets the Founder's statement 3.** The
  Founder rules direction (consequence 5); this plan records that D3 and D6 build now and
  Bevel critiques after delivery. The LIVE ASK to Helm carries it as a Founder ruling to ACK,
  with D3 first (no face needed under either reading) and D6 after D1–D3.
- **Bevel ask filed** (`BEVEL-FEEDBACK.md`, To: Bevel): critique the delivered card + captions;
  P1b's open input (can "skipped" mean something narrower?) is theirs to answer post-delivery.
- **`HELM-FEEDBACK.md` is additions-only (trap 60).** Every entry this plan adds is a prepend
  with `git diff --numstat` deletions = 0 checked before push; identifiers read back after
  the splice. Channel PRs from Soft may land between pulls — re-read the ref at splice time.
- **Soft ≤3 and one claim key per scope** — cards are the keys (`DRA-29`…); claim as the card
  id, never as `#445`.
- **The Founder runs Opus. This plan kicks nothing.** Cards are created in `backlog` with
  the role named in the body and no assignee, because a `todo` card assigned to the Executor
  is an automation wake (DRA-28's run was `invocationSource: automation`). The Founder flips
  a card to `todo` + Executor at kick time.
- **Open PR #475** (P1b loop-close, channel only) — leave it to Soft; nothing here depends on it.
- **DRA-27** stays `in_review` with the Founder; this document is its `weekend-ship-bag`
  revision 2. DRA-8 is done as titled. DRA-28's stale executor monitor run is Soft's to clear.
- **`needs-david: none`** beyond what the Founder said in session. Checked against the
  consequence list: no new data leaves the machine (D3 is the existing browser-URL shape),
  sources are lock 2, request rate is unchanged, no release go is asked. Everything below the
  line is implementation and is logged in `DECISIONS.md`.

## 6. What the owner can play Sat/Sun, honestly

- **D1–D5 land:** Guide door → Sky tab → Warrior, Monk and Druid's eighteen rewards guided,
  stubs captioned, share-back link on every step; phone mirrors it; ticks survive restart.
  Classes whose batch has not merged render today's checklist until it does (lock 5's
  progressive cutover — a class is guided or exactly as before, never half). D6's card if it
  made Friday. Gear menu, rail, Live/Progress/World/Home as today.
- **D1–D3 land, a class PR slips:** the classes that merged are guided; the rest are classic.
  Still real; the smoke note says which.
- **D1 slips:** play `2.0.0+fcd357f2`; Guide door opens the Sky tab as it does today. Do not
  republish partial render.
- **Next week, already carded:** D6 if not landed, D7's remainder, D8 second republish, then
  Delivery 2.

## 7. Deliveries 2–4 — the plan sections the signed §9 deferred, written now because the Founder asked for the whole rewrite

### Delivery 2 — Phase 3 (DRA-40): store consolidation, then normal-quest conversion. Two PRs.

**PR 1 — consolidation.** The per-profile Sky/Epic ticks (`AppSettings.SkyQuestChecklist`,
`SkyQuestCompleted`, `EpicQuestChecklist`, `EpicQuestCompleted`) move into
`QuestLedgerStore.CharacterLedger` per character, keyed exactly as today (`Class|Reward`, item
ids). One migration on first load per character (`MigrateSkyRewardRenames` is the precedent:
move keys, never lose a tick; `ProfileJson` torn-write rules, trap 65), a `.bak` of the profile
section it drains, and `LedgerRoundTripTests` extended to the new fields (the loader's
hand-written copy, trap 26 one layer down). `SkyCompleteToggle` moves to Core (the reason the
router lived in UI.Shared goes away — the projection and router may follow it; log it).
`SkyTestSplit.WithTurnIns` and `QuestsView.ToggleCompleted`'s name-pattern routing are deleted
in the same PR because the ledger is now the one store. Phone, achievements import, loot
auto-tick all read/write the ledger through the same toggles. E2E: a tick made before the
migration is visible after it; the profile file is smaller by exactly the drained section.

**PR 2 — normal-quest conversion.** `QuestCatalog` entries (the harvested index) stay the
index; a normal quest gets a guide only when authored (`GuideType.NormalQuest`), rendered on
the **Quests** tab through the same projection where a guide exists and as today's catalog
row where none does (lock 5 by quest). `SkyQuestDefaults.cs` retires: its 222 rows are now
the guides' TurnIn/item objectives, its provenance comments live in `Sources`, and
`refresh.py CURATED_SOURCES` drops the file in the same change (`WeeklyRefreshWiringTests`
pins it). Requirements §35 Phase 3 warning stands: *do not simply wrap existing checklist text
in the new UI* — the conversion authors who/where/what per step or leaves a Stub.

### Delivery 3 — Phase 4 (DRA-41): Epic 1.0 conversion

`EpicQuestChecklistCatalog` (ordered, sectioned, prose-carrying per-step rows) converts to
`GuideType.EpicQuest` guides, one class per PR, sections → stages, rows → objectives with the
row's item as `ItemNames` and the prose as `What`; `Who`/`Where` authored from eqlwiki's epic
page or stubbed. The Epic tab renders through the same projection; the per-class
`EpicCompleteToggle` lock ("epic marked complete") becomes a guide-level done state. The epic
loot auto-tick keeps working because item-backed objectives read the same rows.

### Delivery 4 — Phases 5 and 6 (DRA-42 / DRA-43): scoped now, planned after Delivery 2

Phase 5 = auto-tick from logs through `ItemNames` (the `SkyLootAutoCheck` family generalised
over guide objectives), while-you're-here (requirements §18), do-not-leave-yet (§19), map
targets from `Where` (§20). Phase 6 = the gear recommendation layer over guide rewards, item
names linked into the item catalog. Both are written as plans once the consolidated store and
the converted catalogs exist; neither is built before then (Helm carry, unchanged).

— Fable 5, 2026-09-09
