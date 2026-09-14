# Channel archive — 2026-Q3

**Immutable. Nothing here is live, and nothing here is a work queue.**

Rotated out of the active channel files on **2026-09-14** by DRA-75 (M0-2),
under the DRA-73 plan rev 2 approved by David on 2026-09-14.
`exo-experiment: channel-rotation`. Tier T1.

Holds live in `HELM.md` and **only Helm lifts one**. An archived line never
revives a hold and never commissions work. If you are looking for something to
do, the inboxes are `SCRIBE.md`, `BEVEL.md` and `FABLE.md` — not this directory.

**Do not append here.** Append to the active file at the repo root.

## What was rotated

| Archive | Entries | Bytes | Active file: before → after | Cutoff |
|---|---:|---:|---|---|
| [`HELM-FEEDBACK.md`](HELM-FEEDBACK.md) | 390 | 1,914,317 | 5.0 MB → 117 KB (24 entries) | everything below the readable 2026-09-09+ set |
| [`FABLE-FEEDBACK.md`](FABLE-FEEDBACK.md) | 139 | 1,006,791 | 1.1 MB → 174 KB (35 entries) | before 2026-09-08 |

No pending ask was archived. The live `HELM-FEEDBACK.md` asks — including the
unsigned **PR #606** DRA-71 D9 ask — are still in the active file.

### Three files, and which one to read

- **`HELM-FEEDBACK.md`** — the readable recovery. **This is the one you want.**
- **`HELM-FEEDBACK.original-flattened.md`** — the verbatim 4.93 MB that was
  removed: two ~2.4 MB lines of cp437 mojibake. Unreadable on purpose. It is
  here so "nothing was lost" can be checked **against the bytes** instead of
  taken on trust, and so `channel-wipe-guard`'s ARCHIVE exemption can see the
  entries that moved. Do not try to read it; do not edit it.
- **`FABLE-FEEDBACK.md`** — a straight date rotation of an uncorrupted file, so
  it needs no counterpart.

## What was NOT rotated, and why

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
