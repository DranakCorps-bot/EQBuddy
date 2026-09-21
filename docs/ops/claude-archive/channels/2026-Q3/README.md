# Channel archive — 2026-Q3

**Immutable. Nothing here is live, and nothing here is a work queue.**

Rotated out of the active channel files in five passes: **2026-09-14** by DRA-75
(M0-2) under the DRA-73 plan rev 2 approved by David on 2026-09-14,
**2026-09-17** by DRA-165 (DRA-144 F1) under DRA-26 plan §5 as David SIGNed it
2026-09-17T01:06:45Z, and **2026-09-18** by DRA-154 (`EXO-CHANNEL-ROTATE`) under the
Helm AUTHORIZE of that evening, and **2026-09-20** by DRA-229 (DRA-144 F4b) and
DRA-231 (DRA-144 F6), both under DRA-26 plan rev 3 section 5.
`exo-experiment: channel-rotation`. Tier T1.
Pass 2 appended into the existing `FABLE-FEEDBACK.md` archive and pass 3 into the
existing `HELM-FEEDBACK.md` one; neither moved anything an earlier pass had written,
and passes 4 and 5 wrote new `SCRIBE.md` and `DECISIONS.md` archives that no earlier
pass had opened.

Holds live in `HELM.md` and **only Helm lifts one**. An archived line never
revives a hold and never commissions work. If you are looking for something to
do, the inboxes are `SCRIBE.md`, `BEVEL.md` and `FABLE.md` — not this directory.

**Do not append here.** Append to the active file at the repo root.

## What was rotated

| Archive | Entries | Bytes | Active file: before → after | Cutoff |
|---|---:|---:|---|---|
| [`HELM-FEEDBACK.md`](HELM-FEEDBACK.md) | 390 | 1,914,317 | 5.0 MB → 117 KB (24 entries) | everything below the readable 2026-09-09+ set |
| [`FABLE-FEEDBACK.md`](FABLE-FEEDBACK.md) | 139 | 1,006,791 | 1.1 MB → 174 KB (35 entries) | before 2026-09-08 |
| [`FABLE-FEEDBACK.md`](FABLE-FEEDBACK.md) *(pass 2)* | 49 | 226,720 | 265 KB → 39 KB (10 entries) | before 2026-09-15 |
| [`HELM.md`](HELM.md) *(pass 3)* | 144 tips + 134 sign-offs | 1,033,986 | 1,047,518 B → 25,411 B (1 tip) | before 2026-09-18 |
| [`HELM-FEEDBACK.md`](HELM-FEEDBACK.md) *(pass 3)* | 81 | 342,571 | 344,449 B → 4,730 B (1 entry) | before 2026-09-18 |
| [`SCRIBE.md`](SCRIBE.md) *(pass 4)* | 47 | 99,145 | 213,675 B → 114,715 B (45 entries) | **triage by `Priority`, not a date** |
| [`DECISIONS.md`](DECISIONS.md) *(pass 5)* | 143 | 595,476 | 676,431 B → 85,238 B (18 blocks) | before 2026-09-17, **plus a hand-triaged floor** |

No pending ask is archived in any pass — pass 2's 18 candidate ask/hold
markers were each dispositioned before the move and all 18 were already discharged,
so nothing needed re-pinning, and pass 3 did the same for 43.
**Pass 5 is the first pass that had to re-pin, and the marker sweep is not what caught
it.** All five 2026-09-14 blocks carrying an `exo-experiment:` tag are correctly archived
as history, but the tag itself is read out of the *live* file by `Get-Experiments` in
`scripts/exo-metrics.ps1` and by two tests, so archiving all six tags broke the ExO
dashboard's regenerability. Both channel guards were green; CI tests caught it. The six
tag lines and their judging clauses are re-pinned verbatim into a dated `STANDING` block
in the live file (3,440 B), and the full entries stay here. **The lesson for pass 6 and
later: the never-rotate floor is a keyword set, so it cannot see content whose liveness is
carried by a consumer rather than by a marker. Grep `scripts/` and `tests/` for the
ledger's filename before cutting it.**
**Pass 5 is also the first pass whose triage changed the cut.** `DECISIONS.md` had 30 blocks
older than the 2026-09-17 date cut carrying a `LIVE ASK` / `PARK` / `HOLD` / `STANDING`
marker. Each was dispositioned by hand: 24 moved — the marker was product vocabulary
(a parked HUD chip row, a seat-mutex holder), a counterfactual ("could have gone the
other way: LIVE ASK first"), a narrative reference to an ask whose residence is another
file and which is now discharged, or a rule that has since graduated to a live document
— and **6 were kept in the live file at any age** because they carry a park with an
unmet reopen condition, an unfixed defect named for veto, a rule whose only residence
is `DECISIONS.md` itself, or a relay another card is still blocked on.
**Pass 4 could not use a cutoff at all.** `SCRIBE.md` is an inbox, not a dated ledger:
its entries carry a `Priority` field, 42 of the 92 were still open by that field, and the
oldest of those (`waiting`, 2026-08-19) sit further down than entries already discharged.
A date cut would have archived live `must-fix` player-facing defects, so pass 4 partitioned
per entry by Priority instead: `someday` / `taken` / `done` and the terminal dispositions
moved, and **zero** entries marked `must-fix`, `approved`, `authorized`, `authorized-next`,
`open` or `waiting` were archived - a count measured over the archive file, not a claim.  The live
`HELM-FEEDBACK.md` asks are still in the active file; the **PR #606** DRA-71 D9 ask
that was unsigned when pass 1 ran has since been SIGNED and moved in pass 3.

### Pass 3 rotated the state file itself, which the first two could not

Passes 1 and 2 moved feedback ledgers. Pass 3 is the first rotation of the **`HELM.md`
class** — the file that holds the holds — and that is why its cut is drawn where it is.
**A hold, an open ask and a standing rule do not rotate at any age**, so the live file
keeps its Holds block (empty, which is a live fact and not an omission), its Wakes and
Claude-kick block, its five retired-hold lines, its item shape and its "What Helm does
NOT decide" section, whatever their date. What moved was rulings, plus a pile of 122
`### PR #…` sign-off entries that had accumulated *underneath* the retired-hold lines
and were never holds themselves.

Two live rulings sat below the calendar cutoff, so the live file carries a pointer block
naming them and where to read them in full rather than relying on a reader's memory:
the **PR #685** whole-sequence SIGN (DRA-180/181) and the **PR #684** Jr/Sr router SIGN
(DRA-179). A rotation that silently moves the authorization the top of the file rests on
is technically a move and practically a loss.

Pass 3 also discharged both grandfather rows in `scripts/channel-size-baseline.psd1`
in the same pull request, which is what `scripts/channel-size-guard.ps1` check C requires
and the only way a row ever leaves that table. It used explicit line ranges and a
reconstruction assertion rather than `scripts/channel-rotate.py`, because Helm had
refused the stock `rotate --apply` for these two files on 2026-09-17.

### Four files, and which one to read

- **`HELM.md`** — every Helm ruling before 2026-09-18, in two blocks.
- **`HELM-FEEDBACK.md`** — the readable recovery. **This is the one you want.**
- **`HELM-FEEDBACK.original-flattened.md`** — the verbatim 4.93 MB that was
  removed: two ~2.4 MB lines of cp437 mojibake. Unreadable on purpose. It is
  here so "nothing was lost" can be checked **against the bytes** instead of
  taken on trust, and so `channel-wipe-guard`'s ARCHIVE exemption can see the
  entries that moved. Do not try to read it; do not edit it.
- **`FABLE-FEEDBACK.md`** — a straight date rotation of an uncorrupted file, so
  it needs no counterpart.

### None of the three gets repaired — including the mojibake (ruled 2026-09-17)

All three files carry mojibake: ~57k cp437 occurrences in the flattened one, 5,429 cp1252 in
`HELM-FEEDBACK.md`, 1,527 in `FABLE-FEEDBACK.md`. They are **OUT of scope for repair, permanently** —
ruled on DRA-160 and recorded with its byte evidence in `DECISIONS.md` (DRA-161). The "do not edit it"
above was written for the flattened file alone, and that gap is exactly what let the card be raised.
One reason each, and they are three different reasons:

- **`HELM-FEEDBACK.original-flattened.md`** — corrupt on purpose: it is the checkability exhibit
  described above and the only clean-checkout fixture the cp437 detector has.
- **`HELM-FEEDBACK.md`** — from offset 5,808 to end it is the `f4af3b5f` blob carried verbatim, and all
  5,429 of its markers are inside that region, so repairing them breaks both the verbatim claim below
  and what `channel-rotate.py verify` asserts — while git keeps the same markers in that blob anyway.
- **`FABLE-FEEDBACK.md`** — its marker count is identical to the pre-rotation blob's. Rotation moved
  those bytes; it did not create them.

**The rule behind all three:** a rotated archive copy is not an independent site of corruption. Rotation
is a one-way move of bytes already immutable in git, so repairing the copy removes nothing from the
record — it only makes the archive diverge from the revisions it was cut from. Repair pays on the live
files at the repo root, and that work is done (DRA-55, DRA-119). It never pays here.

The DRA-55 Helm LOCK — *"Soft LEAVE inventing archive repair without separate Helm ruling."* — **stays
live.** The ruling above declines to seek repair; it does not lift the LOCK. Wanting these files
repaired later still needs a real, separate Helm ruling.

### A transcript is not a map, and the doc sweep had to learn the difference

`DocumentationTests.EveryFileTheDocsPointAtExists` sweeps every `.md` under
`docs/ops` and fails any backticked path that no longer resolves. It reddened on
all three archived ledgers the moment they landed — they name a retired
release-review script, a scratch path under /tmp, and a gitignored seat-claims
file, all of which were true on the day an agent typed them. (Those names are
deliberately **not** backticked in this paragraph: this README *is* swept, so
quoting a dead path here would redden the very test being described.)

**Both of that test's remedies — "fix the doc" and "restore the file" — are
unavailable here.** The file is immutable by construction, and the paths are
history rather than error. So the ledgers are exempt and **this README is not**:
it is the one file in this directory a reader navigates by, so a dead pointer in
it is the ordinary defect that test exists to catch. The exemption is paired with
`DocumentationTests.OnlyTheRotatedChannelTranscriptsAreExemptFromTheLivePathSweep`,
which fails if the exemption ever stops matching anything (trap 78), if the
README slips out of the sweep, or if any other `docs/ops` doc slips out with the
transcripts (trap 34). Prove-failed by disabling the predicate: 4 red, 21 green.

**A future rotation inherits this for free** — the exemption is keyed on the
`channels/` directory, not on these three filenames.

## What was NOT rotated, and why

**Amended 2026-09-18 (pass 3): `HELM.md` has now rotated, and not on the >30-day arm.**
The paragraph below is left as written because it was true and correctly reasoned on
2026-09-14; what it could not see is that the **size** arm is the one that fires first on
this file. `HELM.md` reached 1,047,518 B — 16x the 64 KiB policy — while still holding
nothing over 30 days old, because it takes ~13 KB per append. A calendar rule alone would
never have moved it. `FABLE.md` and `DECISIONS.md` stay unrotated: `DECISIONS.md` is at
675,660 B against a 676,484 B grandfather cap, which is 824 bytes of band and its own next
pass, and appending a pointer into 824 bytes is not a margin.

`HELM.md`, `FABLE.md` and `DECISIONS.md` are named in DRA-75 for a **>30-day**
rotation. On 2026-09-14 they hold **nothing older than 30 days** — the oldest
dated entry anywhere in the channel set is **2026-08-21**, 24 days old. The rule
is real and stays; it simply moved zero bytes this pass. The evidence:

```bash
python scripts/channel-rotate.py report HELM.md FABLE.md DECISIONS.md --cutoff 2026-08-15
```

`BEVEL-FEEDBACK.md` (515 KB) was outside DRA-75's scope and is the obvious next
candidate — a 2026-09-08 cutoff would move 92 entries / 464 KB. That call is
Bevel's; it is asked in `BEVEL-FEEDBACK.md` and has not been run.

## HELM-FEEDBACK.md is recovered text, not the bytes that were on disk

This is the part worth reading before trusting the file.

Commit `c7a597a8` appended to `HELM-FEEDBACK.md` in a way that **flattened the
entire 12,254-line file onto a single line** and re-encoded it through cp437
(trap 60c). A later append did the same thing again. The result was a 5.0 MB
file whose two largest lines were ~2.4 MB each and held **two mojibake copies of
one history**.

The readable history survived in git at `f4af3b5f` (1,909,798 bytes, 12,254
lines) and is what this archive carries, verbatim.

`scripts/probe-uncovered.py` is the proof that dropping the corrupt bytes loses
nothing. It peels the cp437 layers and shows:

- the second flattened line, peeled, **is** the `f4af3b5f` blob, whole — a pure
  duplicate;
- the first, peeled, is `[the PR #564 entry] + [f4af3b5f minus its first entry]`.

So the only content unique to 4.93 MB of flattened bytes was the 4,519-byte
**PR #564** entry. It is recovered, de-mojibaked, and sits at the top of the
archive. Its words are intact; **its line breaks are not recoverable and are
gone**, so it reads as one long line. The corrupt bytes are kept verbatim in
`HELM-FEEDBACK.original-flattened.md` beside this file — so the claim above is
checkable against them, not just against git `1e0f7232`.

**The corruption is not finished.** Entries written since still carry fresh
mojibake (`╬ô├ç├╢` and `ΓÇö` both appear in 2026-09-12/13 entries). This
rotation cleaned up the accumulated damage; it did not fix whatever keeps
producing it. That is a live problem, not an archived one.

## Two things that cost time here

1. **An additions-only diff passes on a silently flattened append.** That is how
   trap 60c ran twice without anyone noticing — the bytes genuinely were only
   added. The tell was never the diff; it was the **line count** going
   12,254 → 2. Nothing watches that.
2. **Never locate the flattened lines by index.** They were at 0-based 70/72
   against one ref and 788/790 against the next one an hour later, because
   channel entries prepend. Both scripts find them by size (`>= 100 KB`).

## Why this lives under `claude-archive/`

DRA-75's issue named `docs/ops/archive/2026-Q3/`. It is here instead, and the
reason is worth recording: `scripts/channel-wipe-guard.ps1` — the guard built
after a channel ledger was destroyed three times in six days — reads its ARCHIVE
exemption out of `docs/ops/claude-archive`, and it deliberately has **no
`-Force` and no skip switch**. Putting the archive anywhere else meant either
failing the guard or editing it as part of the very change it exists to refuse
(trap 52). The path was the cheap thing to move; the guard was not.

## Verifying a rotation

`core.autocrlf=true` in this repo, so `git show ref:path` returns the
LF-normalized blob while the working tree holds CRLF. **Verifying a rotation
against a git blob is a false failure** — it produced one during DRA-75, on a
rotation that was in fact byte-perfect. Take a byte copy of each file before
rotating and verify against that:

```bash
mkdir -p /tmp/pristine && cp HELM-FEEDBACK.md FABLE-FEEDBACK.md /tmp/pristine/
python scripts/channel-rotate.py rotate --apply
python scripts/channel-rotate.py verify --pristine /tmp/pristine
```

`verify` asserts that every original entry block survives byte-exact across
archive + active (a superset is allowed, so later appends do not redden it),
that the archive contains the `f4af3b5f` blob verbatim, and that the recovered
#564 entry is clean UTF-8 rather than mojibake.
