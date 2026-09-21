# Decisions

**How to read this** — one dated `##` block per decision: what was chosen, what it could have gone to instead, and why. **This file is not in date order** — read the ISO date in each heading, never the position.

**Where the archive is** — older blocks are rotated **verbatim** into [`docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md`](docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md). Nothing is ever deleted; an archived block never revives a hold and never commissions work.

**What never rotates** — an open ask, an unexpired PARK or HOLD, and a standing rule stay in this file at any age, however old they are.

**Last cut** — 2026-09-21 by DRA-281 (DRA-144 F8 / DRA-246 re-seat): re-triaged the 6 blocks DRA-231 kept open (2026-09-11 through 2026-09-16) and found none still holds a live ask, unexpired PARK/HOLD or standing rule IN THIS FILE — DRA-57's LIVE ASK reads answered in its own text; DRA-106's LIVE ASK to Helm closed per the archived `HELM-FEEDBACK.md` LOOP CLOSE entry; DRA-71's and DRA-65's PARKs are tracked live in `FABLE.md`, not here; DRA-84's D3-scoped un-PARK note is superseded (D4/D5 already ran). Moved all 6 (31,937 B) to the archive; the `exo-experiment:` STANDING block re-pinned by DRA-231 is unaffected and stays.

## 2026-09-20 — STANDING: the `exo-experiment:` tag index (re-pinned by DRA-231)

**This block never rotates.** Plan §10.1 makes the tag below the thing the M0-exit doctrine
capture cites, and `Get-Experiments` in `scripts/exo-metrics.ps1` regenerates the "Experiments in
flight" table in `docs/ops/exo-dashboard.md` by reading these lines — each tag *and the judging
clause running from it to the next blank line* — out of **this file**. DRA-231's cut moved the five
2026-09-14 entries that first carried them into the archive, so the tags are re-pinned here
**verbatim** under DRA-144's never-rotate floor: all six experiments they name are still in flight.
The full entries, with their calls and evidence, are in
`docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md` — follow a tag there for the reasoning, and
keep it here for as long as the experiment is open. Retire a tag by graduating the experiment, never
by rotating this block.

`exo-experiment: merge-sync` — judged by *merge-to-close latency*: the wall time
from a PR merging to its linked issue reaching a terminal state. The observed
failure is days (EXO-HARDEN-A2, EQ-V2-HOME-CATCHUP sat `in_review` long after the
work was on `main`), and the target is minutes, because the run starts on the
merge event. Stated net of the **wrong-close count** — issues this job moved to
`done` that a human then reopened. A sync that closes the wrong card is not
faster than the drift, it is just more confident, and the same "state net of the
harm" shape the seat-mutex tag uses for false blocks.

`exo-experiment: seat-mutex` — judged by *rework rate* and *PRs per delivered slice*
(baseline 3.7% and 2.15), because a duplicate executor spends both: #566/#568 cost one
full run and produced a second PR for one slice. Stated net of the **false-block count**
kept in `.claude/soft-seats/README.md`'s evidence list — a mutex that refuses work that
should have started is not cheaper than the collision, it is just quieter.

`exo-experiment: metrics-baseline` — judged by *whether a later window's claim can be
checked against it without re-deriving the window*: concretely, whether the §10.3
"current vs baseline" column fills from `docs/ops/exo-baseline.json` alone at the M2
checkpoint, without any §6 KPI being recomputed by hand. Stated net of the count of
metrics still reading `unmeasured`.

`exo-experiment: channel-rotation` — judged by *rework rate* (baseline 3.7%), counting
a channel-file clobber, a silently truncated append or a mojibake re-encode as rework,
because that is the class trap 60 records three times in six days and the only §6 term
this change can move. **Stated net of the effect that is NOT a §6 metric at all:** the
bytes an agent must read before it can append correctly. That is the reason the rotation
was worth doing and there is no KPI for it, so a later graduation entry cites the rework
rows and says the primary benefit went unmeasured rather than mapping it onto a number
it did not move.

exo-experiment: ssc-retirement — judged by *PRs + Helm touches per slice*
(baseline 2.3 PRs/slice, ≥2 touches/slice), stated net of *veto rate* and
*rework rate*.

exo-experiment: whole-sequence-auth — judged by *Governance Wait Ratio* and
*Autonomous Correct Completion Rate* (baseline GWR 0.40–0.60, ACCR 0%),
stated net of *escaped defect rate*.

## 2026-09-19 — DRA-199: the live class-lens transition (DRA-181 D4 arm (c))

**AUTHORIZED by Helm** `d15c1369`; seat `sr-dra199-lensprobe`. Test-only `src/`:
`EQBUDDY_LENSPROBE`, a `questsClassLens` dump fact, additive `EqSegmentedStrip.Selected`.
No production behaviour change, no `WhatsNew` entry. Three calls, defaults noted:
`Selected` is backed by `EqChip.Selected`, not a key the strip remembers — a stranded lens
must read as nothing painted; the chip's click body moved to `LensTo` rather than being
copied into the probe (trap 4); `picks` forces NO refresh, because the phone's writer
cannot. Prove-failed 3×, incl. those mutants re-read off `_classLens` going GREEN — which
is why the fact is read off the strip. Full rationale: PR body. E2E 382/382.

## 2026-09-17 — DRA-164 D1–D3: the Plane of Sky Island view, and the republish this land does NOT do

**Seat:** `dra164-d1` → `d2` → `d3`, one signed sequence (Helm SIGNED PR #663 @ `148cdd6e`,
whole-sequence on green gates). Founder ask ~5:50 AM CT: KEEP the class-sorted Sky view, ADD a
Class/Island toggle, island view multi-selects classes and groups by island —
"everything to collect on island N before moving to next", warrior/monk/druid. D1 `dab8d03d`
(#664), D2 `a1748054` (#665), D3 `a386d886` (#666). Nothing here fetched, no curated catalog
was written, **no tag, no signing, no release** — see item 8, which is the one place this
entry departs from the card's own text.

**1. The veto-worthy default Helm named: the island view EXCLUDES hand-ins and turned-in
rewards, and counts both out loud.** Hand-ins go because the ★ Ready band already owns "what
can I turn in right now" and stays on screen in both modes; turned-in rewards go because
`MarkRewardTurnedIn` force-acquires every row under them, so keeping them renders each island
as mostly-done noise. **The default the other way** was to draw everything and let the player
sort it out, which is defensible right up until the first island is twelve rows of work already
done. Both sentences are EMPTY when nothing was hidden — a note that always fires is furniture,
not a disclosure — and Class view is named in them as where the excluded rows still live.

**2. Class view KEEPS the default (`SkyGroupByIsland` = false), and the E2E proves it by
ABSENCE.** `questsIslandGroups` reports `-1`, meaning "this render drew no island layout at
all" — a different claim from "it drew an empty one", and the only fact that can say the new
renderer did not run. Without it every assertion in the suite would still pass on a build that
had quietly replaced the view this card was told to keep.

**3. THE PARSER WAS ALREADY WRONG ABOUT 22 OBJECTIVES, AND THE FAILURE LOOKED EXACTLY LIKE AN
ANSWER.** The guide catalog writes the Efreeti drop as *"Plane of Sky - Isles 1.5, 4 and 8
respectively."* — one isle word governing a list — and the five shapes the shipped `Parse` knew
captured only the number touching the word. All 22 parsed to `[1.5]` and would have been filed
on the half-island alone. **The before-picture is committed**: the shipped regex is pasted
verbatim into `SkyIslandsTests` and SHOWN returning `[1.5]` beside the fix returning
`[1.5, 4, 8]`, because green-only would have been vacuous here — both parsers return the same
317 objectives and the same 97/95 split, and only the 103-vs-125 count tells them apart (trap
34). The new shape's tail is DIGITS ONLY: *"Isle four - griffons and pegasus"* is also a real
catalog string. **Scope-locked to that one shape**, per the SIGN; no second prose shape, no
invented island scaffold.

**4. The island fact rides the ROW, rather than being parsed back out of the heading.** One
fact stored in one place and read out of another is trap 4, and the other place here is a
string the two producers spell differently — `"Island 6"` from the classic layout,
`"Isle 6: Bazzt Zzzt"` from the guide projection. `QuestChecklistRow.IslandKey` is stamped by
the same code that stamps the heading, in both producers; `SkyIslands.SetKey` gained its
inverse, round-trip tested with the zero-padding asserted AS TEXT so a later tidy-up cannot
silently re-order the groups.

**5. The prose fallback is REFUSED on a turn-in, and the count is what keeps that guard
honest.** 48 of the 95 hand-in objectives mention Isle 1 — they are directions TO the Efreeti
Chamber, not gathering work on Island 1. A naive `Where` fallback would have filed all 48 under
Island 1 with full confidence. The 48 is asserted, so a catalog rewording cannot quietly make
the guard vacuous.

**6. The phone's note needed its own FIELD because it could not ride a group.** The page drops
a group with no rows (`if (!rows.length) continue;`), so a sentence about the list as a whole
has nowhere to sit among the groups, and hanging it on the first island would read as a fact
about that island. `CompanionChecklistSection.Note` is nullable and additive — the envelope
does not change shape, so `CurrentProtocol` does not move. **Both halves of the page claim are
asserted**: every word is Core's and the page spells none of them (trap 32), AND the page is
asserted to READ `data.note`, because a sentence the page is sent but never draws passes every
projection test there is. DRA-84's D2 and D4 each shipped a caption without that second half
and the phone drew four of five.

**7. Two things the pictures said that the counts could not, both RECORDED rather than fixed.**
(a) The desktop frame reaches Island 6 and stops at 900px, so it is evidence that the islands
ascend and NOT evidence about where the multi-island set and the unlocated rows sit — said out
loud rather than cropped quietly. (b) Every guided row says its island three times: the
heading, the step title, and the detail. That is the exact redundancy `WithoutIslePrefix`
removes for CLASSIC rows and never reached a guided row's detail. **It is not new here** — the
same three copies are on screen in class view today — so touching it would change the view this
card was told to KEEP. Filed to `BEVEL-FEEDBACK.md` with the frame as evidence.

**8. THE CARD'S OWN ROUTE SAYS "Desktop republish → ping Helm with 2.0.0+sha when LIVE", AND
THIS LAND DOES NOT DO THAT.** Helm's SIGN forbids it by name from this land — *"LEAVE inventing
Desktop republish … LEAVE inventing release / `release.ps1` / tag / signing / prod secrets"* —
and a release is the one hard Founder gate that stays (CLAUDE.md, consequence list item 2). So
D1–D3 are source only. **The default the other way** was to read the card as authorizing the
republish because it is written into the route; I did not, because a SIGN that names the
prohibition is later and more specific than the card that was written before it, and because
the release gate is not Helm's to waive either. **Where it landed:** the three slices merge, and
the republish is named as owed to Helm/the Founder rather than performed. If the ~2:00 PM CT
Desktop smoke was meant to run against a republished build rather than a local one, that is the
gap, and it is theirs to close.

**9. Fable's feedback for this sequence is in PR #666's body, not in its channel.**
`FABLE-FEEDBACK.md` has spent its grandfather band — recorded baseline 240,896 B, ratchet
ceiling 264,985 B, file at 264,970 B, **fifteen bytes of headroom** — and `channel-size-guard`
refuses the append. Verified by probing it rather than assumed: a test append reddened the
guard with that exact arithmetic, and was reverted. The guard's own refusal says raising the
row is not the fix and that rotation belongs to the standing `EXO-CHANNEL-ROTATE` card, not to
the Executor seat that tripped it — so the note goes in the PR body verbatim and is reported on
DRA-144. Not skipped: a skipped round is indistinguishable from not bothering.

**Gates:** `check.ps1` green at each slice (5,341 unit at D2, 5,347 at D3); D2's three new E2E
rows pass locally against a rebuilt app (trap 64); both channel guards green with the
`BEVEL-FEEDBACK.md` append verified additions-only (46/0) and its identifiers read back after
the write (trap 60c). `WhatsNew.json` entry landed in D2. No `GuideCatalog.json` or
`SkyQuestDefaults.cs` write, no eqlwiki fetch, and nothing here marks DRA-149 or DRA-84 PASS.

— Dranak (Claude Code), Executor — DRA-164 D1–D3

## 2026-09-17 — DRA-149 D5: the re-smoke pack, and the prediction that caught itself

**Seat:** `opus-dra149-d5` (disjoint). D4 merged at `3df21141`; this is the last slice of the
signed sequence. Everything below is inside plan P6 except items 3 and 6, which say so. Nothing
here touches the consequence list, nothing fetches, no catalog was rebuilt, **no release and no
tag**, and nothing here marks DRA-84 PASS — that is still the Founder's call.

**1. `GearCandidates` is a new field on a public Core record, and the reason is that P6's E2E
list named an input nothing carried.** The plan asked the re-smoke to dump "anchors, unread
count, candidates, band refusals, who withholdings". Four of those five were already in the
`EQBUDDY_EXPAND` dump; **candidates** was not, and it is the one that matters most. Without it
the two empty gear screens are indistinguishable: a sweep that found NOTHING (the pre-D1 state —
the tier rule compared a worn `+2` against catalog names where 0 of 11,196 carry one) and a
sweep that found 147 and had every place refused. Both draw one grey sentence, and
`helperGearWhy` counts DRAWN rows, so it is 0 in both. **The default the other way:** infer it
from `helperGearWithheld`, which is the sweep's per-anchor CAP rather than its output — a proxy
for a fact nobody had named (trap 64b), and wrong whenever the cap is 0.

**2. THE FIRST RUN OF THE PREDICTION TESTS REPORTED ZERO BAND REFUSALS AT EVERY LEVEL, AND IT
WAS THE TESTS THAT WERE WRONG.** Those numbers were minutes from being written into a checklist
the Founder would have read. The fixture did not set `HelperInputs.Bands`, and the gate stands
down entirely without them (trap 73, correctly) — it reports nothing and everything passes. That
is trap 78 from the test side: a guard aimed at a rule that never ran. What makes the E2E row
trustworthy is that it asserts `helperBandGate=1` BEFORE it reads any refusal count, which is a
liveness check DRA-84 D2 already wrote for exactly this reason and which I had not copied into
the unit half. **Recorded because it is the single most likely way this pack could have shipped
confidently wrong.**

**3. NOT in P6: `AppHarness.WriteInventoryDumpFrom`, and the number it settles.** The plan says
to stage the Founder's screens; it does not say how. `WriteInventoryDump` builds a dump from
tuples, which is right when the point is the SHAPE. His FAIL is about HIS dump — 21 worn rows,
`+2`..`+9` on every one, an `Any Slot` shield, a bow the game spells `Deterioriated` — and every
one of those is a thing a slice of this card fixed, so a hand-typed stand-in photographs a real
state that is not the one under test (trap 23). It copies the committed fixture verbatim.
**It also settles a pair that reads as a contradiction:** the room dumps 21 anchors and
`GearUpgradesFixtureSweepTests` says 20, because that suite excludes AMMO deliberately — a floor
met by the one anchor that already worked would be met by the thing that was never broken. Two
numbers, two questions, both right, and now both written down.

**4. The unit half and the E2E half disagree on the counts ON PURPOSE, and the disagreement is
load-bearing.** 155 candidates / 16 refusals from the engine; 147 / 18 through the launched app.
The app knows the character's CLASS from the log and the fixture does not, so the class lock
removes different candidates and a different set of zones reaches the gate. Both are right about
their own inputs. The tests are therefore FLOORS on both sides rather than equalities — a gate
that reddens because a wiki refresh moved one row is a gate the next person re-runs until green
(trap 74's lesson), and this one would additionally redden on a fixture difference that is not a
defect.

**5. The `WhatsNew.json` entries are drafted in `docs/ops/` and NOT in `WhatsNew.json`.** P6 says
to draft them and that the release shipping them is later. The tempting place is the existing
`2.0.0` entry, and it is the wrong one: **2.0.0 is the build the Founder failed**
(`2.0.0+275cc215`), so filing these there would tell every player that the fix was in the build
that lacked it. They land when `<Version>` moves, which is a release decision and David's.
**The default:** append to 2.0.0 now so nothing can be forgotten. The forgetting risk is real and
is handled by `release.ps1` refusing a tag without an entry.

**6. NOT named in P6: the checklist leads with the two screens that look like failures.** The
plan asked for a checklist saying what each screen should SAY, and for the band refusal to be
predicted "instead of letting it read as a failure". Writing it surfaced a SECOND screen of that
shape, in the opposite direction: the vendor block needs **no play history at all**, so it is the
one block in the room where *"it is empty because I have not played enough"* is not available as
an explanation — an empty one is a real defect. Every other block in this room gets better the
more you play, so the reading is inverted exactly where a reader would not expect it. Both are
⚠️ boxes in `docs/ops/dra149-founder-resmoke.md`.

**7. The plan's worked example was right, and it is now pinned rather than quoted.** P6 predicted
*"4 base-better items — Temple of Veeshan `60+` and Sleeper's Tomb `55+` refused against the
staged level, 1 quest row behind the toggle"*. Measured: two Velium Reinforced bows in Sleeper's
Tomb, the Bow of the Silver Fang in Temple of Veeshan, and `Rune Shafted Harpoon` from *The
Mighty Snowfang Hero* — which appears ONLY with include-quests on, because a quest is not a camp
and neither the band gate nor the who rule touches it. That is a plan prediction surviving
contact with the shipped catalog, and it is worth saying so as loudly as the one that did not
(DRA-84 D2's Crushbone example was a level off its own constant).

**8. THE STAGED SHOT CORRECTED THE PLAN'S WORKED EXAMPLE, AND NO ASSERTION COULD HAVE.** P6
predicted four base-better items for the bow. There are four — **for a character who is both a
Ranger and a Shaman**, which is nobody. `Bow of the Silver Fang` (Temple of Veeshan) is `RNG`
only; `Rune Shafted Harpoon` (The Mighty Snowfang Hero) is `SHM` only; the two Sleeper's Tomb
bows are `WAR|PAL|RNG|SHD|ROG`. The fixture character sees ONE refused zone and no quest row, and
`shell-helper-founder-bow` photographs exactly that. `FounderResmokeTests` runs with no class
lock, so its four is a fact about the CATALOG and was one sentence away from being read as a fact
about the Founder — it now says so, and so does the checklist. **The class lock is working
correctly**; what was wrong was a claim's scope, which is the kind of defect a green test cannot
see.

**9. `shell-helper-founder` ships with a stated caveat rather than a restyle.** The shell window
clamps near 885px tall on a 1080 screen whatever `EQBUDDY_SHELL_SIZE` asks for — a take at
`946x1000` came back 932x993 with the bottom ~110px black and the content ending mid-sentence at
the same place as the 880 take. So the three withheld captions are below the fold in the very
picture chosen to photograph one of them. **The default the other way:** shorten the room's
content by narrowing the staged pick until they fit, which would have made the picture agree with
the recipe by changing what it is a picture OF. Instead the caveat is in the recipe (trap 79's
rule: say which part of a capture is unfaithful, do not restyle the product until the camera
agrees), `shell-helper-founder-bow` is the narrowed staging that shows a refusal caption whole,
and the numbers are asserted where a fold cannot hide them.

## 2026-09-17 — DRA-161 / EXO-HARDEN-A1e: the rotated archive copies are OUT of scope for mojibake repair, permanently

**Seat:** Executor, carrying out the Planner ruling on DRA-160 (2026-09-17 09:50Z). This entry and one
paragraph added to the archive's own `README.md` are the whole change — **the three archived ledgers were
not touched; their blobs are sha-identical either side of this commit (`022fdea4`, `7eada6af`,
`fef3a1f9`), and the only file changed under `docs/ops/claude-archive/` is that README** — no transform
was run against any archived path, and the PR's `git diff --stat` shows only those two files.

**Verdict: OUT. All three files under `docs/ops/claude-archive/channels/2026-Q3/`, not now and not on the
next sweep.** The counts that raised DRA-160 are real — 56,979 cp437 depth-1 plus 14 depth-2 in
`HELM-FEEDBACK.original-flattened.md` (4,926,243 bytes, blob `fef3a1f9`), 5,429 cp1252 in the archived
`HELM-FEEDBACK.md` (1,915,606 bytes, blob `7eada6af`), 1,527 cp1252 in the archived `FABLE-FEEDBACK.md`
(1,000,117 bytes, blob `022fdea4`) — each measured off `/git/blobs/{sha}` with the decoded byte length
asserted against the tree's `size`, which is the only read that does not hit the contents-API false-clean
trap. They are real and they are not a defect to repair. **A future mojibake sweep reads this entry
instead of re-raising the card.**

### The Helm LOCK is not spent by this, and it stays live

The governing sentence is the DRA-55 Helm SIGN (2026-09-16 19:53Z), quoted here verbatim rather than
paraphrased, and re-read off the source comment during carry-out rather than trusted from the relay:

> Helm SIGN DRA-55 plan (ops PR #10). Executor owns slice 1 producer fix then slice 2 ledger repair.
> **Soft LEAVE inventing archive repair without separate Helm ruling.** BEVEL.md marker line stays.

That LOCK forbids *repairing* the archive without a Helm ruling. It does not require a Helm ruling to
*decline* to repair: leaving the archive untouched is the LOCK's own default state, so this verdict
affirms the LOCK rather than lifting it. No LIVE ASK was opened and Helm's door stays unspent.

**The LOCK remains live.** Anyone who wants these files repaired later still needs a real, separate Helm
ruling. This entry neither grants one nor pre-empts one — it records why nobody should bother asking.

### Three files, three different reasons — not one bucket

**1. `HELM-FEEDBACK.original-flattened.md` — it is corrupt on purpose, and two artifacts already said so.**
It holds 56,979 of the ~57k cp437 occurrences on the card, and it exists so DRA-75's "nothing was lost"
claim can be checked **against the bytes** instead of taken on trust, and so `channel-wipe-guard`'s
ARCHIVE exemption can see the entries that moved. It is also the cp437 detector's only clean-checkout
fixture — real corrupt bytes with no git archaeology needed, as DRA-55's intake comment named it. The
archive README has said *"Do not try to read it; do not edit it"* since the day it landed. Those 56,979
markers are the exhibit, not the damage.

**2. `HELM-FEEDBACK.md` — it is a git blob carried byte-verbatim, and every marker sits inside the
verbatim region.** Byte arithmetic, re-derived during carry-out rather than copied from the ruling:
`f4af3b5f` resolves as commit `f4af3b5f5ac6dc1b06290a478278bd9d19681d90`; `HELM-FEEDBACK.md` at that
commit is blob `8e18b203fcbe42a20d255f477f386b5d283afcf2`, **1,909,798 bytes**. That blob is byte-contained
in the 1,915,606-byte archive copy **at offset 5,808, with zero bytes after it** — the archive file is
exactly `[5,808 bytes of header + recovered PR #564 entry]` + `[the f4af3b5f blob, whole]`. All 5,429
cp1252 occurrences are inside the verbatim tail; none are in the prepended part. So a repair pass edits
the verbatim region, and that breaks two live assertions: the README's own claim (L84-85) that the
recovered history is carried verbatim, and `channel-rotate.py verify`, which per README L140-143 asserts
that the archive contains the `f4af3b5f` blob verbatim. It also buys nothing — git still holds blob
`8e18b203` with all 5,429 markers intact. Repairing the copy does not repair the history; it only makes
the copy stop matching the history it exists to document.

**3. `FABLE-FEEDBACK.md` — rotation moved these bytes, it did not create them.** The pre-rotation live
`FABLE-FEEDBACK.md` at `c3a40c1e` (parent of rotation commit `8f9e3205`) is blob
`51b18b02101c5210b94892a652549872f7f9a0b2`, 1,170,568 bytes, carrying **1,527** cp1252 occurrences — the
identical count now in the 1,000,117-byte archive copy. Those bytes were already permanent in git before
the archive existed.

### The principle, stated once so the next sweep quotes it instead of re-deriving it

**A rotated archive copy is not an independent site of corruption.** Rotation is a one-way move of bytes
that are already immutable in git history. Repairing a rotated copy removes zero corruption from the
record — it only makes the archive diverge from the revisions it was cut from, and it spends the fixtures
and byte-identity assertions that were deliberately built on that sameness. Count archive markers as
*copies of* live-surface markers when scoping a sweep, never as their own findings.

Repair pays on the **live** surface, and that work is done and guarded: DRA-119 slice A (PR #657, merged
2026-09-17 07:25Z) made `channel-wipe-guard.ps1` check 4 fail CI on cp437 at both depths, and slice B
(PR #659, merged 2026-09-17 09:40Z) repaired 77 lines while preserving 4 quoted ones. Rotation is
one-way, so nothing in the archive can reach a live file. Nothing is at risk from this verdict.

### The default this could have gone the other way on

Repair all three, on the grounds that ~64k mojibake occurrences in the tree is obviously bad and the fix
is a script that already exists (`scripts/demojibake.py`). That reading counts markers instead of reading
what each file is for, and it would have destroyed a SIGNed checkability exhibit, a detector fixture, and
two byte-identity assertions in order to change nothing about how much corruption git holds. The tell is
that the archive numbers do not move the live numbers in either direction — which is what makes them the
wrong surface to measure.

## 2026-09-17 — DRA-149 D4: the vendor half ships, and a guard deleted three keywords a human wrote

**Seat:** `opus-dra149-d4` (disjoint). D3 merged at `ec22acc6` (PR #653, both gates green), so
the sequence-wide SIGN carries this slice. Everything below is inside plan P5 except items 5
and 8, which say so; nothing here touches the consequence list, **nothing fetches** (the 118
zone wikitexts are the COMMITTED cache), no catalog was rebuilt, no release, and nothing here
marks DRA-84 PASS.

**1. The opening survey said SHIP, not park, and the number is on the record.** P5's park
condition was *"if Jewelcrafting matches fewer than 3 zones the face parks with that number and
the wiki door"*. Measured against the committed cache: 50 of 118 zone pages name a merchant in
their map key, 359 lines, 353 of them distinct; all eight professions match at least one zone,
and Jewelcrafting matches **17**. The face shipped.
`EveryProfessionMatchesAZoneAndJewelcraftingClearsTheParkFloor` keeps that condition alive in
the suite, so the day a wiki edit takes it under three the park is back on the table.

**2. The ADMIT rule is a LIST ITEM in THREE spellings, not one, and reading the refusals is what
found the other two.** The first pass admitted only `*` and refused 86 lines. Most of them were
map keys written differently — Misty Thicket and Everfrost Peaks number theirs with `#`, and
Halas, Oggok, Runnyeye and Timorous Deep write theirs as raw HTML `<li>` (three of those four
with the previous item's `</li>` on the front of the line, so an anchored `^<li>` rule missed
Oggok's entire fifteen-entry key). Widening to all three took the catalog from 41 zones to 50.
**The default it could have gone the other way on:** ship the `*`-only rule with a clean report
and 86 refusals. Nothing would have failed and five zones would simply have had no shops
forever. A COUNT of refusals would not have found it — the report listing them by zone did,
which is the argument for the report being part of the transform rather than a nicety.

**3. Prose stays refused even where it names a real merchant.** Kaladim's page says *"Note that
there is a merchant who sells Ore located at approximately 750, 200 on this map."* — true,
useful, and not admitted. **The default:** take it, on the grounds that it is obviously good
data. Refused because *"a sentence mentioning a merchant"* is a rule with no edge, and the same
relaxation admits Freeport's city history and Crushbone's hunting advice, both of which mention
merchants and neither of which points at one. All 42 refusals are listed by zone in
`merchants-report.md`, so what the strictness costs is visible rather than assumed.

**4. The vendor's NAME is not a field.** It is the obvious next step — `[[Bndainy Everhot]]`
looks exactly like an NPC — and the same Kaladim list also holds `[[Cleric]] Guild`,
`[[Rogue]] Guild Members` and `[[Kadek Norkhitter]]`. A rule that lifted link targets into a
"vendor" column would print "Cleric" as a merchant's name, and a rule that tried to tell them
apart would be guessing about the wiki's own linking. The name survives IN the transcribed
sentence, which is where the page put it and where a player can check it. **The default:** a
`Vendors` array per line, which every surface would then have had to caveat.

**5. NOT spelled out in the plan: `MerchantsShown` shows ONE line per ZONE.** P5 named a cap
and did not say per what. Per LINE, Freeport — the busiest map key in the cache — fills a
profession's whole list on its own, and "where should I go" answers with one place three times.
The cap is a travel budget, so it counts destinations. It lives in `HelperPresentation` rather
than in either surface because both draw the same three (trap 4), and
`TheShownListIsCappedAndNeverRepeatsAZone` prove-fails by removing the `continue`.

**6. THE GUARD DELETED THREE CURATED KEYWORDS, AND THIS IS THE ITEM MOST WORTH A VETO.** The
first draft of `ZoneMerchants.Keywords` carried `spices` (Baking), `metal bit` (Blacksmithing)
and `pelt` (Tailoring). All three are real trade supplies. Not one of them appears on any
shipped merchant line, so all three matched nothing, contributed nothing, and made the table
look longer and better-researched than it was — the failure with no symptom, because a dead
keyword produces a perfectly well-formed empty result (trap 78). They were removed because
`EveryCuratedKeywordMatchesSomethingInTheShippedCatalog` named them.
**The default the other way:** keep them as future-proofing against a wiki edit that adds the
word. I did not, because a curated table is only worth its curation if every row was checked
against the data, and a row nobody can point at a line for is a guess with a comment on it. The
consequence is stated plainly: **a keyword added here without a shipped line behind it fails the
build**, so a future wiki edit that introduces "Spices" needs the row put back deliberately.

**7. Stations and finished products are refused by name, which is a judgement.** No `oven`,
`kiln`, `forge`, `loom` or `barrel` — they sit on about a third of the lines, they are not
something a merchant sells, and matching on one files most of Norrath's shops under most
professions. `alcohol` is out for the same reason from the other side: it is Brewing's PRODUCT,
it is on forty lines, and a brewer buys "Brewing Supplies". Where a station rides a line matched
for another reason it is still on screen, because the line is shown whole.
`NoProfessionMatchesOnACraftingStationOrAFinishedProduct` pins all six.

**8. NOT named in D4's row: the phone got the block in this slice.** P5's row reads "transform +
reader + face + committed negatives". The parity rule in `CLAUDE.md` is a standing repo rule
rather than a plan extension — when a surface exists on both, the decision lives in `UI.Shared`
and all three call it — and DRA-84 D5's lesson is that a sentence reaching the wire without a
page-side must-list row passes every parity test there is. Both words already lived in
`HelperPresentation`, so the wire, the page and the must-list rows landed together.
**The default:** ship desktop-only and let D5's checklist caveat it. The cost of the choice is
three fields and one record on the Helper section; the cost of the other was a re-smoke that
finds the phone missing a block the PC has.

**9. A FOURTH wiki door, and it is wired.** `HelperDoorKind.WikiZone` resolves through
`WikiLinks.Page`, not `Search`: a zone title is not an item and must not go through the
item-alias rule. D2 found `WikiSkill` had shipped UNWIRED for a whole slice because `Door()`
fell through to `AddressFor`, which answers null for every wiki kind; the switch D2 left behind
is exhaustive by kind, so adding this one was a compile-time question rather than a silent
no-op.

**10. THE FABLE FEEDBACK FOR THIS SLICE IS NOT IN `FABLE-FEEDBACK.md`, AND THAT IS THE GUARD'S
CALL RATHER THAN MINE.** `channel-size-guard` refuses the append: the file has spent its
grandfather band (baseline 240,896 B, ceiling 264,985 B, currently 264,970 B — fifteen bytes of
headroom), and the refusal says in its own words that *"RAISING the row is not the fix"* and
*"rotation belongs to the standing EXO-CHANNEL-ROTATE card, not to the Executor seat that
tripped this."* So there is no version of this entry that fits and no edit here that is mine to
make. The note is written and lives **verbatim in the PR body**, and DRA-144 (*Channel hygiene
B: rotation pass 2*) carries a comment naming this file and this block. `BEVEL-FEEDBACK.md` is
well inside its band and its note landed normally. Recording it here because "feedback is not
optional" and a silently skipped round is indistinguishable from not bothering.

Gates: `check.ps1` all green (5,289 unit, +30), the two Helper E2E rows green. Four prove-fails
recorded: a plain substring rule reddens two word-boundary rows, dropping the per-zone rule
reddens the cap row, dropping the page's `m.lines` reddens the page-side must-list, and dropping
the transform's HTML arm reddens `--check` on both generated files.

## 2026-09-17 — DRA-149 D2: the alias table's seam, a fourth gap reason, and a dead wiki door found on the way

**Seat:** `opus-dra149-d2` (disjoint; D1 ran in parallel on `dra149/d1-sweep-base-vs-base`).
Plan PR #649 merged at `2645cb96`, Helm SIGNED the D1-D5 sequence ~9:15 PM CT, Live Holds
empty. Everything below is inside plan P2 except where it says otherwise; nothing here touches
the consequence list, nothing fetches, no catalog was rebuilt, no release.

**1. The alias lands INSIDE `EqlWikiItemService.NormalizeTitle`, not beside it.**
P2 named that method as "the candidate" and told the executor to verify every reader routes
through it. Verified: `ItemCatalog.Find`, `EqlWikiItemService.CachedInfo`, `LookupAsync`,
`ItemInfoWindow`'s heading and `UI.Shared/WikiLinks.Search` all call it, and nothing else folds
an item name. So one row fixes the catalog lookup, the wiki fetch and the player's own door
together. **The default it could have gone the other way on:** a dedicated
`ItemNameAliases.Resolve` call at the gear sweep only, which is a smaller diff and would have
left the item window and the wiki door still showing the spelling that failed — a second answer
to "what is this item called" (trap 4).

**2. `WornItem.BaseName` is now built by that same seam, which is a behaviour change P2 did not
spell out.** Its doc comment has always read *"the wiki's title for it, which is the catalog's
key"*, and it was built by `QuestCatalog.BaseItemName` — a second `+N` stripper that has never
heard of a spelling. The contract was therefore true only where the two agreed. This matters
beyond tidiness: `GearUpgrades.Sweep` skips a candidate whose name equals the anchor's
`BaseName` ("the catalog holds the item the player is wearing too"), and that refusal MISSED
for exactly the items an alias covers. **The default:** leave `BaseName` as the dump's fold and
alias only inside `statsFor`. I did not, because it leaves a live same-name hole for D1 to walk
into — D1 is moving that comparison to base-vs-base. Pinned by
`FounderWornSheetTests.EveryAnchorsBaseNameIsTheNameTheCatalogFiledItUnder` over his whole dump.

**3. A FOURTH gap reason, `GoalGapReason.NothingWornIsReadable`.** P2 asked for unread rows to
become a sentence and did not mention the gap. But with a dump present and every row
unreadable, `Worn.Count == 0` drew `NoInventoryDump` — *"Run the inventory command in game and
this fills in"* — which is a loop with no exit for the one player it fires for. It is the same
shape `EveryZoneOutsideYourBand` already has (a state `NoCatalogUpgrade` would misdescribe),
and the must-list tests cover a new member automatically. **The default:** ship the caption
and leave the gap saying the wrong thing, on the grounds that the state is not in the fixture.
Cost of the choice: one enum member on a public Core enum while D1 is in flight.

**4. NOT in the plan, and reported loudly: `HelperDoorKind.WikiSkill` has been a silent no-op
since DRA-71 D8 shipped it.** `HelperRoom.Door()` special-cased `WikiFaction`, then fell
through to `AddressFor`, which answers null for every wiki kind — and the null arm `return`s
the label UNWIRED, before `_doors++` and before `_deadDoors++`. So the "eqlwiki" control under
all eight professions drew a tooltip and opened nothing, and `helperDeadDoors` could not see
it. `HelperMustListTests.EveryDoorEitherLandsOnARoomOrOpensTheWiki` proves the WORDS exist and
nothing proved the CONTROL opens — trap 34 one layer down. I fixed it in the same method rather
than filing it, because D2 adds a THIRD wiki door and shipping a new one beside an identical
dead one is not an option; the switch is now exhaustive by kind so a fourth is a compile-time
question. **The default:** file a stub and ship the new door beside the broken one. Flagged in
`FABLE-FEEDBACK.md` — the guard gap is the durable lesson, not the one-line fix.

**5. `WikiLinks.Page` lifted out of `WikiLinks.Faction`.** Wiring the profession door needed a
non-item search, and `WikiLinks.Search` applies the ITEM rule — it strips a trailing "+N" and
now consults the item alias table. `Faction`'s own doc already warned against exactly that.
One spelling rule, two callers, rather than each door picking the nearest helper.

**Verification.** `scripts/check.ps1` all gates green (5,178 unit tests). Full local
`tests/EQBuddy.E2E`: 369/370 — the one red is `EveryLandedRoomIsReachableByItsOwnAddress`
(`world:drops`), an OPEN ledger row, passed 19/19 on an immediate re-run of the whole theory,
and filed as a third occurrence in `docs/ops/flake-ledger.md` including the fact that this seat
failed to capture the assert text. Four prove-fails run and recorded in `docs/TestPlan.md`.
**No `WhatsNew.json` entry** — the plan puts the drafted entries in D5 with the re-smoke pack,
and no release ships from here.
## 2026-09-17 — DRA-164 D4 (DRA-171): the island view's rows prefix their class

Executes P8 of the DRA-164 plan (PR #670, merged `49d0edc6`) under the Helm SIGN pinned
to head `a58be752`. PR #671.

**Two defaults it could have gone the other way on, and neither is a consequence-list
door:**

**1. A PR COMMENT was taken as the Helm SIGN.** The ruling arrived as two comments on
#670 (`5715055057` / `5715055864`), not as a PR review and not as a `HELM.md` commit —
and `CLAUDE.md`'s DRA-73 M0 cutover 1 names exactly those two forms. I took it as the
signature and started the slice.

*Why.* The substance is unmistakable: titled `Helm SIGN`, the head SHA pinned, a numbered
Carry-out addressed to Executor. It is immutable, timestamped and attached to the thing it
rules on — which is the whole of what cutover 1 says a review buys over an `ssc-N` PR.
The cutover's purpose was to stop Executor waiting on a ceremony; refusing a signature
this explicit on its container would have reproduced exactly the stall cutover 2 exists to
end, and "the SSC has not landed" is already named as not a reason to hold.

*The other way.* Treat the comment as no signature, write a LIVE ASK asking for a review,
and park the slice. That is defensible on the letter of the doc and it is what an
automated check would do — `gh pr view 670 --json reviews` returns `[]`.

*Where it landed.* Proceeded, and asked Helm to close the gap in whichever direction it
prefers — amend cutover 1 to admit a comment, or issue rulings as reviews from here on.
The standing risk is only that the doc and the practice disagree; David vetoes from here.

**2. The class was REMOVED from the owner label rather than said twice.** P8 asked for the
prefix and said the row carries it "plus the reward"; it did not say what becomes of the
class in the existing owner label (`Warrior · Belt of the Four Winds`). I moved the class
INTO the title and left the reward alone beside it, so each row names its class once.

*Why.* Saying one fact twice on a row is the redundancy the six questions exist to remove,
and the D3 Bevel note had already filed the island being said three times on every guided
Sky row. The prefix is only an improvement if it REPLACES the owner's copy.

*The other way.* Keep the owner label exactly as it was and add the prefix in front —
strictly additive, and unarguably inside the LEAVE list.

*Where it landed.* One copy. Guarded on both surfaces and in the running app
(`questsIslandRowOwner` is asserted not to name the class), and stated in the plan feedback
so Fable can rule differently next time at the cost of one line.

**No `WhatsNew.json` entry of its own:** the sentence went into the island-view highlight
that has not shipped yet, rather than a second highlight about a feature no player has
seen without the prefix. Nothing here touches privacy, the release go, a public surface,
or the values line.

— Dranak (Claude Code), Executor — DRA-164 D4 / DRA-171

## 2026-09-17 — DRA-181 D4: one producer for the quest class-lens chips

**Seat** `opus-dra181-d4`, disjoint-parallel under Helm's SIGN of #685. P5 shipped as written; the calls below are implementation, none on the consequence list. **Kept short on purpose: this file is 3 KB from its ratchet cap** (baseline 614,986 + 10%), so the fuller version is the PR body's.

**Assumption:** P5 authorises what has to move for the chips to track the picks, including the REDRAW — a strip that is right only after a forced refresh satisfies the sentence and not the Founder's screen.

1. **The offered list joined the repaint signature (`off:`).** The plan said the signature already carries `classes`; it carries the LENS-NARROWED list, so with a lens on, a deselection moves no term — and the phone writes these picks (`CompanionActions.SetClasses`) with no way to force a refresh here. Could have been left alone on "the picker forces a refresh anyway"; that is trap 72's own argument, four times over on this surface.
2. **`_myClasses` became `_offered`** rather than gaining a sibling: the field already held exactly that list, and two names for one fact is trap 4. No behaviour change.
3. **`questsClassStrip` abbreviates and uses "-" for the collapsed strip.** "Shadow Knight" carries a space and the dump is space-separated `key=value`; a raw name — or an empty value — would silently corrupt the next pair.
4. **The ternary scan is aimed at the DECISION, not every emptiness test** (five other files matched the broad shape). Prove-failed against the two lines this slice deleted, with one live exemption so it is not a detector aimed at nothing.
5. **`WhatsNew.json` entry DRAFTED, not shipped** (`docs/ops/dra181-whatsnew-draft.md`) — DRA-149 D5's idiom; the release go is the Founder's.

— Dranak (Claude Code), Executor — DRA-181 D4

## 2026-09-17 — DRA-180 D1: the zone-era table, and the five defaults that could have gone the other way

**Seat:** `opus-dra180-d1` (Paperclip DRA-180). Helm SIGNED the DRA-180+181 plan at PR #685
(`443dea45` on Soft `main`), whole-sequence on green gates; this is D1 only. Ships
`scripts/harvests/eqlwiki/zone-eras-transform.py` → `Core/Data/ZoneEras.json` +
`Core/ZoneEras.cs` + `ZoneErasTests`. **Fetches nothing** (the COMMITTED zone cache), plain
JSON, no curated file written, **no engine reads it**, nothing about Pages / Play / tag /
signing / release. `needs-david`: NONE — the WorldEra one-word ask is P4's and rides Helm's
mailbox, not this slice.

**1. The fold's earliest-era rule lives in `ZoneEras.Reconcile` (C#), not in the transform.**
The plan says *"the transform emits the EARLIEST era under the folded identity"*. *The default
the other way* was a `Folded` section in the JSON — closer to the plan's letter, and it would
have put the rule in two places the moment `ZoneEras` needed to answer a `DropZones` spelling
(trap 4). *Where it landed.* The transform emits one row per PAGE — what the page said — and
the C# owns the fold. The report names the collision, both pages' eras and the rule in words,
and computes nothing. Reversible in one slice if Fable wants the letter.

**2. A fold disagreement where either side is ABSENT answers NOTHING.** The plan decided the
Dated/Dated case (Chardok: Kunark beats Chardok Revamp) and not this one, which has no
instance in the corpus. *The default the other way* was to let the dated side win. *Where it
landed.* Null, because an absence is not an era and cannot be compared — letting a dated title
carry an absent one is exactly the *"absent means Classic"* default the plan refuses by name.
Guarded against a fixture, and the rule is commutative so dictionary order cannot decide it.

**3. `--selftest` was added and wired into `check.ps1` + CI beside `--check`.** The plan asked
only for `--check`. *The default the other way* was `--check` alone. *Where it landed.* **Both
refusal arms are unreachable in the committed corpus — 0 off-ladder words, 0 pages with two
eras — so `--check` green says nothing about whether they fire** (trap 78, and trap 34 from the
aimed-at-nothing side). 17 checks over synthetic wikitext, writing nothing; it also asserts the
mirrored `LADDER` is non-empty, since an empty admitted set refuses everything and reports a
clean corpus.

**4. The report carries a join survey the plan's D1 list did not name.** *The default the other
way* was the six sections asked for. *Where it landed.* Included, because D2's gate is worth
measuring before it is built: **81% of the catalog's drop weight lands on a zone with an era**,
and `Chardok` (155 mentions) resolves through the fold. Snapshot-stamped and deliberately
outside `--check`, the `zonelevels-report.md` precedent, so a catalog refresh cannot redden it.

**5. `QuestEraLadder.IndexOf` extracted; `Allowed` now calls it.** *The default the other way*
was a second `Array.FindIndex` over `Eras` inside `ZoneEras`. *Where it landed.* One producer
for "where on the ladder" (trap 4) — a second opinion on what counts as one of our era words is
what makes a gate fail open silently.

**Two corrections to the plan's own numbers, both measured:** its §0 histogram reads 56 Classic
/ 24 Kunark, and the six figures sum to 102 rather than the 104 it also states. Measured twice,
with two regex shapes: **57 / 25 / 19 / 1 / 1 / 1 = 104**. The total and the 14-name ABSENT list
are exactly right; only the split was off. Also `Plane of Hate` is enumerated as
`Plane of Hate cleanupproject`, and the guard uses the real title.

**No `WhatsNew.json` entry:** nothing here is player-noticeable — no surface reads this
catalog. `check.ps1` all green (5,398 unit tests); **9 mutants prove-failed** across both
languages (empty ladder, off-ladder accepted, two-eras picked, positional parse,
absent-means-Classic, latest-wins fold, absent-side fold, unrankable fold, containment lookup).
`FABLE.md`'s DRA-180 item is deliberately NOT drained — D2–D5 still need it. Nothing here
touches privacy, the release go, a public surface, or the values line.

— Dranak (Claude Code), Executor — DRA-180 D1

## 2026-09-19 — DRA-180 D2: the era gate in `Recommendations` — four defaults

Plan P1/P2/P3. Each could have gone the other way; none is on the consequence list (the era
VALUE is game data with the wiki as source, and plan P4 routes that through Helm, not here).

1. **The liveness fact reports the EFFECT, not the constant.** The plan asks E2E to assert
   `helperEraGate=1`. It cannot be 1 on any build that ships D2, because P2 ships
   `WorldEra.Current` EMPTY on purpose — so a dump reading that constant could only be asserted
   against a build nobody has. The fact is now `RecommendationSet.EraGateLive`, produced by
   `Recommendations.EraGateArmed(inputs)` — the SAME predicate the gate itself stands down on
   (trap 4), so a dump can never claim a gate ran that did not. D2 asserts it is **0** in a
   launched app, which is a real assertion (`DumpValue` throws on an absent fact) and is the
   honest reading of "the gate lands dark". **D5 flips it to 1**, and the E2E carries that
   prediction in a comment so the redness is the intended signal rather than a surprise. An
   env-var door onto the curated value was refused: that is a second writer of the one fact P2
   says is curated and hand-committed.

2. **The two new gap reasons got their WORDS in D2, not D3.** `HelperMustListTests` refuses a
   `GoalGapReason` no surface can say (trap 34's must-list), so the engine and its sentences
   cannot be split across a slice boundary — the suite reddened on exactly those two rows and
   nothing else. D3 still owns the per-anchor all-removed sentence and the `WhatsNew` drafts;
   what landed here is the minimum the guard demands, which is the guard working rather than
   scope creep.

3. **`GearEraRefusal` is its own record, not `GearBandRefusal` with nullable halves.** The two
   quote different evidence — two era words against two numbers and a level — and a surface
   testing which fields were populated would be deciding the rule a second time (trap 4). The
   same reasoning keeps `GearEraRefusals` and `MaterialEraRefusals` apart, and both apart from
   the band lists: one merged count explains neither list (DRA-149 D3's rule).

4. **Quest rows ARE era-gated, though they stay band-EXEMPT.** The band exemption is sound —
   the quest is the path, and what guards it is not the question. Era is a different claim: a
   quest in unopened content cannot be started at all, so offering it as a way to gear up is
   the Kael Drakkel row's lie in quest clothes. Read off the quest catalog's own `Era` through
   `QuestEraLadder.Allowed`, folded once per engine rather than scanned per bucket.

**Measured in a launched app** (world temporarily set to Classic, then reverted):
`helperEraGate` 0→1, `helperEraRefused` 0→**4**, `helperEraLine` 0→1, `helperZones`
`ClanRunnyeye,KaelDrakkel,TowerofFrozenShadow`→`ClanRunnyeye`, and **`helperBandRefused` 2→0**
— the era gate ran first and took the two 60+ planes before the band gate saw them, so the
ORDER decision is visible end-to-end rather than only in a unit test. That is D5's prediction
pack, already measured.

**Gates:** `check.ps1` all green (5,437 unit), full E2E 380/380 green against a rebuilt app
(trap 64). **Five mutants prove-failed**: the gate never runs (5 red), band-before-era (1 red,
the order guard alone), the world defaulted to Classic (1 red, the P2 guard alone), the page
sent the caption but never drew it (1 red, the must-list), and the lit-gate E2E above.

**No `WhatsNew.json` entry:** the gate is dark, so nothing is player-noticeable yet. The
release that ships a lit gate is D5's and stays the Founder's. `FABLE.md`'s DRA-180 item is
deliberately NOT drained — D3 and D5 still need it. Nothing here touches privacy, the release
go, a public surface, or the values line.

— Dranak (Claude Code), Executor seat `opus-dra180-sr` — DRA-180 D2

## 2026-09-19 — DRA-180 D3: per-anchor "everything was removed" (POINTER — file at ceiling)

Six logged decisions live in **PR #694's body**, not here: DECISIONS.md is 674,806 B against a
676,484 B grandfather cap, so the full entry (1,664 B) would leave 14 bytes and redden the next
append. Rotation is DRA-154's standing card and never rides a feature branch, so this is a
pointer, not a trim.

Headlines: D3's declared sentence needed a Core change (`GearAnchorRemoved` + stage snapshots)
because D2's refusals are keyed on the PLACE — a words-only function with no producer would be
the furniture the plan forbids. Reported only when NOTHING of an anchor survived; a candidate is
charged to the gate that took its LAST place; an anchor nothing dominates stays
`NoCatalogUpgrade`; cap 3 + a count; band half ships live, era half dark until D5.

## 2026-09-21 — DRA-262 D2: two calls the signed plan did not make

**1. A 2.0.0 What's-new highlight was made TRUE rather than left to ship self-contradicting.**
DRA-66's entry (still unreleased) ends *"If your achievements dump has already named your
classes, that answer wins and the room says so — run the dump again if it is out of date."*
D1 reversed the first half and D2 deletes the sentence the second half points at, so shipping
both entries would put two opposite promises about one control in one release. I struck that
one clause; the rest of the highlight is byte-identical, and the new entry carries the dump
story. **It could have gone the other way**: leave it, on the grounds that D2's declared duty
was one new entry and editing a neighbouring highlight is scope. Chosen against because
"every entry TRUE" is the standing rule and the falsehood is one my own change created.

**2. The plan's optional chip tick in the E2E was DECLINED, on a measurement.**
`SetStatedClasses` has one writer in the app — the chip's own `onClick` inside `HomeRoom` —
and the suite may not press a control. Reaching it needs a FIFTH `DebugHooks` rendezvous, and
each of the four that exist was authorized separately. The plan gated the ask on "cheaply";
this is not. **It could have gone the other way**: build the probe, on the grounds that the
plan floated it. Chosen against because a new debug rendezvous is machinery the slice did not
declare. Displacement stays proven in `CharacterClassesTests` (D1) and the E2E asserts
`shellHomeClassDoor == 1` beside `shellHomeClassChips == 0` — prove-failed by restoring the
early return: `door=0 chips=0`, red on the first half, which is what the chip count alone
could never have seen.
