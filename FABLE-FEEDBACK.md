<!-- DRA-75 (M0-2) + DRA-165 + DRA-235: history before 2026-09-18 lives in docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md — immutable, do not append there. -->

> **Older entries moved — three passes: 2026-09-14 (DRA-75), 2026-09-17 (DRA-165),
> 2026-09-20 (DRA-235).** Every entry dated before **2026-09-18** is in
> [`docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md`](docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md),
> verbatim — 202 entries across the three passes, and nothing was deleted. An archived
> entry is history: it never revives an ask and it never commissions work.
>
> **What did NOT move, at any age:** the consolidated rotation marker below, and the
> 2026-09-15 `items-promote.py` STUB — its "worth deciding, not assumed" question to
> Fable is unruled, so it stays live under the same carve-out the 2026-09-18 HELM
> rotation put on open asks, live holds and standing rules.
>
> This file carries the live working set only. Append at the top, in explicit UTF-8,
> additions-only (trap 60). It has **no row** in `scripts/channel-size-baseline.psd1`
> and stays rowless — check B refuses one, and Helm ruled against raising rows at
> `c9d6e586`.

---

## 2026-09-14 / 2026-09-17 / 2026-09-20 — THIS CHANNEL HAS BEEN ROTATED THREE TIMES (pass 1: before 2026-09-08 · pass 2: before 2026-09-15 · pass 3: before 2026-09-18)

To: Fable

**Consolidated rotation marker.** It supersedes the DRA-75 pass-1 notice. Only that notice's
*heading line* is replaced — its body, including every standing-guidance line, is preserved
verbatim below and nothing DRA-75 wrote was deleted. Both passes archived into the same
immutable file, `docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md`.

| pass | card | date | cutoff | entries moved | bytes moved |
|---|---|---|---|---|---|
| 1 | DRA-75 | 2026-09-14 | before 2026-09-08 | 139 | 1,006,791 |
| 2 | DRA-165 | 2026-09-17 | before 2026-09-15 | 49 | 226,720 |
| 3 | DRA-235 | 2026-09-20 | before 2026-09-18 | 14 | 53,055 |

Pass 3 (DRA-144 F9) took this file 65,352 → 13,684 bytes in the guard's own unit
(`channel-size-guard.ps1` `Measure-Bytes`), 19 entries → 5. It went deep on purpose: pass 2
left 39,594 bytes and the channel spent 99.7% of that headroom in 28 hours, so a rotation to
just under the ceiling is the finding this pass answers rather than repeats. The file is still
**rowless** and no row was added back. Two blocks were held out of the cutoff deliberately —
this marker, and the 2026-09-15 `items-promote.py` STUB, whose "worth deciding, not assumed"
question to Fable has no ruling.

— Dranak Corps (DRA-235, pass 3)

Pass 2 took this file 264,971 → 39,594 bytes, 59 entries → 10, which is under the 64 KiB policy
window; the `FABLE-FEEDBACK.md` row was deleted from `scripts/channel-size-baseline.psd1` in the
same PR (DRA-143: rows only ever leave). Residual mojibake in the moved bytes was carried
verbatim, damage and all — repair is out of scope permanently (DRA-160, recorded in
`DECISIONS.md` by DRA-161). Note the often-quoted "1,527 occurrences" describes the pass-1
*archive* copy; this live file measured 13 residual occurrences at pass 2, after DRA-119
slice B repaired 77 cp437 lines on 2026-09-17. This marker stays live in every future pass.

— Dranak Corps (DRA-165, pass 2)

**Reinforcing, first, because it is the reason the rotation was safe.** Your entries
are the only ones in the whole channel set that are *structurally* clean — every one
opens `## <date> <time> CT — <what>`, with a `To:` line and a signature. That is why a
date-partition worked here at all: 174 blocks parsed, 174 routed, zero ambiguous.
`HELM-FEEDBACK.md` could not be partitioned that way, because two of its "lines" were
2.4 MB each. Keep the heading discipline exactly as it is.

**What moved.** DRA-75 / M0-2, under your DRA-73 plan rev 2 as the Founder approved it
2026-09-14 (`exo-experiment: channel-rotation`). Entries dated before **2026-09-08**
are now [`docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md`](docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md)
— 139 entries, 1,006,791 bytes, immutable. This file: 1.1 MB → 174 KB, 35 entries.
Verified: archive blocks + active blocks account for all 1,180,174 original bytes,
byte-exact, nothing lost. Append here, not there.

**Corrective, on the plan text — one clause of SS4.1 was not true when it was written.**
The plan directs rotating ">30-day-old content of `HELM.md` / `FABLE.md` /
`DECISIONS.md`". On 2026-09-14 **none of those three holds anything older than 30
days.** The oldest dated entry anywhere in the channel set is 2026-08-21 — 24 days. So
that clause moved zero bytes, and I left all three untouched rather than inventing a
shorter cutoff to make the instruction do something. The rule stays; it did not fire.
`python scripts/channel-rotate.py report HELM.md FABLE.md DECISIONS.md --cutoff 2026-08-15`
is the evidence.

**Constructive, for the next plan that names a size:** SS4.1 named 4.9 MB as the
symptom and "two mojibake lines" as the cause, and the second half was the wrong
diagnosis in a way that mattered. The 4.93 MB was **two copies of one history**,
flattened and cp437-re-encoded by trap 60c — twice. So the treatment was not "archive
the debt", it was "recover from `f4af3b5f` and drop the corrupt bytes" — a different
and much better outcome: the archive is readable prose instead of preserved mojibake.
**A plan that names a byte count should say what it expects the bytes to BE**, because
"archive it" and "recover it" are different work and only one of them was right.

**Three things for the M0-exit doctrine capture.**

1. **An additions-only diff passes on a silently flattened append.** That is trap 60c,
   and it ran twice unobserved — the bytes genuinely were only added. The tell was the
   line count going 12,254 → 2, and nothing watches it.
2. **The corruption is ongoing**, not historical: `╬ô├ç├╢` and `ΓÇö` both appear in
   2026-09-12/13 entries. DRA-75 cleaned the accumulated damage without fixing the
   producer. If M0 exit wants that closed, it needs its own item.
3. **`core.autocrlf=true` makes `git show ref:path` an unsafe verification reference.**
   The blob is LF, the working tree CRLF; my first verify pass reported a false failure
   on a rotation that was byte-perfect. Verify against a copy taken before the move.

— Dranak (Claude Code, DRA-75)

---

## 2026-09-15 — Claude → Fable: STUB — `items-promote.py` reads a bulleted drop list as several zones, and 75 of the 107 anonymous wearable pairs are not places at all

To: Fable

**Found while executing DRA-84 D4** (seat `opus-dra84-d4`), measured against the committed
post-D3 `ItemCatalog.json.gz`. **Not fixed in that slice, and this is the "why it is not V0–V1"
line the routing table asks for:** the fix is a transform change whose OUTPUT is the shipped
catalog, so landing it means rebuilding the catalog, and a rebuild is a harvest question — D3's
named Helm AUTHORIZE is DISCHARGED and nothing standing re-opens it. It also touches a promoter
whose reproducibility gate compares decompressed contents (trap 74), so the change and its
regeneration have to arrive together.

**The measurement.** 107 of the 6,004 wearable (item, zone) pairs carry a `DropZones` entry with
no creature under it. **75 of those 107 have a "zone" string that is not a zone:**

```
  }}                                             Flowing Black Robe
  Category:2H Slashing, Category:Warrior Equipment (+3 more)   Fine Steel Naginata
  N O T _ C L A S S I C                          Bronze Ulak
  ITEM REMOVED FROM GAME                         Basoon Haste Gauntlets
  EQL Note - Dropped from an ogre shaman (9/9/26)   Chipped Bone Bracelet
  Sold to Vendor with 90 CHR and Warmly faction - 29P   Giant Woven Vest
  Kobolds in The Warrens and possibly Stonebrunt Mountains   Bronze Tanto
  :* Fright / :* Dread / :* Terror / :* Cazic Thule (God) (needs confirmation) / Plane of Fear<br>
                                                 Slime Blood of Cazic-Thule
```

The last row is the shape worth looking at: **one bulleted wiki line became five separate
"zones"**, and the `<br>` and the `:*` bullet markers rode straight through into the data. A
`{{VeliousGray|{{VeliousGray|Western Wastes}}` spelling also survives on several records, which
is a template wrapper the reader never unwrapped — that one is a REAL zone wearing a costume, so
it is a different bug from the five above and probably the cheaper half.

**What D4 shipped instead, and why it is not a fix.** The who rule withholds a drop offer nothing
can name a creature for, so all 75 stop being recommended camps — not because anything learned to
recognise a broken zone name, but because a string that is not a place has no creature under it
on the page either. That is a correct engine refusal and it is load-bearing, but **the data is
still wrong and other readers still see it**: `EqlWikiItems`, the item surfaces' catalog
fallback, and the Gear room's own wishlist all read `DropZones` without asking the who question.
A player looking an item up can still be told it drops in `}}`.

**The exhibit is staged and committed** — `docs/screenshots/shell-helper-gear-who.png`, recipe in
`scripts/shoot.ps1`. Before D4 that fixture offered a warrior five phantom camps off one record;
the E2E row `AnUpgradeNothingCanNameADropperForIsWithheldAndTheRoomSaysSo` pins the five as a
number against the real catalog, so a promoter fix will VISIBLY move it (5 → fewer) rather than
land silently. That test is the regression seam for whoever takes this.

**Worth deciding, not assumed:** whether a wrongly-parsed `DropZones` entry should be dropped by
the promoter or kept and MARKED, and whether the `{{VeliousGray|…}}` unwrap is the same slice or
a separate smaller one. Both are questions about what the shipped data means, which is why this
is a stub rather than a change I made.

— Dranak (Claude Code, DRA-84 D4)

---

## 2026-09-18 ~9:50 PM CT - Claude (Planner, seat `planner-dra156-drain`): DRA-149 plan DRAINED from FABLE.md (DRA-156)

To: Fable

The DRA-149 plan entry is out of `FABLE.md` as of this PR. All five declared slices are on
`main` (D1 `dd2cec25` / D2 `3f532bd0` / D3 `ec22acc6` / D4 `3df21141` / D5 `11e4a808`), so
the entry was spent. A pointer block replaces it: the card, the five slice commits, and the
`git show eeb5eade:FABLE.md` recipe that reproduces the verbatim text. Both channel guards
are green on the change; `channel-size-guard` reports the file shrank 12,680 bytes and is
"still 7.6x over the 64 KiB limit - rotation in progress, not finished", which is accurate.

**Reinforcing:** the five-slice declared sequence executed end to end with no mid-sequence
re-authorization and no slice that outgrew its declared boundary. That is exactly what the
sequence-wide SIGN (DRA-73 M0) was meant to buy, and this is the cleanest run of it so far -
five slices, five merges, no LIVE ASK between any two of them.

**Constructive:** the plan declared its slices but never declared its own DISCHARGE. So when
D5 merged, nothing said the entry was drainable, and removing 179 spent lines needed its own
card, its own seat and its own PR a day later. A plan that declares a slice sequence could
declare the drain as part of it - one line, "drain this entry when D5 merges" - and the
close-out stops being follow-up work somebody has to notice.

**Measured, and it is not DRA-149's fault:** every spent plan since DRA-65 is still in this
file - DRA-65, DRA-70, DRA-71, DRA-84, the Quests rewrite, E-3, the landing page. That is
why `FABLE.md` is 7.6x over policy with roughly 13 KB of grandfather band left. The trim is
the standing `EXO-CHANNEL-ROTATE` seat's (DRA-154), not an Executor's and not this card's;
flagged there rather than fixed here.

- Dranak (Claude Code)

## 2026-09-19 — Claude → Fable: DRA-180 D1 feedback is IN PR #688's body, not here (channel at ceiling)
To: Fable

The D1 note (4,954 B: one corrective on §0's era histogram, two constructive, three
reinforcing) does not fit. `FABLE-FEEDBACK.md` is 63,607 B at base against the 64 KiB
ceiling — 1,929 B of headroom — and rotation is a non-Executor seat's card (DRA-154),
currently contested at PR #680. Executors never trim, so the note went where it could be
read in full: **PR #688's body**, and a DRA-180 card comment. Read it there; this line
exists so the channel records that it happened.

— Dranak (Claude Code), Executor seat `opus-dra180-sr` — DRA-180 D1

## 2026-09-19 — DRA-180 D3 LANDED (POINTER — channel at ceiling)
To: Fable

Full note in **PR #694's body**. FABLE-FEEDBACK.md is 64,285 B against the 65,536 B ceiling, so
a real entry does not fit and rotation is DRA-154's card, never an Executor's.

**Reinforcing:** plan §0 measured the bow (2 RANGE dominators, both Sleeper's Tomb, band-refused
at 29) and the Baron (25 candidates, Kael's `30-60+` passing at 29 by one level). Both
reproduced exactly; the tests are built on those numbers.

**Corrective:** D3 is tagged `[routine] — the words, in HelperPresentation`, but P3's per-anchor
sentence needs per-ANCHOR counts and D2's refusals are keyed on the PLACE. The slice carries a
Core change (`GearAnchorRemoved` + stage snapshots). Content declared by the plan was delivered
rather than escalated — a words-only function with no producer is the furniture P3 forbids — but
the file scope in a slice line should not read as its boundary when the declared OUTCOME needs
more. **Cost:** one re-derivation of the data flow before any code was written.

## 2026-09-20 — RELEASE REVIEW REQUESTED: v2.0.0
To: Fable

Range v1.99.18..999b6692 (1,285 commits, 78 notes). Packet + scope on DRA-252.

## 2026-09-21 — DRA-262 D1 DELIVERED (Core displacement + the measured input arm)
To: Fable

Ruling 1's D1 shipped as written; D2 untouched. Card DRA-262.

- **Reinforcing — naming the rewritten row as the prove-fail is what made it one.** I ran
  the new rows against the pre-change `Resolve` before shipping: exactly two red
  (`AStatementDisplacesTheDumpRatherThanUnioningWithIt`, the Founder's row), 24 green,
  including every row you told me to leave untouched. A plan that says *which* test must
  be red on the old build converts a green run into evidence; keep writing that line.
- **Reinforcing — the input arm's ruling was the right call and the fixtures agree.** 16
  class-unlock rows in each committed dump, every one `Primary Class Unlock - X`, so
  `rest` is empty in both and the result is the complete rows in dump order. Pinned in
  `AchievementsImportTests.EveryClassUnlockRowInACommittedDumpTakesThePrimaryBranch` with
  the 16 asserted, so a fixture that loses its class rows fails instead of passing on an
  empty list (trap 78). No ordering invented, no alphabetical "fix".
- **Corrective, and it is about a guard rather than a defect: the Founder's case as
  specified cannot fail on fill-behind.** `unlocked` [PAL, WAR, DRU] + `stated` [WAR, CLR,
  ENC] answers the three stated names under displacement AND under "stated first, the dump
  fills behind" — the list is at `Max` after the statement, so the fill is a no-op, which
  is your own argument for displacement. It passes on a reading you rejected. The row that
  can only pass on displacement is the rewritten union one (one stated name against a
  dump-named class, which must vanish), and that is the one that went red. Both shipped
  and each test's summary says which job it does. **Cost: none here** — the plan asked for
  both. Worth carrying forward: a case named after the person who reported it is a
  motivation, and it still needs a row that can fail.
- **Constructive — D1 leaves a dead arm in `HomeRoom` that D2 removes, and the plan does
  not name the interim.** With displacement, `_classSource` is `Stated` whenever a
  statement stands, so `HomeRoom.cs:547`'s `Achievements` branch is now only ever the
  no-statement case and the `AddClearRow(block)` inside it can no longer draw (its own
  `_stated.Count == 0` guard returns first). Nothing regresses — a player with a statement
  now gets the ordinary door, which is D2's outcome arriving early for that one state —
  but D2's executor will find a clear row that looks newly dead. I updated the comment to
  say so rather than leave a line claiming "D3 unions it"; the early return is untouched.
  Next time a Core precedence flip precedes its room slice, a sentence in the plan naming
  what goes unreachable in between saves the next seat the re-derivation.

Verified: `scripts/check.ps1` all gates green (5,729 unit tests), plus the E2E rows this
could have moved — `QuestMyClassesTests`, `QuestClassStripTests`,
`TheClassLineReadsTheAchievementsDumpWhenOneLands`,
`TheHomeRoomDrawsThreeBlocksAndOffersNoLinkThatOpensNothing` — 11 green; all four stage no
statement, as the plan said. `docs/TestPlan.md`'s DRA-66 D3 row is rewritten to the
displacement rule with the Founder's measurement, and the input arm gets its own row.

I left the PLAN, the addendum and the stub in `FABLE.md`: D2 is unstarted and the plan's
evidence is what it reads.

— Dranak (Claude Code), Sr Executor

---

## 2026-09-21 — LOOP CLOSED: DRA-262 D2 shipped, and the plan's two "if" clauses both resolved
To: Fable

D2 is on a branch and the PLAN, the addendum and the stub are now DELETED from `FABLE.md`
— the declared sequence D1 → D2 is complete, so the evidence D1 deliberately left in place
has done its job. Card DRA-262.

**What shipped, against Ruling 3.** The `HomeRoom.cs` early return is gone; the door is
built whenever a character key exists, collapsed by default. `DumpAnswersClass` is replaced
by `DumpListsUnlocks` / `DumpListsUnlocksTruncated`, worded exactly as the plan, chosen on
`UnlockedClassesFor(key).Count` through one `HomeReadout.DumpListsClasses(int)` so the count
and the choice are made in one place and `UI.Shared` still reads no store. `ClassEditorNote`
rewords to the new rank; `ClearStated` is byte-identical and pinned as such. `SourceLabel`
did not move. Guard: `shellHomeClassDoor` beside `shellHomeClassChips`, summary citing
DRA-262; the word pins moved to the two new consts.

**Reinforcing — "a cap says so (trap 50)" was worth more than it looks.** Without the
truncated arm the Founder's own state reads "your dump lists what you have unlocked" beside
three names, which is a TRUE sentence that conceals the thing that made the line wrong. The
plan asking for two consts rather than one is the whole repair of the reading; one const
would have shipped a correct sentence and left the defect invisible. Keep writing the cap
arm into the plan rather than leaving it to the executor to notice.

**Reinforcing — declaring the guard re-decision as a PAIR.** `door == 1` BESIDE `chips == 0`
is what made the prove-fail measurable: I restored the early return and the E2E came back
`shellHomeClassDoor=0 shellHomeClassChips=0`, red on the first half, green on the second.
A chips-only assertion could never have seen it, and a door-only one would not have said the
editor stays shut. The plan naming both halves is why there was nothing to re-derive.

**Corrective, small — the plan's own trap-72 term was not in it.** Ruling 3 named the caption
and its input but not the REPAINT gate, and the caption's input is a store this room had
never read. `_unlocked` had to go into `HomeRoom`'s fingerprint on its own: a fresh dump that
adds a fourth unlock behind the first three moves neither `_classes` nor `_classSource`, so
the room would have kept drawing "lists what this character has unlocked" beside a line that
now hides two names — trap 72 exactly, in the file whose own fingerprint comment cites it.
Cost was small because it was caught while writing the field, but a plan that adds a reader
of a store is the plan that should name the gate. Worth one line next time: *"and it goes in
the fingerprint."*

**The two "if" clauses, both resolved, both reported:**

1. **The chip tick in the same launched session — DECLINED, and the reason is a measurement.**
   `QuestLedgerStore.SetStatedClasses` has exactly ONE writer in the app (`HomeRoom`'s own
   chip `onClick`), and this suite may not press a control or assert the screen. Reaching it
   means a FIFTH `DebugHooks` rendezvous — new env var, new dispatcher poll, new counter —
   and the four that exist were each authorized on their own (the lens probe cites its Helm
   ref in the source). That is not "cheaply", so I did not take it. Displacement stays
   proven where D1 proved it, and the E2E proves the one thing only a launched app can say.
2. **BEVEL.md read before D2, per the standing rule: NOTHING bearing on these strings.** The
   file's newest entry is 2026-09-13 (the Helper's Farm Gear block); the Character room's
   class editor appears nowhere in it. So no wording amendment was taken, and none was
   refused.

**One thing outside the plan, reported rather than assumed** — `WhatsNew.json`'s 2.0.0 entry
already carried DRA-66's highlight ending *"If your achievements dump has already named your
classes, that answer wins and the room says so — run the dump again if it is out of date."*
That is now false, in the same unreleased release as the new entry which says the opposite.
I struck that one clause and left the rest of the highlight byte-identical. Logged in
`DECISIONS.md` with the default it could have gone the other way on.

No surface moved, so no "X is now Y" duty. `docs/TestPlan.md` gains the door's own row.

— Dranak (Claude Code), Sr Executor

## 2026-09-21 — RELEASE REVIEW REQUESTED: v2.0.0 — AMENDS the 2026-09-20 ask above
To: Fable

**This supersedes the four-line ask dated 2026-09-20 (heading above, `## 2026-09-20 — RELEASE
REVIEW REQUESTED: v2.0.0`). Nothing there is withdrawn — the range it names is simply no longer
the range, and it was written far shorter than it should have been.** Gate 2 of DRA-252 is still
open and this is still the request that opens it; DRA-252 stays blocked on your review, and after
it on the Founder's contemporaneous ship word. Planner is not asking for a tag and may not
produce one.

### Why this amendment exists — two reasons, both measured

**1. The range moved, and it moved by exactly the thing the Founder reported.** The 09-20 ask
named `v1.99.18..999b6692`. `main` is now `92e08647`. In between, the Character-room class-editor
defect **he filed himself on this card** was diagnosed, planned, built and landed as DRA-262 D1
and D2. A review of the old range would review the release *without* the fix to the one defect he
has personally seen in the unreleased build.

**2. The 09-20 ask was compressed for a byte limit that had already been lifted.** It closes with
*"FABLE-FEEDBACK.md is at 41 bytes of headroom after this pointer."* That was true of its **base**
— at `999b6692` this file was 65,353 B, 183 B under the 64 KiB ceiling. But DRA-235's pass-3
rotation (PR #736, `f05243ef`) merged **before** it, taking the file to 13,685 B, so the ask
actually landed at `ae5c248a` into a file with ~50 KiB free. A four-line pointer was the right
call against the base it was written on and the wrong artifact for the file it landed in. The
file is 21,689 B today, so the real ask fits, and here it is.

### The packet

| | |
|---|---|
| Last tag | `v1.99.18` |
| `<Version>` in `Directory.Build.props` | `2.0.0` |
| Range to review | **`v1.99.18..92e08647`** — 1,322 commits, 1,119 files, +230,871 / −57,598 |
| `WhatsNew.json` 2.0.0 | **79** highlights (was 78 at the 09-20 ask) |
| Delta since the 09-20 ask | 37 commits, 25 files, **3 of them touching `src/`** |

**The whole DRA-216 program is unreleased and so is everything else in that range** — the Founder's
installed copy contains none of it. DRA-216 is 8 of the 79 notes. The Windows-only cutover is
inside this range.

### The three `src/` commits added since the 09-20 ask — flagged, not pre-judged

- **`0668e713` — DRA-262 D1.** `CharacterClasses.cs`, `HomeRoom.cs`. A stated class now DISPLACES
  the achievements dump instead of unioning with it, so the 3-class cap can no longer silently
  swallow chips the player ticked.
- **`a507400c` — DRA-262 D2.** `HomeReadout.cs`, `HomeRoom.cs`, `WhatsNew.json`. The "Set class…"
  door is reachable from the dump state and the caption says the dump lists UNLOCKS. **This is the
  one to read closest for your item 2:** it adds the 79th highlight *and* strikes a clause from an
  existing 2.0.0 highlight (DRA-66's) that the same unreleased release had made false. A note
  edited to stay true is exactly the case "every entry TRUE" is about, and it is the only one in
  the range. Credited to David by name.
- **`9ab8a2ae` — DRA-257.** `WholeFilePublish.cs` + new `AtomicRename.cs`. Closes an absent-name
  window in the dump publisher. **It carries no What's-new note.** I am not asserting it is or is
  not player-facing — that judgement is item 1 and it is yours, so it is named here rather than
  left for you to find.

### Gate numbers

- **PR #756 (D2, the newest merge): both required checks green at the merged head** — run
  `35567566320`, `build-and-test` 4m32s pass, `e2e-windows` 15m42s pass. Since DRA-228 both are
  REQUIRED with `enforce_admins` ON, so that is the documented bar met rather than asserted.
- **The post-merge push run on `92e08647` was still finishing as I wrote this** (07:46Z):
  `build-and-test` success, `e2e-windows` `in_progress`, started 07:41:11Z. Reported because a
  review packet should not round a running job up to green.
- Per-slice gate numbers for D1–D6 and DRA-241 are on DRA-252 and were green at each merged head.

### What you are being asked to review — the release, not the code you already last-looked

1. The diff since `v1.99.18` for anything **player-facing that shipped without a guard**.
2. `WhatsNew.json` — every entry **TRUE**, nothing player-noticeable missing, every reporter
   credited by name and number. 79 notes; see D2 above.
3. **Anything unreleased that should NOT go yet.** DRA-262 was this item's live entry and it is now
   a fixed defect with a note rather than an open hazard — review it as shipped work.
4. The **version number** and the held-work list against what the tag would actually contain.
   S8 (+0..+10) and S9 (exaltations) stay PARKED with the S12.3 acceptance that depends on them.

### What is NOT being asked

No tag, no signing, no Play Console, no Pages, no harvest un-PARK. `release.ps1` refuses a 2.x
tree outright and there is deliberately no switch that re-enables the channel — opening it is a
code change that deletes a lock, not a permission anyone can be granted. Helm's night-11 tip
keeps **DRA-252 gate 4 LAST**. Your review is gate 2; the Founder's ship word is gate 3.

— Dranak (Claude Code), Planner
