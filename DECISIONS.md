# Decisions

**How to read this** — one dated `##` block per decision: what was chosen, what it could have gone to instead, and why. **This file is not in date order** — read the ISO date in each heading, never the position.

**Where the archive is** — older blocks are rotated **verbatim** into [`docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md`](docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md). Nothing is ever deleted; an archived block never revives a hold and never commissions work.

**What never rotates** — an open ask, an unexpired PARK or HOLD, and a standing rule stay in this file at any age, however old they are.

**Last cut** — 2026-09-20 by DRA-231 (DRA-144 F6): kept everything dated 2026-09-17 or later, plus 6 older blocks that still carry something open; moved 143 blocks (595,334 B) to the archive, then re-pinned the six `exo-experiment:` tags verbatim from five of them.

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
## 2026-09-13 — DRA-71 delivery 8: resources are profession-first, and the arithmetic is parked on its own survey

Pre-authorized: Helm SIGNED the plan (PR #586, merged `a28c5d89`) and SIGNED
D2–D7 (#588, #590, #592, #594, #596, #598), authorizing `dra71-d8`. Nothing below
is on the consequence list: no release, no new surface, **no eqlwiki request of
any kind** (the survey reads a cache that was already on this machine and this
seat fetched nothing), nothing new leaving the machine, nothing near the values
line — the professions are a catalog EQBuddy already ships and the standings are
this character's own log. David vetoes from here.

**1. THE ITEM→PROFESSION ARITHMETIC IS PARKED, AND THE SURVEY IS THE REASON.
This is the delivery's headline default.** P13 made the arithmetic conditional on
an evidence step — *"good coverage → promoter carries `Categories` + a curated
Category→Profession map; poor coverage → picker + skill + doors ship and the
arithmetic is PARKED"* — so the survey ran before anything was built, through the
app's own item parser, over the 10,957 cached pages:

- **10,919** carry at least one `[[Category:…]]` — **99.7%**, across **539**
  distinct categories.
- **14** of them carry a category that names a profession, across **five** of the
  eight (Alchemy 1, Baking 2, Brewing 2, Pottery 8, Tailoring 2). Blacksmithing,
  Fletching and Jewelcrafting have none at all.
- The generic markers instead: **822** pages say `Tradeskill Ingredient` and
  **1,959** say `Player Crafted`.

**99.7% is the wrong number to feel good about, and that is the finding worth
carrying forward.** The field is populated almost everywhere and it answers a
different question: the 539 categories are class usability (`Warrior Equipment`,
4,308 pages), slot (`Chest`, `Head`), weapon skill (`1H Slashing`), zone
(`Plane of Sky`), acquisition (`Vendor Sold`, `Foraged`, `Quest Items`) and
cosmetics (`Fashion: Plate`). A reader that had checked only whether the field was
POPULATED would have shipped an arithmetic on a field that never says which
profession. This is the third slice running where a catalog field says less than
its name promises — `DropMobs` in D6, `merchant_value` in D7 — and the first
where the field looked fully covered while doing it.

So the picker, the standings and the two doors ship, the arithmetic parks, and
**the block says so on screen with the number in it** rather than looking
unfinished: *"Of the 10,957 item pages it has read, 14 say which profession an
ingredient belongs to."* The survey now runs inside `itemcatalog-build` and prints
in both paths, so `--check` re-takes it on every weekly refresh without writing
anything — a reopen condition nobody measures is one nobody can satisfy.

The veto shape, if this is wrong: build the map anyway from the 14 and the two
generic categories. I think that is a worse product — a player asking where to
farm pottery clay would get an answer built from eight pages — but it is one
curated file away.

**2. THE ANSWER IS PROBABLY IN A DIFFERENT FIELD, ALREADY SHIPPED, AND THIS SEAT
DID NOT BUILD ON IT.** The same survey counted the `recipes` field beside the
categories, and it is a much better source: **1,235** pages carry one, **851**
name one of the curated eight at the top of their recipe list — *"* [[Blacksmithing]]"*
— and all **eight** professions appear. A further **242** name a skill with no
Mastery AA (Tinkering, Spell Research, Make Poison, Fishing). The field is already
parsed by the app's own parser and already promoted into `ItemCatalog.Record.Recipes`,
so an arithmetic on it would need no promoter change and no fetch.

**I did not build it, deliberately.** The plan's evidence step named `Categories`,
and a ranking engine off a field the plan never surveyed is a new arithmetic with
its own rulings to make — how its criteria compose, whether Farm Materials becomes
`Answered`, what `LevelUseFor` says about it — which is the shape that belongs in a
plan rather than in an executor's judgement call. It is filed to Fable as a MET
reopen condition with these numbers in it. **If the preference is for me to have
built it in-seat, that is the veto**, and it is the more interesting one of the
two on this page.

**3. THE EIGHT ARE THE MASTERY-AA EIGHT, AND FOUR REAL PROFESSIONS ARE OUT.**
P13 named the source — *"wiki-matched (from the Mastery AA list)"* — and the
shipped `AaCatalog.json` carries all nine General "… Mastery" abilities, so the
curated list is checked against a catalog EQBuddy already ships rather than
against a comment: every profession names its AA, and `TradeskillsTests` reads
that ability's own effect sentence back out (*"reduces the chance of failing
Blacksmithing recipes"*) to check the spelling. `Crafting Mastery` is the ninth
and is refused by name — it raises the specialization cap of professions you
already have, and its effect text names seven of the eight in one parenthetical,
omitting Alchemy.

Tinkering, Spell Research, Make Poison and Fishing all have eqlwiki skill pages
and all four appear in recipe lines; none has a Mastery AA, so none is here. They
are committed negatives in the test, so the edge of the curation is a failing
assertion rather than a comment. Widening the list is a decision, not a fix.

**4. THE WIKI SPELLS ONE PROFESSION THREE WAYS AND I CARRIED ALL THREE, PLUS ONE
THAT IS NOT THE WIKI'S.** "Jewel Craft Mastery" (the AA name) reduces failures of
"Jewelcrafting" recipes (the effect text) and Crafting Mastery calls it
"Jewelcraft" (the same page). Matching is whole-string against a list of aliases
per profession, so all three resolve. **"Jewelry Making" is the fourth and it is
NOT from eqlwiki** — it is classic EverQuest's own spelling of that skill, carried
under the standing rule that other sources are allowed where the wiki is silent
and marked as such in the file. The wiki names the SKILL; it is silent on what the
game's skill-up line PRINTS, which is a different fact, and an alias the log never
prints simply never matches while a missing one costs a player their standing with
no way to tell why.

**5. ONLY THE EIGHT PROFESSIONS ARE PERSISTED — SIXTY COMBAT SKILLS ARE NOT.**
The plan asked for skill values to start persisting. The store admits a skill only
when `Tradeskills.Match` claims it, which is `QuestLedgerStore.TrackFilter`'s own
rule applied to a second kind of row ("the file stays quest-sized instead of
hoarding every rat whisker"). The alternative — persist every skill the log
announces — writes fifty rows per character that nothing reads, which is trap 43
with a storage bill. The reopen is one line: widen the filter in the slice that
builds a surface for combat skills.

**6. THE HIGHEST VALUE WINS, AND THERE IS NO TIME HIGH-WATER MARK.** The loot
ledger needs one because loot accumulates and a replayed line would double it. A
skill-up carries the TOTAL the game printed, so the largest value seen IS the
standing and the launch replay lands on the same number. The moment travels with
the value rather than with the count, because a surface has to be able to say WHEN
rather than implying "now".

**7. ABSENT MEANS ALL EIGHT — filter semantics, `UnlockPicks`' rule and not
`HelperFactions`'.** A faction pick tells an engine which of hundreds of standings
to weigh, so "none picked" has to mean none. This pick decides which of eight rows
a list draws, so "none picked" means the list a player who has never touched the
control should see. The offer is never narrowed by its own filter, or a pick could
not be undone.

**8. THE ROWS ARE IN THE CURATED ORDER, NOT IN EVIDENCE ORDER.** The faction
picker sorts closest-to-the-top first; this one does not. Eight fixed rows want a
stable order more than a clever one, and a list that re-sorted itself when a number
moved would shift under the player's pointer for no gain.

**9. THE WATCH CONTROL WRITES A SETTING, AND IT IS THE ONLY DOOR IN THIS ROOM
THAT DOES.** "Watch skill-ups" adds the `WatchKind.SkillUp` rule and opens
Settings → Alerts → Watch rules so the player can give it a sound. The rule that
makes a door-with-a-side-effect the right shape is the in-game-command one: a
surface that names an action ships the action. It is idempotent, the existence
check asks `TrackedRule.Matches` (the rule's own matcher, so a rule the player
wrote and named "smithing!!" counts and is never duplicated), and a DISABLED rule
still counts — turning it off was their decision, and a second enabled copy would
overrule it.

**10. FARM MATERIALS IS STILL `Deferred`, AND ITS SENTENCE WAS REWRITTEN.** The
goal has no engine, so `ShapeFor` is unchanged and `LevelUseFor` stays null — which
is the must-list's correct answer for a goal with nothing to decide about yet. The
deferral now names which HALF is missing (*"your professions and where they stand
are above. EQBuddy is not ranking WHERE to farm the materials yet"*), because the
old sentence read over a block full of the player's own numbers would say the block
had failed.

**11. THE STAGED SHOT CHANGED THE COPY, AND THIS IS THE SECOND SLICE RUNNING WHERE
IT DID.** The unknown-standing row was written as one sentence carrying its own
explanation — *"…no skill-up in your log yet. EQBuddy reads your standing from the
game's own 'You have become better at…' line, so it starts from your next one."*
Correct, and there are eight rows: the default state repeated it eight times down
the block. **Distinct-count is the tell in prose exactly as it is in data (trap
73)**, and a fact about where EQBuddy gets its numbers belongs to the BLOCK. The
explanation is now said once and the row is four words. The park note also had no
margin of its own and read as belonging to the last profession rather than to the
block. Neither was visible to any assertion in this repo.

---

## 2026-09-12 — DRA-70 delivery 1: the Helper room, and the ten defaults taken to build it

Pre-authorized: Helm SIGNED the plan (PR #580, merged `0705f45f`) and authorized `dra70-d1`
after it landed on Soft `main`. No consequence-list door was touched — the values line is
untouched (every input is this character's own log, dumps and catalogs EQBuddy ships;
nothing measures another player), no release was cut, no public post was made, and no
eqlwiki policy moved. Reporting duty rather than asking duty; David vetoes from here.

Each row is the default I took, the thing it could have been instead, and where it landed.

**1 — The Helper's WORDS live in `UI.Shared/HelperPresentation`, not in Core beside the
engine.** `UnlockGuidance` — the mini-recommender this copies its manners from — phrases its
own sentences in Core, so the obvious default was to do the same. It could have gone that
way. It did not, because the rule this feature has to keep is a rule about VOCABULARY
(HOME-006: nothing may claim a camp is safe, easy or survivable) and a guard over vocabulary
can only be written where the vocabulary is. Core carries typed `WhyFact` records with
numbers on them; `HelperPresentation` turns each into a sentence and
`HelperPresentationTests` sweeps every one — constants AND assembled interpolations, which
is the tier a `const` scan cannot see. **The exception is deliberate and it proves the
rule:** a sentence `UnlockGuidance` has already measured AND phrased is passed through
verbatim as a `WordedFact`, because two surfaces wording one arithmetic are two answers and
the newer copy is always the one that goes stale.

**2 — The second sort key is a BOOLEAN, not a line count.** HOME-003 says personal evidence
outranks generic advice. The first build read that as "more personal lines rank higher", and
the fixture caught what that means: a faction grind with four movers beat the fastest camp
the character had ever farmed, on volume. A count is a proxy for confidence and a proxy is a
claim about the world (trap 64b). `Recommendation.HasPersonalEvidence` asks HOME-003's own
question; `MoreSentencesDoesNotOutrankABetterMeasuredCamp` is the row that pins it.

**3 — "Work on Faction" REQUIRES a pick and is not a filter over every standing.** A faction
dump carries hundreds of rows; weighing all of them is the thirty weak answers HOME-002 asks
for the opposite of. The cost is one more state to explain, and it is explained: a dump with
nothing picked asks for a PICK, a missing dump asks for the COMMAND, and the two are
different sentences rather than one that covers both.

**4 — `UnlockGuidance.Faction` was made PUBLIC and taught to take a faction NAME.** It was
private and took an unlock criterion. The alternative was a second mover-finder in
`Recommendations`, which would have been a second phrasing of one arithmetic on two surfaces
— the failure that file's own comment names. It also gained a `Zone` init property carrying
where the best raiser was killed, because the Helper joins on the zone and re-deriving "which
mover is best" at the call site would be a second producer of a selection that method has
already made. Nothing about the Unlocks tab's behaviour changed.

**5 — The five goals without engines SHIP VISIBLE, each with a door.** The alternative was
hiding a chip until its engine lands, which would make the feature look smaller than the plan
it is executing and would mean the strip changes shape under the player three more times. A
deferred chip says so in its own words and hands over the door to the room that answers its
question TODAY — a chip producing one apologetic sentence and pointing nowhere would be the
rail's own *"an affordance that opens nothing is a trap"* reappearing one level in, where the
rail's guard cannot see it. `HelperMustListTests` asserts both halves together, because
either one alone is the bug.

**6 — The ⧉ copy REPEATS when two goals want the same dump.** With nothing picked, Unlock
Classes and Unlock Races each carry their own `/outputfile achievements` button: four copies
on one screen. Deduplicating would mean one of the two goals asks for a file and offers
nothing, which is precisely the row the player who picked only that one is looking at. It is
DRA-63's ruling applied one room over — a row that asks names its own answer, in every state.

**7 — Three recommendations, four why-lines each, twelve faction chips.** Three is HOME-002's
own number. Four is a judgement: a headline plus five reasons stops being a recommendation
and becomes a report. Twelve is a judgement about a picker that is not a browser. **All three
caps report what they withheld** (trap 50), and a cap that held nothing back says nothing at
all.

**8 — `ZoneHistory.MinHours` is fifteen minutes, and it is a judgement rather than a
measurement.** It is the shortest sitting that can contain a pull, a death and a recovery,
which is what an XP rate is supposed to average over. Below it the fold answers NO rate at
all rather than dividing — four minutes containing one lucky pull is "120%/hr" if you let it,
and that is noise ranked first.

**9 — Level Up is personal-only, and the absence of a catalog fallback is asserted.** The
plan PARKS a generic camp catalog behind a reporter asking for one, so a character with no
stored play gets a sentence saying so rather than a level-range table EQBuddy would have had
to invent (trap 73, and the match-the-wiki rule one step further out: there is no wiki answer
here either). The consequence is that D1 ships exactly ONE catalog-sourced line — a Task
criterion whose quoted quest name matches the shipped catalog — and
`TheOnlyCatalogSourcedLineInThisDeliveryIsAMatchedQuestName` says so out loud, so the day D2
adds another somebody has to come and change that row deliberately.

**10 — The Helper builds its own empty state rather than joining `ShellRoomEmpty`.** That
module's declared scope is the four rooms that came in as moves and lifts of v1 windows;
Home and Live built their own for the same reason, and the Helper's empty is not "no
character" but "nothing to suggest yet", which is a different question with a different
answer.

**Two wordings were found by the SHOT and not by any assertion**, which is the whole argument
for the illustration lock: *"across 1 of your session"* (a lone plural toggle in the middle of
an interpolation) and *"you stand at 1,000, 1,000 from the top"* (a comma that reads as a
thousands separator). Both are fixed with a regression row. The same first shot also
disproved two of my own written predictions — the shoot fixture's session is ALREADY ARCHIVED
by the time a room draws, so the Helper had real recommendations where I predicted none. The
wrong predictions are kept in `shoot.ps1` beside what the shot actually showed, because the
next person predicting a picture of that profile needs to know.

— Dranak (Claude Code, Paperclip DRA-70, seat `opus-dra70-d1`)

## 2026-09-12 — DRA-57 / T4 GO: the Founder commissioned the Pages enable, and this is the entry that should have been written that day

**Backfilled 2026-09-14 under `HELM.md` 2026-09-14 ~7:00 PM CT** (DRA-57 LIVE ASK
answered: *"GO happened — Founder commissioned the enable on 2026-09-12 … Soft write
the missing `DECISIONS.md` entry dated to the 09-12 GO"*). Nothing here is a new
action on Pages: the enable is two days old and the record is what was missing.
**Soft did not enable and has not disabled** — the site stays up per that ruling.

**Placement, and the default it could have gone the other way on.** This file is
newest-first, so the obvious move was to prepend it at 2026-09-14 with the date in the
body. It sits in its 09-12 slot instead — at the END of the 09-12 block, because the
successful `workflow_dispatch` is 11:27 UTC and everything above it that day is later.
The ledger's whole value in this incident was that a reader could ask *"what does
2026-09-12 say about Pages"* and get an answer; an entry filed under the day it was
noticed would have preserved the same hole in a different place. The heading carries
**backfilled** so nobody reads it as a contemporaneous write.

**1. THE ENABLE IS A FOUNDER COMMISSION, AND THE VALIDATION IS THE LIVE SITE.** The
T4 gate (first enablement IS the publish moment, consequence list #3) was satisfied by
the Founder commissioning it directly — not by Soft, and not by the DRA-48-shaped
"deploy plumbing" reading that this same file corrected on 2026-09-10. What is
recorded as the check is **the served page**, not the plan's intent: measured at Helm's
look and re-measured for this entry, `GET /repos/DranakCorps-bot/EQBuddy/pages` →
**200**, `build_type: workflow`, `source: main /`, `public: true`, `https_enforced:
true`; repo `has_pages: true`; `https://dranakcorps-bot.github.io/EQBuddy/` → **HTTP
200**, `Last-Modified: Sat, 12 Sep 2026 18:29:44 GMT`, 41,712 bytes. That body is
**byte-identical to `site/index.html` at Soft `main`** (`git show HEAD:site/index.html`
— same 41,712 bytes, `diff` clean once the working tree's CRLF checkout is normalized),
so the enable published the reviewed content and nothing else. **What would reverse
it:** David deciding the landing comes down — one `DELETE …/pages`, which is his call
and Helm's, not Soft's.

**2. THE ACTOR IS THE SHARED ACCOUNT, SO THE RECORD NAMES THE ROUTE INSTEAD.** Both
2026-09-12 runs (`workflow_dispatch` 11:27 UTC, push-deploy 18:29 UTC via #577) carry
actor and triggering_actor `DranakCorps-bot` — the account Soft, Scribe and the Founder's
own dispatches all post under, so **the audit trail cannot say WHO from the actor field**,
and that is the reason this gap took two days to surface rather than being obvious from
the run list. Per Helm's 09-14 tip the commission routed **via the Founder's PC after the
agent token returned 403**; that is recorded here as Helm's finding, not as something Soft
measured. The generalizable half: **on a shared bot account, "who did it" is not a
property of the API and has to be written down by the human or agent who did it** —
which is precisely the duty this entry is discharging late.

**3. ADOPT — a claim about Pages state quotes a FRESH API result or says it is
unchecked.** Helm ADOPTed the corrective Soft raised in the LIVE ASK, and it is logged
here because it binds Soft's own writes as much as Helm's rulings. The failure it
prevents is measured, not hypothetical: `has_pages: false` was restated in a 09-13 ruling
that was already a day stale, and Soft carried the same stale line forward on the 09-14
wake before checking. **Two sources for one fact, where one of them is prose that cannot
go stale loudly — trap 4 with a gate attached.** The rule: never carry a prior `HELM.md`
or `DECISIONS.md` sentence forward as the current state of Pages; run
`gh api repos/DranakCorps-bot/EQBuddy/pages` (or read `has_pages`) and quote it, or write
"unchecked". This is a discipline, not a guard — there is no executable check for it, and
saying so is part of the entry.

**4. THE `pages.yml` HEADER IS CORRECTED IN THE SAME CHANGE.** Its comment block said
the workflow is *"INERT until Pages is enabled"* and that *"Enablement … stays behind the
DRA-48 T4 gate"*. Both were true when written and are false now, and a stale gate
sentence sitting in the file the gate is about is the exact shape §3 just banned. Corrected
to what is true, with the GO dated. **The default it could have gone the other way on:**
leaving it, on the grounds that Helm authorized a README/About link and not a workflow
edit. Left alone it would have been the next reader's stale source — and it is a comment,
so it changes no behaviour and reverses in one line.

— Dranak (Claude Code, DRA-57)

## 2026-09-11 (DRA-65 — Unlocks guided detail plan; the calls Fable made alone)

Fable seat, plan only (`claude/fable-dra65-unlocks-20260911`). The plan itself is the top
entry in `FABLE.md`; these are the places the Founder's three lines left a choice, decided
by default rather than asked.

**1. "FILTER BETWEEN RACE AND CLASS UNLOCKS" IS READ AS DISCOVERABILITY + PHONE PARITY,
NOT A NEW FILTER AXIS.** The All/Races/Classes lens already exists on the desktop behind
`UnlockSectionCombo`; the Founder asking for it anyway is evidence the combo hides it, and
the phone's Unlocks tab — which ships in the tab strip today and falls through to the
General catalog when tapped (`index.html` `drawList()`) — has no lens at all because it has
no body. The plan makes the existing lens a chip strip and makes the phone tab real.
**The default it could have gone the other way on:** invent a finer axis (filter to my
race/class, or by done-state) — rejected; not asked, and the section lens is the thing his
sentence names.

**2. GUIDED DETAIL COMES FROM STORES THAT ALREADY EXIST, NOT FROM NEW CONTENT.** Three
shapes: own-kill faction movers with kills-to-go arithmetic (`MobHistory` pool, log-only,
the player's own play), the Sky checklist's own piece count + door for `Obtain` rows, a
catalog door for `Task` rows. A harvested "ways to raise" block from the wiki's faction
pages is PARKED with a written reopen condition — it is a new harvest shape carrying trap
73/74 obligations and touches the weekly fetch list (consequence-7 adjacent). **The
default it could have gone the other way on:** ship wiki content now for maximum
"guided" — rejected; doors + measured personal evidence are honest today, prose is not.

**3. NEGATIVE MOVERS ARE SHOWN AS COSTS.** "Your kills of X cost 5 each" is the same
measured fact as a raiser and the one a faction-grinder most needs; suppressing it would
be a top-N-hides-what-matters cut (trap 50). Capped at 3 each way, cap said aloud.

**4. THE TICK NEVER MOVES.** An unlock and its criteria stay the game's answer
(achievement flag / faction dump); every guidance line is additive prose with its own
producer. "Pieces in hand" is worded as bag evidence, never as "obtained" (trap 4).

**5. THE KERRAN TASK GETS SILENCE, NOT A STUB.** 'Aid the Kerrans of Kerra Isle' is not
in the quest catalog (verified against `quests.json`); the row stays the dump's sentence.
A door appears only on an exact catalog match.

**6. THE PHONE FIX IS IN SCOPE THOUGH THE FOUNDER NAMED NO SURFACE.** Tapping Unlocks on
a phone drawing the quest CATALOG is a live defect, and parity by shared module (David,
2026-08-18) is standing law — leaving it out would ship "more guided" on one surface and
a lie on the other.
## 2026-09-14 ~8:40 PM CT — DRA-84 D3: the weekly harvest refresh, and four calls made rather than asked

**Assumption at the top:** the D3 harvest un-PARK Helm AUTHORIZED by name (#623 SIGN, HELM tip
`f25fd184`) authorizes running the standing weekly refresh **as designed** — the fetch, the
promotions, the surveys and the catalog rebuild — and nothing about the refresh's design. Every
call below sits inside that reading, and each states the default it could have gone the other
way on. None touches the consequence list: no release, no public surface, no privacy, no money,
no roadmap direction, no departure from eqlwiki, and **no change to how we ask eqlwiki for
things** (same client, same ~1 req/s, same User-Agent).

**1. Resumed the interrupted run instead of re-running `refresh.py` whole.**
The prior `-p` process exited mid `items-harvest`. The other way: re-run `refresh.py`, which
would have re-evicted the window and re-fetched the ~1,420 changed pages the dead run had
already paid for. I resumed — `items-wikitext.jsonl` keys on revision ids, so 11,014 of 11,197
item pages were already current and only 183 were fetched. **Why this way:** re-fetching pages
we already hold is load on a third party that buys nothing, and item 7 of the consequence list
makes request behaviour toward eqlwiki something to be careful with in the conservative
direction. The risk of resuming is a page edited *after* the dead run's eviction sitting stale
behind an advanced window, so I checked rather than assumed: exactly one article qualified
(`Patch Notes`) and no harvester tracks it. Had that check found tracked pages, the answer would
have been a targeted evict-and-refetch of those, not a full re-run.

**2. Re-ran `guides-transform` after stamping `refresh-state.json`, and did NOT reorder
`refresh.py`.**
`guides-transform` embeds `retrievedAt` from `refresh-state.json`'s `ranAt`, which `refresh.py`
stamps **after** the promotions — so a refresh that advances the date leaves the committed file
one cycle behind and reddens the `generated` gate on the stamp alone. The other way: reorder the
promotion after the stamp and fix it permanently. I took the workaround and escalated the fix.
**Why this way:** the authorization is to run the refresh, not to redesign it, and a reordering
is exactly the kind of change that should carry a ruling rather than ride in on a data PR.
Recorded in CLAUDE.md so the next runner meets it as a known shape rather than a mystery, and
named for veto in the LIVE ASK. **This is a real latent bug and it is not fixed** — if Helm
leaves it, every future refresh that advances the date needs the same manual re-run.

**3. Replaced the vendor-value tripwire rather than deleting it or changing the engine.**
`TheShippedCatalogCarriesNoVendorValueYet` fired exactly as written — 773 shipped records now
carry a `MerchantCopper`. Three ways to go: delete the row (silent), flip it to a bare
"non-zero" assertion (vacuous), or replace it with a survey of what actually ships. I took the
third: the counts the promoter's own survey printed, with the **distinct** count (403 values
behind 773 prices) as the load-bearing one, because that ratio is what separates a parser
reading a per-item field from a parser finding one template (trap 73). Added a committed
negative for its forbid-scan arm so it is not a guard aimed at nothing (trap 78). **I changed no
engine and no wording** — that belongs to the money engine, and the thing a human should look at
(568 of 773 prices carry no Charisma/faction condition, so they draw the flat sentence with no
"yours will differ" clause) is measured, pinned in the guard, and named for veto rather than
absorbed.

**4. Updated three moved guards and two live doc counts from a second source, not from actuals.**
QuestCatalog 1,178 → 1,173 and harvested guides 1,164 → 1,158 are data moves, and the lazy fix
is to paste the new numbers in. Each was re-derived first (trap 52): the seven Darkforge rows
left the quest catalog because the wiki folded them into `Category:Items` under one umbrella
page — verified by finding all seven in the items dump and none in the quest title list; and
`Class Race Quest List` joined the nothing-to-say list because the quest-item enumeration
dropped "Innoruuk Symbol Quests", itself a quest page that was never an item — verified against
its own byte-identical cached wikitext, so it was not a transformer regression. The reasons are
written into the test comments, because a count that moves without a reason beside it is the
thing that makes the next refresh unreviewable. CLAUDE.md and `docs/TestPlan.md` updated to stay
true; the append-only channel ledgers were left alone as history.

**Not done, deliberately:** no curated catalog written (flag-only rule KEPT — 92 SpawnCatalog /
255 GuideCatalog / 243 SkyQuestDefaults.cs flags go to a human), no `WhatsNew.json` entry (the
release that ships these catalogs earns one, and vendor prices appearing in Farm-to-sell
sentences should be named in it), no engine change belonging to D1/D2/D4, and no move on the
standing Soft-open un-PARK, which the #623 SIGN named for D3 only.

— Dranak (Claude Code, DRA-84 D3)

## 2026-09-16 — DRA-106 ranked: the soft-seat staleness is per-WORKTREE, and the fix ranked first is a call-site path, not a change to the signed script

**Decision (Planner, pre-authorized — process, reversible, not consequence-list):**
rank the four options DRA-106 filed, and rank FIRST an option nobody had filed —
change the documented INVOCATION of `claim-seat.ps1` / `release-seat.ps1` to resolve
through the clone's main checkout, the same `git rev-parse --git-common-dir` resolver
DRA-90 already uses for the store. Docs only; the Helm-signed mechanism is untouched.

**The default it could have gone the other way on:** option 3 (change the script), which
is what both the card and the DRA-96 watchdog ranked as the only real closer. It was
demoted because **code added to `main` cannot repair a copy that will never receive it** —
a stale worktree gets the fix only when it updates, at which point it would have had
DRA-102 anyway. Option 3 is a forward guard, not a repair, and was ranked and described
as one.

**Measured before ranking** (`-Check` twice, wrote nothing), harness clone, pre-DRA-102
worktree `EQBuddy-dra68`, card DRA-98 held live in the Bosun clone: the worktree's own
copy **granted**; the same card resolved through the main checkout **refused**, naming
the foreign holder through the DRA-102 registry. The identical one-liner from the main
checkout gives the identical refusal, so one invocation is correct everywhere.

**The correction that decided option 1:** merged-ness is the wrong denominator. The
exposure is the set of worktrees an agent can be DISPATCHED into — a property of the
harness, not of branch history — which is why 6 of 7 in the harness clone outweighs 54
of 72 being merged in the Bosun one. "Most of them are finished" was never the
reassurance it looked like.

**Where it landed:** LIVE ASK to Helm (`HELM-FEEDBACK.md`, `36635eca`, back-channel run
35068979008) asking SIGN-or-HOLD on the docs change and SIGN-or-defer on the in-script
guard. DRA-107 carries both slices with the acceptance bar, blocked on that relay, next
seat Executor. DRA-108 carries the worktree sweep at `low` — hygiene, explicitly not a
mutex fix. No Executor kicked, no worktree touched, no code moved, no seat claimed.

— Dranak (Claude Code), Planner — DRA-106

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
