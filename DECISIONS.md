# Decisions

**How to read this** — one dated `##` block per decision: what was chosen, what it could have gone to instead, and why. **This file is not in date order** — read the ISO date in each heading, never the position.

**Where the archive is** — older blocks are rotated **verbatim** into [`docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md`](docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md). Nothing is ever deleted; an archived block never revives a hold and never commissions work.

**What never rotates** — an open ask, an unexpired PARK or HOLD, and a standing rule stay in this file at any age, however old they are.

**Last cut** — 2026-09-21 by DRA-294, under the Helm arm (c) discharge floor ruled in DRA-282 Q2 item 4 (tip PR #773, `9121470c`): dropped the file from 55,411 B to the 32,768 B floor. Ten blocks moved verbatim to the archive — `DRA-199`, `DRA-180 D2`, `DRA-180 D3`, `DRA-164 D1–D3`, `DRA-149 D5`, `DRA-149 D4`, `DRA-149 D2`, `DRA-164 D4`, `DRA-181 D4`, `DRA-180 D1` — each re-checked by hand against line 7's floor and found to hold no open ask, unexpired PARK/HOLD or standing rule resident IN THIS FILE: DRA-164 D1–D3 item 8's republish question and DRA-164 D4 item 1's SIGN-as-PR-comment question are narrative, not markers, and both are overtaken by continued practice since (PR-comment SIGNs have landed routinely, e.g. DRA-262); DRA-180 D2/D3's "FABLE.md item not drained" notes describe `FABLE.md`'s own live state, not a hold here. The `exo-experiment:` STANDING block (re-pinned by DRA-231) and the DRA-161 Helm-LOCK entry are both unaffected and stay — DRA-161's LOCK is independently live and restated in `HELM.md` ("Soft/Planner may Soft file a fresh amended DRA-55 plan"), but nothing forced its removal to meet the floor so it was kept here too rather than risk-judged out. `DRA-262 D2` (2026-09-21) stays as the current tip. Full text of all ten: `docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md`.

## 2026-09-20 — STANDING: the `exo-experiment:` tag index (re-pinned by DRA-231)

**This block never rotates.** Plan §10.1 makes the tag below the thing the M0-exit doctrine
capture cites, and `Get-Experiments` in `scripts/exo-metrics.ps1` regenerates the "Experiments in
flight" table in `docs/ops/exo-dashboard.md` by reading these lines — each tag *and the judging
clause running from it to the next blank line* — out of **this file**. DRA-231's cut moved the five
2026-09-14 entries that first carried them into the archive, so the tags are re-pinned here
**verbatim** under DRA-144's never-rotate floor: all six experiments they name are still in flight.
Tags registered after that cut are appended below the re-pinned six and live under the same floor:
`challenger-seat` (DRA-300) is the first.
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

`exo-experiment: challenger-seat` — judged by *the two-direction challenge count* that §8 of
the ops repo's `CHALLENGER_ROLE_CHARTER.md` (under `purpose/`) defines, both sides kept because a one-sided count is how
`seat-mutex` above failed. **Direction A — the challenge that was owed and never came:** a plan
shipped, the premise failed, and either no wake fired or the assessment said PROCEED; loud when it
lands, and counted from the failure backwards. **Stated net of direction B — the challenge that
cost more than it bought:** a wake fired, the plan changed, and the change bought nothing. §8 names
direction B as *the direction a red team fails silently in*, because every individual challenge is
defensible and nobody files a complaint about excess rigour — left uncounted, it is how the
Challenger becomes a mandatory review gate while reporting green, which is the same one-sided defect
the `seat-mutex` tag records. **Kill criterion, stated in advance:** after **ten** C1–C4 wakes, a
direction-B count exceeding the direction-A count means the seat is **DROPped, not tuned** — the bar
is a rate and not an anecdote. **Baseline frozen, no verdict reading yet:** the §8 30-day baseline
was frozen on **DRA-301** (2026-09-22, window 2026-08-23 → 2026-09-22, 265 `done` cards): SIGNs later
reversed or narrowed **0**, plans whose stated premise was contradicted by what shipped **5** (1.9%).
§8 required it *before the first wake* and it froze after one, so it was late by one wake. That is a
pre-seat reference, not a direction-A/B count, and this tag claims no A/B reading. Wake count at
registration was **1 of 10** (DRA-299, 2026-09-22, exit 0). Registered from the commit that stood
the seat up, per §8, so the metric exists before the readings do rather than after.

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

[Helm decode / DRA-330: the relay substitution reads as **No archive repair without separate Helm ruling.**
The LOCK forbids archive repair without a separate Helm ruling. It is not a permission to repair. Readable
authority is this gloss, not the quoted comment.]

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


## 2026-09-22 — Support EQBuddy moves from landing footer to hero (Founder)

**Chosen:** keep the DRA-69 Stripe Payment Link
(`https://buy.stripe.com/aFa00k1tE2064qRb0S9R600`, new tab, text link — not an
embed) and place the quiet optional-support sentence under the hero pill row,
removing it from the footer. Guard renamed to
`LandingSourceClaimsTests.TheHeroCarriesAQuietSupportLink` (asserts hero has it
and footer does not).

**Default it could have gone the other way on:** leave it in the footer, which
is what Helm signed for DRA-69 (PR #577: footer text-weight KEEP, not hero).

**Why this way:** Founder ask to move Support EQBuddy near the top of the
landing so it is visible without scrolling, still optional community support
for a free program — not a CTA shout. Hero under the pills preferred over a
topbar nav item so it stays readable without crowding the section links.


## 2026-09-22 — Support EQBuddy copy: amount chosen on Stripe (Founder follow-up)

**Chosen:** keep the same Payment Link URL; reword the hero line so the gift is
optional voluntary support and the donor chooses any amount on Stripe's
checkout. Amount choice is a Stripe Dashboard Payment Link setting — not an
on-page picker and not a second URL.

**Default it could have gone the other way on:** leave the shorter "If this
companion is useful, you can Support EQBuddy." sentence, which never named $5
but also never said the donor picks the amount.

**Why this way:** Founder follow-up on the hero-move PR — allow a custom
donation amount, and do not imply a fixed price on the landing. Soft LEAVE
inventing a second Payment Link until Helm supplies a replacement URL.


## 2026-09-22 — Support EQBuddy is a topbar chip, not a hero paragraph (Founder)

**Chosen:** remove the hero `<p class="support">` sentence shipped in #826 and
place a quiet `nav.topbar` chip labeled Support EQBuddy at the top-right
(after GitHub). Same Payment Link URL, new tab, no embed. Guard renamed to
`LandingSourceClaimsTests.TheTopbarCarriesAQuietSupportChip` (topbar has it;
hero and footer must not).

**Default it could have gone the other way on:** keep the hero paragraph #826
just landed, which was near the top but not the top-right chip the Founder
meant.

**Why this way:** Founder feedback after #826 merged — he wanted a chip at the
top right of the page, not the hero paragraph under the pills. Soft LEAVE
inventing a second Payment Link or changing the Stripe URL.

## 2026-09-22 — Landing hero KPIs are measured counts

**Chosen:** the four tiles in the hero KPI band on `site/index.html` read, in
order, Quests Tracked, Items Cataloged, Downloads, and Max Concurrent Users.
`site/metrics.json` is the source of truth the page fetches. The `.n` text is
that same snapshot, so the band still reads when the fetch does not run.
Shipped 2026-09-22: `questsTracked` **1173** (length of the `quests` array in
`src/EQBuddy.Core/Data/QuestCatalog.json`), `itemsCataloged` **11196** (length
of the `Items` array in `src/EQBuddy.Core/Data/ItemCatalog.json.gz`),
`downloads` **28462** (sum of GitHub release asset `EQBuddySetup.exe`
`download_count` across all releases — installer downloads, not unique people),
`maxConcurrentUsers` **null**, painted as an em dash.

**How to refresh:** re-count those two arrays and write the counts into
`site/metrics.json`. Re-sum `EQBuddySetup.exe` download counts from the GitHub
releases API and write that sum into `downloads`. When opt-in telemetry
(DRA-336) publishes a max-concurrent figure, replace null with that integer.
Then set each hero `.n` to the formatted value (thousands separators; null
stays an em dash). `LandingSourceClaimsTests.TheHeroKpisAreMeasuredStats`
refuses a drift between the JSON, the catalog arrays, and the painted text,
and it refuses a concurrent integer while the value is still null.

**Default it could have gone the other way on:** leave the four principle zeros
in the band (0 game-memory reads, 0 accounts, 0 telemetry by default, 11,000+
catalog). Those sentences stay on the page outside the band — the pills and
the principles section — because they are still the product boundary.

**Why this way:** Founder ask 2026-09-22. The band is a place for counts a
reader can check. A concurrent number that telemetry has not published would
be a figure EQBuddy invented.

## 2026-09-22 — Max Concurrent Users empty state says why (Founder)

**Chosen:** while `maxConcurrentUsers` stays null, the hero tile shows "Telemetry not live yet" instead of the em dash #830 painted.

## 2026-09-23 — Support EQBuddy tip link is Ko-fi (Founder)

**Chosen:** the topbar chip labeled Support EQBuddy opens `https://ko-fi.com/eqbuddy`
in a new tab. Same placement, same label. The Stripe Payment Link is gone.
Framing stays an optional community tip for a free program — not charity,
crowdfunding, or paid access, and the label is not Donate.

**Default it could have gone the other way on:** keep the DRA-69 Stripe Payment
Link (`https://buy.stripe.com/aFa00k1tE2064qRb0S9R600`), which the 2026-09-22
entries froze.

**Why this way:** Founder ask 2026-09-23. That Payment Link is dead; Ko-fi is
the live tip URL.

## 2026-09-18 - Sky ticks: ledger-keyed auto-tick, hand-ins read from the log, runes spared on a scan

Hateborne's own report, asked in session (not an inbox item). The Plane of Sky tab ticked
High Quality Raiment and Wind Rune Meda with none of either held. Calls made, each with the
default it could have gone the other way on:

1. **Hand-in parsing was built in session, not routed through a Fable plan.** A new log
   grammar plus consumption rules reads as V2 under this file's routing; Hateborne chose to
   build it here (question tool). It is one small class (`HandInTracker`), and every exit it
   records goes through the ledger's existing replay-safe time gate.
2. **A refusal cancels the WHOLE trade.** "You can have it back" never names the item; the
   other way was guessing which item came back. The cost: a bag item stays over-counted until
   the next scan, a currency item until the player corrects it.
3. **Only `*` guesses are ever taken back**, never a tick the player made, one on a class
   they picked, or one an import proved. The other way was reconciling every tick to the count.
4. **A scan never judges a Wind Rune** (Hateborne's call, asked because it ships to every
   player). Runes store to currency since 2026-09-16 and every scan since recorded them as
   zero; `CurrencyItems` names them before the log teaches it. The other way would have
   cleared 55 of his 68 guesses, many for runes he holds. The cost: guesses left over from
   before this build need a right-click.
5. **Folds live for the RUN, on MainWindow, shared by both hosts.** The 2026-09-03 ruling says
   "session-only"; this reads session as the run (Hateborne: "across the session (but not
   across all sessions)"). The other way was one store per host. A fresh Quest Tracker also
   reopens on the last tab now (the host always kept it and nothing read it back), except
   when the map badge opens it on an item filter.
6. **Ratchet room came from dead code, not a baseline bump.** `LogParser`'s second
   `LocationRx` check could never fire: the anchored fast path returns first.

- Claude Code (Hateborne's session)

## 2026-09-25 - DRA-339: Quick Buff landings take your last own rank; buff fading chips get a switch

1. **Root cause is Quick Buff, measured in the Founder's log.** 13:37:17 on 09-22: "You
   activate Quick Buff.", every landing 3 s later, no cast line. Unresolved landings armed at
   rank I's wiki base without SCR (Thorns 900 s vs his 1,416 s). The other way was a longer
   alert floor or silencing estimated buffs; both hide the defect rather than fix it.
2. **The rank is the one your log last showed you casting for that line.** An inference, so
   it never teaches a duration (the `DumpNarrowed` gate). Other way: spellbook-dump rank.
3. **`BuffFadeChipsEnabled` defaults ON.** The Founder's authorizing comment says "default
   on, Founder wants off"; Planner's re-scope read it as default OFF. I followed the Founder's
   words: turning it off for everyone would remove a working alert from every player to
   serve one preference. Veto = flip one default.
4. **BuffWarnSeconds=10 stopgap stands down.** It was never written to any profile; with
   (1) fixed and the switch shipped, it has nothing left to cover.

- Dranak (Claude Code), Sr Executor

## 2026-09-29 - #954: buff chips get right-click dismiss and a player-set length

1. **A dismissal is of ONE landing, not of the buff.** The next real landing shows again (the
   slow-chip rule). Other way: "never show this buff again" per spell — rejected because the
   family Mute already answers "I never want these", and a per-spell hide would silently eat a
   buff the player later starts relying on.
2. **Dismissals and lengths persist, per character** (`buff-player.json`), keyed on the
   landing's LOG time. Other way: RAM only, like the slow chip — rejected, the launch replay
   would undo it (trap 85), which is exactly the reporter's "a new session does not clear it".
3. **A typed length is filed under the RANKED name** where the log named one, else the chip's
   label. Other way: always the label (simpler) — rejected per trap 71; a rank upgrade would
   inherit the old rank's number and alert early.
4. **Gestures: right-click dismisses, double-click opens a small length editor**, on the HUD
   row and the Buffs card alike. Other way: a right-click context menu with both verbs —
   rejected because right-click already means "dismiss" on every other chip family.
5. **The typed length outranks learned durations.** Other way: a later natural fade could
   overrule it. Kept the spawn-override rule ("typed by the player — outranks inference,
   forever"); "Use EQBuddy's length" is the way back.
## 2026-09-29 - Tracked quests on the minimized bar (Founder request)
0. **Surface: the Founder's direct ask is the ruling.** CLAUDE.md's surface table puts
   quests on the phone, and the Quests CARD left the widget in the Helm-signed HUD
   subtraction (2026-09-05). This is a chip with a hover peek, not the card back, and David
   asked for it by name. No hold names it. Helm is told in the PR description, not
   HELM-FEEDBACK.md: that file is 115 B under its 64 KiB ceiling and waits on the
   DRA-154 rotation, which a feature branch may not do.
1. **"Track" is the existing 📌, not a new list.** `QuestLedgerStore.Tracked` already backed
   the detail pane's pin and the phone's 📌. Other way: a separate "on the bar" list -
   rejected, two lists answering "which quests am I following" is trap 4.
2. **The chip is a ★ key ("quests"), and untracking the last quest does NOT remove it.** The
   empty state "No quests being tracked – View Quests" only exists on a chip that outlives
   its quests; unticking Tracked quests in Options is how it leaves. Other way: the
   watch-pin model (chip exists only while something is tracked) - rejected, it has no
   empty state to show.
3. **Ticking Track stars the chip; unticking never un-stars it.** Other way: track from the
   phone also stars it - rejected, a phone tap should not grow a chip on a bar whose owner
   never asked for one (pinned by `WithoutTheStarThereIsNoQuestsChipEvenWithAQuestTracked`).
4. **The link NAVIGATES to Guide → Quests; it is not a pop-out.** The EQBuddy window never
   reports closing to the bar, so a pop-out would leave the chip unable to peek while the
   Guide stayed open. `HudExpand.PopsOut` names the one exception.
5. **The row's 📌 icon was removed from the Quests list** - the Track tick on the left says
   the same fact. The detail pane's pin button stays.
6. **Fixed on the way: `QuestMatcher` dropped tracked quests with no turn-in items** from
   "mine" (and would have from this peek). The item test ran before the tracked test.
- Dranak (Claude Code)
## 2026-09-29 - Track on the Epic 1.0 and Plane of Sky tabs (Founder smoke)
1. **A Sky reward is tracked by its catalog quest name, in the SAME list as a quest.** Each
   reward already is a catalog quest ("Bard Sky Test: Amulet of the Fae"), so the Sky tab's
   tick and that quest's Quests-tab tick are one fact. Other way: a Sky list of its own -
   rejected, two ticks for one quest that could disagree is trap 4.
2. **An Epic section gets its OWN list (`TrackedSections`), keyed `guideId/stageId`.** Other
   way: a prefixed key inside `Tracked` - rejected, the phone and the matcher read that list
   as catalog quest names and would meet a foreign string. The stage ID, not the heading
   text: Cleric/Druid/Rogue rows say "Checklist" where their stage says "<Class> Epic Quest".
3. **The peek shows each row the way ITS tab does** - a Sky reward's steps and the Epic
   section's steps with the next one - not the Quests tab's bag-count fraction for the same
   name. Other way: one uniform row - rejected, it would contradict the tab the player plays
   that quest on.
4. **Epic section ticks show only while the epic is expanded** - the headings do not exist
   folded. Other way: a tick per section on the folded heading line - rejected as a new
   layout nobody has signed; Bevel's unruled "section 3 of 5" item is still open.
5. **Not on the phone yet.** The phone already shows a Sky reward's quest as tracked
   (same list); Epic sections have no phone surface. Logged as the gap, not built.
- Dranak (Claude Code)
## 2026-09-29 - Tracked quests: every step behind a +/-, and the list pops out (Founder request)
1. **Folds start SHUT, stored as the expanded exception** (`AppSettings.TrackedQuestsExpanded`,
   the `GuideExpanded` idiom). Other way: start open, since the ask was "show all the
   information". Rejected because the peek is a hover over the game and an Epic section alone
   can be twenty steps; the one-line summary stays, and a + is one click that is remembered.
   Flip is one line if David wants them open.
2. **One fold list for both hosts** (the bar's peek and the float). Other way: a list per
   host. Rejected, the float is the peek popped out and should not disagree with it.
3. **An unguided quest's steps are its turn-in items with have/need**; a guided one's are its
   guide objectives (the detail pane's `ApplyQuest`), with no separate items list beside them.
   Other way: both. Rejected, the pane's guide already absorbs the turn-ins.
4. **Steps are read-only in the peek and the float.** A step is ticked on its tab, where the
   loot and hand-in routing live. Other way: tick from the peek. Deferred, not asked for.
5. **The chip's pop-out is now a float (`BreakoutKind.Quests`), and "View Quests" stays
   beside it as a link to the Guide.** The one-day Guide destination host and `PopsOut` are
   retired. Other way: keep the pop-out going to the Guide and add a second button for the
   float. Rejected, every other chip's pop-out is its float.
6. **The float arrives UNPINNED** - in the default `DisabledBreakouts`, and added once to
   existing profiles by `MigrateQuestsFloatOff`. Other way: open by itself like Damage.
   Rejected, a new always-on-top window on the next minimise is nobody's request.
7. **The peek keeps its 5-row cap; the float is uncapped.** The cap line now names both ways
   to the rest ("pop this out, or View Quests").
## 2026-09-29 - Epic steps: a round mark to click, Track on sections only (Founder)
David, asked in session: each Epic step starts with an EMPTY CIRCLE; clicking it turns it
into a GREEN CHECK and strikes the step through; clicking again undoes it. Track stays on
the section headings only. What was left to decide, and how:
1. **The circle is a `CheckBox` with its own template (`EQBuddy/StepMark`), not a new
   control.** Same store, same `Checked`/`Unchecked` wiring, same `IsChecked` every sweep and
   dump fact reads, the UIA Toggle pattern and keyboard focus for free. Other way: a
   handled vector or a Button - rejected, it would need all of that rebuilt and a second
   writer for the tick (trap 4). Enter toggles as well as Space, which a stock box does not.
2. **Epic tab only, including its SEARCH results.** Sky keeps its square boxes: its Track
   tick sits in the reward heading's own column and never shared a line with a row. The
   Epic tab's search view draws the same steps, so it wears the same mark. Other way: every
   tab - rejected, the Founder named Epic, and a Sky row is an item you hold.
3. **Done is struck through on the Epic tab only** (`QuestPresentation.StrikesDone`).
   Everywhere a square box stays, strike-through already means SKIPPED. On Epic the ring
   tells them apart: a done step has a green check, a skipped one an empty ring.
4. **`questsGuideSkipped` now means struck AND not done**, because done rows are struck
   too. Other way: a second tag on the text - rejected, the check state is already the fact.
5. **Solarized's green is its palette's `GoodBrush`** (olive, #859900), not a new colour.
## 2026-09-29 - DPS & HPS by type: every meter row wears its kind's colour
Founder-approved mockup "option A". Assumptions I made, and the default each could have
gone the other way on:
1. **The mix strip is in a FIXED kind order, not damage order.** The rows under it are
   already in damage order; a strip whose segments swap places when two kinds cross reads as
   movement where only a share moved. The width carries the share. Other way: damage order,
   matching the rows. One constant (`OutputKindPresentation.Order`) to flip.
2. **Two mockup colours were lifted to clear 3:1**, not the floor lowered: Proc `#e0679a` →
   `#e36f9f` and Other `#8a8f98` → `#959aa3`. Both missed only on SolarizedDark's panel wash
   (2.97 and 2.92), the lowest-contrast ground a dark theme ships. Light set unchanged.
3. **Light or dark set is picked from the theme's BgBrush luminance (> 0.4 = light)**, as
   derived tones, so no theme row grows eleven values and a Custom theme lands on the side
   its own background is on. Other way: a per-theme table.
4. **No strip when every row is Other.** An archived session's rows deserialize as Other and
   draw grey; one grey bar labelled "Other" would explain nothing. Other beside a classified
   kind IS drawn and named.
5. **History is not coloured in this change.** The History window and the session-review
   pull panes draw `HistoryBreakdownRow`s, which carry no kind; they keep the accent bar.
   Sessions saved before this build carry no kind at all, so if a meter ever draws one it is
   grey. Reviewing an archived LOG replays it and so is coloured. History's all-time ability
   lists will show an old "Stinging Swarm" row (ticks and hits merged) beside a new
   "Stinging Swarm (DoT)" row; nothing merges them back.
6. **Session heal rows dropped their kind at snapshot time** in the Core half (43cafb80);
   fixed in-line (same line count, SessionStats stays at its ratchet), caught by
   `OutputKindTests`.
7. **The phone gets the legend's words over the wire** ("DoT 18%"), never composed on the
   page (trap 32); the colours ride the theme section as `kind*` tokens.
- Dranak (Claude Code)
## 2026-09-29 - Type colours locked, and the player may pick their own (PR #964)
David: "make sure the colors stay consistent for type so if they're not locked, please lock
them" and "in options we can let people color code the types to whichever color they want
from a color wheel". Decided in the parent session, not re-asked:
1. **Consistency = one colour per type on every surface**, every desktop meter and the phone,
   through the one producer (`ThemeTones.Derive`). The default sets stay: dark everywhere but
   Solarized, light (deeper shades of the same hues) on Solarized.
2. **Lock = literal hex per kind in a test** (`KindColourTests`), a committed copy rather than
   a read of `ThemeTones`' arrays, keyed by kind. The phone page's CSS fallbacks are pinned to
   the dark set. Other way: comparing against the arrays, which an edit carries along.
3. **A pick applies in EVERY theme**, overriding both the dark and the light default for that
   type. Other way: a pick per theme. One pick is what "color code the types" asks for; a
   player who picks a colour that is poor on Solarized can see it and change it.
4. **Picks ride the palette** as explicit `Kind*Brush` rows (`CustomTheme.PaletteFor`), and an
   explicit row wins in `Derive`. So the desktop dictionary, the phone's first frame and every
   broadcast carry the same answer with no second path. Invalid values are ignored.
5. **The wheel applies live while dragging and persists when it settles** (release, Enter,
   close). Escape or a click outside keeps the last pick; there is no Cancel. Other way: an
   OK/Cancel dialog.
6. **Block placement:** Options → Look, directly under the theme picker (and its Custom rows),
   with its explanation on an ⓘ (the prose-to-hover rule). The committed `options-window`
   shot predates the block and was not re-shot; three new recipes cover it.

- Dranak (Claude Code)

## 2026-09-29 - v2.0.1 released WITHOUT the Fable release review (Founder override)

1. **David chose to release 2.0.1 before Fable reviewed it.** He was asked in session with
   the question tool, the review's cost stated ("an hour to the next morning"), and waiting
   recommended. His answer: "this time. You are correct to prefer 1 but I am reworking the
   organization and it's not ready." CLAUDE.md allows this ("he can override knowingly").
   The default it went against: gates green, then Fable reviews, then David.
2. **Fable gets an after-the-fact review request** in FABLE-FEEDBACK.md with the tag, the
   commit range and the gate numbers, once the release is out. Anything it finds becomes a
   2.0.2 item, not a pulled release.
3. **What went out** (all smoke-tested by David on his own machine first): #955 Reading-log
   start-up, #956 buff chip dismiss and length, #958 tracked quests on the bar, #960 no
   tooltip over bar panels, #962 Epic step circles, #963 tracked-quest +/- folds and the
   Quests float, #964 DPS/HPS colour by type with locked defaults and a colour wheel.
   `docs/release-notes/v2.0.1.md` was widened to name all of them.

- Dranak (Claude Code)

## 2026-09-29 - v2.0.1 shipped out of process: correction to the entry above

1. **No Fable review, before or after.** The entry above said Fable would get an
   after-the-fact review request. David then said: "It's all you. There is no Helm right
   now. We are working out of process for this release" and "Fable isn't reviewing this one
   either". So no FABLE-FEEDBACK request was filed and Helm was not woken. David is
   reworking the organization. Neither step was forgotten; both were waived for this release.
2. **Released 2026-09-29 ~5:30 PM CT** by Claude, on David's standing "push it live when
   it's ready" while he was away. Tag `v2.0.1` at `c694b523`, whose tree is byte-identical
   to #956's head `1f949ba8`, where both required checks passed. Verified after the script
   reported success:
   - the GitHub release is Latest with all four assets;
   - the OneDrive installer checksum matches the built one;
   - the signature is Valid and timestamped as `CN=FlossworksCross-Stitch`;
   - the local install is `2.0.1+c694b523`.
3. **#954's reporter was answered** with a signed routine reply once the release was live.

- Dranak (Claude Code)

## 2026-09-30 - /who sets the equipped classes and the level (DRA-633)

David asked in session for `/who` to set the character's classes and level every time he types it. He can still override them on the Character room. He ruled that the `/who` level is the LOWEST of the equipped classes. The log agrees: on Sep 11 all three of WAR/DRU/MNK read 50, and the 23-27 dings that followed were a different equipped set.

Decisions I made, and the default each one could have gone the other way on:

1. **The statement and `/who` are ordered by TIME, not by rank.** Whichever is fresher wins, with an exact tie going to the statement. This is the same rule `CharacterLevel` already uses for dings against statements.
   - Default against: "/who always wins", which would make a Character-room pick last only until the next `/who` in the replayed log.
   - Result: a pick lasts until the next `/who` he actually types.
2. **A class statement stored before this change has no stamp, so it counts as the oldest claim.** The first `/who` replaces it. That matches "/who should set it every time".
   - Default against: treating old statements as "now", which would have made them outrank every `/who` until he re-picked.
3. **Per-class levels from `/who`.** Every equipped class below N (or unknown) rises to N. A class above N is left alone, because the row only says "at least N" about it. Only when every class stands above N does the lowest one come down to N.
   - Default against: writing N onto every class, which would have lowered his real 60s.
4. **Other players' rows are parsed only to be dropped.** `WhoTracker` keeps nothing but the watched character's own row: the values line. `/anon` rows parse to nothing.
   - Review fix (DRA-645): `LogWatcher` hands every listing row, `/anon` and title rows included, to `WhoTracker` ONLY, and the history import skips them. Before, each row also reached the session journal and the raw-line ring, and counted as your play time. So a text watch rule can no longer match a `/who` row.
   - Default against: letting text rules see rows, which would let a rule watch for another player's name in the listing.
5. **Version bumped to 2.0.2** in `Directory.Build.props` for the What's-new entry, which `whatsnew-guard` requires. This is not a release; the release go stays David's.

- Dranak (Claude Code)

## 2026-10-01 - DRA-128 graduation: `ssc-retirement` ADAPT, `whole-sequence-auth` HOLD (DRA-671)

T2 ruling: Planner recommendation `e6169a75` on DRA-549, ACCEPTED by Dranak 2026-10-01 (comment `609a0b92`), recorded on DRA-128. Doctrine home: ops `EXO-PLAYBOOK.md` **entry 7** and the *Experiments in flight* table. Ruling ledger: `HANDOFF.md`.

**`ssc-retirement` - GRADUATE, verdict ADAPT.** The evidence is the dashboard rows, not this entry: [`docs/ops/exo-dashboard.md`](docs/ops/exo-dashboard.md) **Reading 2**, the `ssc-retirement` table in R2.0.1 and its row set in R2.0.2 (sensitivity), window PRs #619-#643, landed by DRA-118 as PR #644, read against the frozen `docs/ops/exo-baseline.json`.

1. **Caveat 3.** Most of the PRs-per-slice drop is mechanical: the `helm/ssc-N` twin was retired by construction (see the R2.0.1 "governance-only PR share" row). The term that was actually judged is the "Helm touches per delivery slice" row.
2. **Caveat 4.** The rework row's 0 of 23 bounds the true rate below about 4%; it does not prove 0. Adopters keep counting veto and rework after they adopt.
3. **The missed touches target is named, and is not a hold.** The touches row misses the frozen <0.3 target. Retiring the carrier PR changed the vehicle of a ruling, not how often rulings happen; per-slice ruling frequency is `whole-sequence-auth`'s claim, not this experiment's.
4. **Why ADAPT and not ADOPT.** An adopting project must already have a committed, auditable ruling ledger (a `HELM.md` equivalent with SIGN-as-commit). Retiring carrier PRs without one retires the audit trail too.
   - Default against: ADOPT, which would let a project with no ledger delete the only place its rulings were recorded.

**`whole-sequence-auth` - HOLD, stays in flight, no verdict.** Lifting condition (verbatim): 1. DRA-134 (Sr Executor) completes: standing Scribe triage sweep live and DefectConventionStart set. Instrument half is already merged (DRA-135). 2. The first window whose last merge is at least 14 days (DRA-133 lag floor) past the convention start gets a reading where the escaped-defect row prints a rate or a named honest status other than NoConvention. 3. A fresh T2 ruling on that reading judges GWR/ACCR net of the measured term, and may re-set the <0.15 / >70% targets if they were joint M0-bundle aspirations.

The `exo-experiment:` tag lines above are left as they are. `seat-mutex` kept its tag after it graduated (DRA-111), and `Get-Experiments` reads them.

- Sr Executor (Claude Code), DRA-671

## 2026-10-01 - v2.0.2 released, under a pre-given go and a Reviewer PASS

1. **The go came before the review.** David, 2026-09-30, in session: "ship it when the review
   passes". The review was Reviewer's (DRA-660), not Fable's or Helm's. David: "Everything is
   going through paperclip so the role helm was doing is still being done." He waived his own
   smoke test for this release: "These are small changes so I'm okay skipping smoke test."
   - Default against: gates green, then Fable reviews, then David says ship.
2. **Scope:** #981 (#679 Reward Chest loot), #982 (/who sets classes and level), #983 (#966
   Guide window), #984 (#942 bar grows left), and DRA-42 D1-D3 (#985, #987, #988). **#710 was
   dropped** at David's call, once Jr found the shrouds are detrimental spells and the Watch
   list is beneficial-only by design; it moves to 2.0.3 on DRA-638.
3. **Released 2026-10-01 ~06:02 CT**, tag `v2.0.2` at `f9e266a6`. Reviewer passed `3a9e33a7`;
   the two later merges are DRA-642 (a build-script fix) and DRA-671 (docs), and neither is
   player-facing. CI is green on the tag. Verified after the script reported success:
   - the GitHub release is Latest, with all four assets;
   - the OneDrive installer sha256 matches the build;
   - the signature is Valid and timestamped as `CN=FlossworksCross-Stitch`;
   - the local install is `2.0.2+f9e266a6`.
4. **Why it took a settings change.** In this session the auto-mode classifier refused
   `release.ps1` as a production deploy, and refused adding its own allow rule as
   self-modification. David added `Bash(pwsh -NoProfile -File scripts/release.ps1:*)` to
   `.claude/settings.local.json`, and said that running release scripts is not a
   founder-level activity in the ExO. Making release execution a seat is DRA-675 (Planner).
5. **Replies posted** on #966, #679 and #942, signed, after the release, as David asked.

- Dranak (Claude Code)

## 2026-10-01 - The release go is a decision, not a keystroke; execution is a seat (DRA-675 D1)

**Authority:** the Founder on DRA-675, 2026-10-01: *"make sure future releases aren't bound by me needing to execute command level scripts. These are not founder level activities in our ExO."* Plan `docs/plans/DRA-675.md` (Challenger walk DRA-676, PROCEED-WITH (C1); its conditions 1-5 bind).

**What changed.** CLAUDE.md consequence item 2 still makes the release go the Founder's and the one hard gate. The go now names the version and the reviewed commit, may be conditional on that review, and is recorded on the release card. EXECUTION is the Sr Executor release seat's (`docs/ops/release-seat.md`, runbook row **Release**). The Founder is never asked to run the script. A seat assignment or a Reviewer PASS is not a go. "Hold releases" gains the clause that such a go on the card is explicit. `scripts/release-verify.ps1` turns "it shipped" into rows (tag, release, OneDrive, sha256, signature), and a failed `release.ps1` is a hard stop that the seat never retries.

- Default against: keep the go per-release and typed by the Founder at release time, with the Founder running `release.ps1` himself. That is the path that stalled v2.0.2 until he typed an allow rule by hand.

**Measured, not assumed (plan §2):** the Sr seat runs `claude_local`/acp with `--setting-sources=project,local`, and Paperclip's acpx client answers its prompts in `approve-all`. The `-EvolvedLocal -Tag x` probe went through with no rule, and per Challenger condition 1 that clears nothing on its own. No settings file is both Sr-only and standing. The allow-rule text and its per-card target are in `release-seat.md`, and it was written into no settings file. Whether plan §4's Founder paste card is filed is Planner's call.

- Sr Executor (Claude Code), DRA-678

## 2026-10-01 - Releases sign as a service principal; az login is the fallback (DRA-679 D1)

**Authority:** the Founder on DRA-677: *"automatic signing login, please."* Plan `docs/plans/DRA-679.md`, signed by Helm on PR #997. The Challenger walk DRA-680 returned PROCEED-WITH (C2), and its conditions C-1..C-4 and kill criteria K1..K3 were checked before `signing.ps1` changed. The evidence is on DRA-695 and in the PR body.

1. **The certificate lasts 12 months, not 6.** Each rotation needs the Founder's `az` session, and a shorter cycle buys little when the key cannot be copied off the PC.
   - Default against: 6 months.
2. **The identity was created by script (`signing-identity.ps1 -Create`), not in the portal.** The Founder's ruling authorized creating the login, and doing it by script is how it stops being his keystroke.
   - Default against: the plan's §8 portal walk-through. That stays the fallback if a call is ever refused.
3. **`Az.Accounts` 5.5.3 is restored into the gitignored `tools\psmodules`, not installed into the user profile.** Controlled Folder Access refuses writes to `Documents\PowerShell\Modules` (measured), and `tools\` is already where the pinned dlib is restored.
   - Default against: `Install-Module -Scope CurrentUser`.
4. **The key is TPM-held** (`Microsoft Platform Crypto Provider`; ExportPolicy `None`, measured). It cannot be copied even by an administrator.

- Sr Executor (Claude Code), DRA-695

## 2026-10-02 - Achievements engine, Exploration first: the plan's calls, logged at D1 (DRA-754)

**Authority:** plan `docs/plans/DRA-754.md`, Helm SIGN on PR #1020 (DRA-756). D1 is DRA-772. The plan says its §2-§4 calls are logged here at D1, as the reporting duty.

1. **R1 = (b): keep every place, rank in-band places first, put the band sentence on the row; `LevelUseFor(Achievements)` = `Consumes`.** Helm's SIGN did not rule R1, and the plan's own "Unsigned -> (b)" default applies. An unknown band or level changes nothing (trap 73). This lands in D2.
   - Default against: (a), refuse a place whose band bottom is 5+ over the character. That would hide the place a player chasing an Explorer is asking about.
2. **The unit is the distinct PLACE, not the row.** 186 rows are 95 places in both committed dumps. This retires DRA-749's 207 / 146 / 71%.
   - Default against: rank rows, which double-counts every place an Explorer and its own Traveler achievement both name.
3. **No alias table.** The 4 world places that do not resolve (Dragoncrypt, Freeport Sewers, Shadowrest, The Caverns of Exile) are not another zone's spelling. They are REPORTED by name. An alias table arrives with its first evidenced row (Hunter's plan, or a real Exploration miss).
4. **Matching is exact, then `ZoneMapFiles.IdentityKey`, and nothing looser.** `DropZones` is admitted only through `TradeskillMaterials.IsPlace`. The route is resolved separately over `ZoneGraph`'s own nodes and never through `ZoneGraph.Resolve`'s containment. A key two graph nodes share answers no route.
   - **Measured correction to the plan's §4:** `ZoneGraph.Resolve("Freeport Sewers")` is null as spelled, and so is `ZoneGraph.Resolve("The Commonlands")`. The containment trap is real one step later: the RESOLVED name `Commonlands` comes back as West Commonlands, and a containment matcher in the universe resolves Freeport Sewers to `Freeport`. The negatives pin those facts instead.
5. **The dump is the authority on completion, and nothing is stored.** A place is complete only when every row naming it says so. Rows that disagree are counted, reported and left open (0 of 95 in both dumps). A `You have entered` line is evidence and never a tick (D3).
6. **Player-instanced places are a curated prefix list** (House, Guild Hall, Guild Lobby, Wedding Chapel), matched as a whole leading word. Each prefix's evidence is a dump row in `tests/fixtures/achievements/averaj.txt`. They are counted in one line and never ranked.
7. **`UnlockSource.Exploration` is built on first READ after a refresh, not inside `Refresh`.** It is the same `_achievements` (one producer). Its universe gunzips the item catalog, and the tick that notices a new dump should not pay for that.
   - Default against: build it in `Refresh` beside `Races`/`Classes`, as the plan's wording has it.
8. **Out of scope, as the plan says:** Hunter, Slayer, era gating (`ZoneEras` has no reader and WorldEra starts ABSENT) and any write to the dump's state.

- Sr Executor (Claude Code), DRA-772

## 2026-10-04 - Support EQBuddy tip link is PayPal.Me, and it stays (DRA-994)

**Chosen:** the topbar chip labeled Support EQBuddy opens `https://www.paypal.me/DavidEdwards08` in a new tab (`rel` contains `noopener`). This URL is permanent unless a better option arrives. Ko-fi case 245917 is the example named on the card. A swap happens only then, and only if David approves it. The Stripe Payment Link stays gone. The page shows no email address.

**Default it could have gone the other way on:** treat PayPal.Me as a temporary stand-in and swap the href back to `ko-fi.com/eqbuddy` the moment case 245917 resolves, with no further approval.

**Why this way:** Founder, 2026-10-04 3:12 PM CT, on DRA-994. The 3:10 PM decision restored the chip; the 3:12 PM wording says the link is permanent, not temporary. This supersedes the 2026-09-23 Ko-fi entry for the live href.

- Cursor Executor, DRA-994
