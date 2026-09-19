#!/usr/bin/env python3
"""DRA-75 / M0-2 — archive oversized channel files, with a 30-day rotation rule.

Governing plan: DRA-73 plan document rev 2 (approved by David 2026-09-14).
exo-experiment: channel-rotation. Tier T1.

This is a SANCTIONED RESTRUCTURE under an approved plan, not a routine append
(trap 60). It MOVES bytes; it never rewrites them. `verify` re-derives the
original from archive + active and asserts equality block-for-block.

Two files are rotated:

  HELM-FEEDBACK.md   4.9 MB. Commit c7a597a8 flattened the whole 12,254-line
                     file onto ONE line and re-encoded it through cp437
                     (trap 60c), then a later append did it again -- so the
                     4.93 MB is TWO mojibake copies of one history. That
                     history is still readable in git at f4af3b5f. The
                     archive therefore carries the RECOVERED READABLE text,
                     not the corrupt bytes. Proven by scripts/probe-uncovered.py:
                     the only content unique to the flattened bytes is the
                     4,519-byte PR #564 entry, which is recovered and kept.

  FABLE-FEEDBACK.md  1.1 MB, uncorrupted. Straight date rotation.

HELM.md / FABLE.md / DECISIONS.md are IN SCOPE but hold nothing older than
30 days (oldest content anywhere is 2026-08-21), so they are not touched.
`report` is the evidence for that.

DRA-207 generalised the partitioner and the driver so the tool can target the
remaining ledgers. Three limits were removed; nothing above changed.

  * The entry unit is a parameter (`--unit`, default 2). DRA-75 hardcoded
    level 2. SCRIBE.md keeps its entries at level 3 -- under level 2 the tool
    saw 2 blocks in a 200 KB file and reported nothing to rotate.

  * The date source is a parameter (`--date-from`, default `heading`). None of
    SCRIBE.md's 89 level-3 headings carry a date, so `heading` leaves every
    entry undated; `body` falls back to the first date in the block. It never
    overrides a heading date.

  * `rotate` takes files, a `--card` and a `--date`, on the same partition path
    `report` uses, and writes. With no file argument it still runs the frozen
    DRA-75 pair: `rotate_helm` and `rotate_fable` are untouched, because they
    encode two incident-specific recoveries that must not be generalised away.
    Their output is byte-for-byte what main's script produces; that is a
    done-bar condition, proved by running both scripts on the same input.

  * The archive banner's Helm-holds sentence is a parameter (`--holds`, OFF by
    default). DRA-75 hardcoded it because both files it rotated were
    Helm-adjacent feedback channels. It is true of HELM.md and false of
    SCRIBE.md, DECISIONS.md and BEVEL.md -- stamping it into their archives
    asserts a hold mechanic they do not have, which is the hardcoded-card-id
    defect one field over.

Two rules the general path enforces that DRA-75 did not need:

  * Undated entries are KEPT LIVE. An entry with no date has not been shown to
    be old. (SCRIBE.md: 23 entries, 25,799 B.)

  * Re-rotating a file OVERWRITES its earlier archive with a smaller one and
    still reports success. `rotate` refuses when the archive already exists or
    the file already carries a rotation pointer, unless `--force`.

Byte conservation (preamble + archived + kept == input) is asserted before any
write. This tool MOVES bytes; it never rewrites them.

DRA-211 made the SECOND rotation of a file safe, which is what DRA-154's weekly
cadence produces by construction. DRA-207 left two things contained rather than
fixed, and they were related:

  * A rotation marker is a dated entry and archived ITSELF. `FABLE-FEEDBACK.md`
    opens with `## 2026-09-14 / 2026-09-17 - THIS CHANNEL HAS BEEN ROTATED
    TWICE ...`, whose own body says "This marker stays live in every future
    pass"; `BEVEL-FEEDBACK.md` line 1 is the same shape. `block_date` reads
    2026-09-14 off both, so any later cutoff moved the file's own rotation
    record into the archive. Markers are now HELD LIVE regardless of date --
    a marker is metadata ABOUT the file, not an entry of its history.

  * `--force` was a one-way door: the archive write was `write_bytes(header +
    moved)`, which REPLACES. Every archive write now goes through
    `write_archive`, which refuses any proposal that is not a byte-prefix
    extension of what is already on disk. That refusal is UNCONDITIONAL --
    `--force` cannot override it, because the whole point is that no flag
    should be able to drop archived history.

  The append question is answered APPEND, not a new dated file per pass. That
  is what DRA-165's hand-carried second FABLE pass already did (both passes are
  in the one immutable `2026-Q3/FABLE-FEEDBACK.md`, and the live marker table
  records them that way), and it keeps true the one path the live pointer
  names. A second pass writes a `## ` PASS MARKER into the archive ahead of its
  entries -- a heading of its own on purpose, because an unheaded separator
  would be swallowed by the last archived entry, and `verify` compares entries
  byte-for-byte, so it would report real history as LOST.

  The append machinery (`write_archive`, the prefix refusal, the pass marker)
  is DRA-175's, written on PR #696 against pre-DRA-207 main and never merged;
  DRA-211 carries it onto the generalised `rotate_file` path. #696 additionally
  holds the `rotate_helm` zero-flattened-lines SKIP and the nothing-to-move
  guard for the frozen pair, which are still its to land.

DRA-175 / PR #696 lands that remainder on the FROZEN pair, and only there --
the 2026-09-17 ruling names an append-safe FABLE-only patch. Three arms, each
one measured against main at 6dc9b3b8:

  * `rotate_helm` SKIPS on zero flattened lines. DRA-75 removed both, so the
    `assert len(idx) == 2` fires and kills the WHOLE `rotate` command before the
    FABLE half runs -- `rotate --cutoff 2026-09-08` and `--cutoff 2026-09-15`
    both die there, rc=1, so even the dry run cannot be read. That is how the
    FABLE half stayed unmeasured. It never re-rotates HELM-FEEDBACK.md; that
    room is DRA-154's.

  * `rotate_fable` APPENDS on a second pass, through DRA-211's `write_archive`
    and `PASS_MARKER`. Without it the frozen path can only ever propose
    `header + moved`, which `write_archive` correctly REFUSES -- safe, but it
    makes a second FABLE rotation impossible rather than append-only, and the
    weekly DRA-154 cadence produces second passes by construction.

  * Nothing to move writes NOTHING. `write_archive` cannot catch this one: a
    header-only proposal against an empty archive is a legal first write, and
    against a populated one the refusal fires but the run exits 2 on a tree
    whose correct answer is "no work". That is the tree at the default cutoff.

A second pass does NOT write a second pointer. The active file's pointer block
is hand-written prose (DRA-165 consolidated two passes into one table);
regenerating it would be an Executor trimming channel content (DRA-26 rev 3
section 5), so the tool REPORTS the cumulative numbers an appended pass makes
stale instead of inventing a third pointer that contradicts the other two.

Pick the unit and the date source per file from `report`; do not guess them.
`report --show-blocks` lists every entry with its resolved date and its
archive/keep/hold disposition -- read that back before rotating anything.
`selftest` proves the DRA-211 arms and proves the refusal FIRES (trap 78).
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import re
import subprocess
import sys
import tempfile
from pathlib import Path

# `report --show-blocks` prints real channel headings, which carry em-dashes and
# arrows. Windows hands this process a cp1252 stdout, so the pre-flight the
# module docstring tells you to read back crashed with UnicodeEncodeError on the
# first heading containing U+2192 -- on FABLE-FEEDBACK.md, today. A diagnostic
# must never be the thing that fails.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):  # pragma: no cover - redirected streams
        pass

REPO = Path(__file__).resolve().parent.parent
ARCHIVE_DIR = "docs/ops/claude-archive/channels/2026-Q3"

# HELM-FEEDBACK.md forensics
HF_LAST_GOOD = "f4af3b5f"
# The two flattened lines are found by SIZE, never by index: every channel
# append re-numbers them (they were at 70/72 against one ref and 788/790
# against the next one an hour later -- trap 60a, re-read at splice time).
HF_FLAT_MIN = 100_000

# The DRA-75 stamp. Frozen: these two archives are already on main and must
# keep reproducing byte-for-byte. Every other rotation supplies its own.
DRA75_CARD = "DRA-75 (M0-2)"
DRA75_DATE = "2026-09-14"

# The card stamped into a pass marker written by the FROZEN pair's append path.
# The frozen path takes no `--card` (DRA-207 added that to the general path
# only), so this names the mechanism rather than pretending to a caller id
# nobody supplied.
DRA175_CARD = "DRA-175"

HEADING = re.compile(rb"(?m)^## .*$")
DATE = re.compile(rb"(20\d\d)-(\d\d)-(\d\d)")

# The entry unit, per file. DRA-75 hardcoded level 2 because both files it
# rotated keep entries at level 2. Three of the five ledgers left do too, but
# SCRIBE.md keeps its entries at level 3 with only two level-2 headings in the
# whole file -- so a level-2 partition sees 2 blocks in a 200 KB file and finds
# nothing to rotate. Pick this per file from `report`; never guess it.
DEFAULT_UNIT = 2


def heading_re(unit: int) -> re.Pattern:
    return re.compile(rb"(?m)^" + b"#" * unit + rb" .*$")


# ---------------------------------------------------------------- helpers


def split_blocks(data: bytes, unit: int = DEFAULT_UNIT):
    starts = [m.start() for m in heading_re(unit).finditer(data)]
    if not starts:
        return data, []
    preamble = data[: starts[0]]
    bounds = starts + [len(data)]
    blocks = []
    for i in range(len(starts)):
        block = data[bounds[i] : bounds[i + 1]]
        blocks.append((block.split(b"\n", 1)[0], block))
    return preamble, blocks


def block_date(heading: bytes):
    m = DATE.search(heading[:80])
    return tuple(int(g) for g in m.groups()) if m else None


def entry_date(heading: bytes, block: bytes, source: str = "heading"):
    """Resolve an entry's date.

    `heading` (the DRA-75 behaviour, and the default) reads the date out of the
    heading line only. `body` falls back to the first date anywhere in the block
    when the heading has none -- it never overrides a heading date, so it can
    only ever date an entry that `heading` left undated.

    SCRIBE.md needs `body`: none of its 89 level-3 headings carry a date, so
    under `heading` every entry is undated and a date cut archives nothing.
    66 of the 89 carry a date in the body. `body` is opt-in because the first
    date in a body is not guaranteed to be the entry's own date -- use
    `report --show-blocks` to read the resolved dates back before rotating.
    """
    d = block_date(heading)
    if d is None and source == "body":
        m = DATE.search(block)
        if m:
            return tuple(int(g) for g in m.groups())
    return d


def dated_of(blocks, source: str = "heading"):
    return [(entry_date(h, b, source), h, b) for h, b in blocks]


# A rotation marker is a dated entry, so a date partition archives the file's
# own rotation record and the live file loses it. It is metadata ABOUT the file,
# not an entry of its history, so it is HELD regardless of date.
#
# Calibrated against every level-2 and level-3 heading in all nine ledgers (618
# h2 headings): this matches 2, and they are exactly the two live markers --
#   FABLE-FEEDBACK.md  "## 2026-09-14 / 2026-09-17 - THIS CHANNEL HAS BEEN
#                       ROTATED TWICE (pass 1: ... pass 2: ...)"
#   BEVEL-FEEDBACK.md  "## 2026-09-14 - WHERE THE HISTORY WENT: two channel
#                       files rotated, and yours is the next candidate ..."
# Eight other headings contain the substring "rotat" (HELM.md x4,
# HELM-FEEDBACK.md x2, DECISIONS.md x2) and none of them match -- they are
# rulings and decisions ABOUT rotation, which are ordinary history and must
# still age out. That margin is why this is phrase-anchored rather than a
# `rotat` substring test.
#
# Prose matching is a guess, so it is never silent: every held entry is printed
# by `report` and by `rotate`, with its heading, before anything is written.
# `--hold` adds a substring (SCRIBE-FEEDBACK.md's re-pinned standing rules were
# held by hand this way) and `--no-marker-hold` turns the default off.
HOLD_MARKER_RE = re.compile(
    rb"(?i)\b(this channel (has been|was) rotated"
    rb"|where the history went"
    rb"|(channel|history) rotated"
    rb"|rotation (pass|marker|pointer))\b"
)


def is_held(heading: bytes, holds: tuple = (), marker_hold: bool = True) -> str:
    """Return the reason this entry is held live, or "" if it is not.

    The reason is returned rather than a bool so the operator reads WHY a
    dated entry stayed behind, not just that one did.
    """
    if marker_hold and HOLD_MARKER_RE.search(heading[:200]):
        return "rotation marker"
    for h in holds:
        if h.lower().encode("utf-8") in heading.lower():
            return f"--hold {h!r}"
    return ""


class ArchiveReplace(Exception):
    """A rotation tried to REPLACE archived bytes instead of adding to them."""


def _common_prefix(a: bytes, b: bytes) -> bytes:
    n = 0
    for x, y in zip(a, b):
        if x != y:
            break
        n += 1
    return a[:n]


def write_archive(path: Path, proposed: bytes, apply: bool) -> bytes:
    """Write an archive, refusing anything that is not a pure APPEND.

    DRA-175 / PR #696. The caller composes the whole proposed file; this asks
    one question of it -- does it still START with every byte already on disk?
    That is what makes the refusal reachable: the DRA-75 composition
    (`header + moved`) fails it, and `selftest` runs exactly that composition to
    watch it fire. A size check alone would pass a same-length rewrite.

    There is deliberately no force parameter. `--force` exists to let an
    operator past the already-rotated tells; it must not be able to drop
    archived history, which is the whole defect DRA-211 was opened on.
    """
    existing = path.read_bytes() if path.exists() else b""
    if existing and not proposed.startswith(existing):
        keep = len(_common_prefix(existing, proposed))
        raise ArchiveReplace(
            f"{path.name}: REFUSED — this is a replace, not an append. "
            f"{len(existing):,}B are archived; the proposal agrees with the "
            f"first {keep:,}B and then diverges, dropping {len(existing)-keep:,}B "
            f"of archived history. Q3 archives are immutable (Helm, 2026-09-17); "
            f"no flag overrides this."
        )
    if apply:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(proposed)
    return existing


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()[:16]


def git_blob(ref: str, path: str) -> bytes:
    return subprocess.run(
        ["git", "show", f"{ref}:{path}"], cwd=REPO, capture_output=True
    ).stdout


def peel(data: bytes, rounds: int = 6) -> bytes:
    """Undo cp437 mojibake layers (utf8 -> cp437-decode -> utf8-encode)."""
    best, cur = data, data
    for _ in range(rounds):
        try:
            nxt = cur.decode("utf-8").encode("cp437")
        except (UnicodeDecodeError, UnicodeEncodeError):
            break
        try:
            nxt.decode("utf-8")
            best = nxt
        except UnicodeDecodeError:
            pass
        cur = nxt
    return best


def sig(b: bytes) -> bytes:
    """Whitespace/encoding-immune signature, for comparing flattened text."""
    return re.sub(rb"\s+", b" ", re.sub(rb"[^\x20-\x7e]+", b" ", b)).strip()


def flat_line_indices(data: bytes):
    """0-based indices of the flattened >100 KB lines, found by size."""
    return [i for i, l in enumerate(data.split(b"\n")) if len(l) >= HF_FLAT_MIN]


# ---------------------------------------------------------------- text


ARCHIVE_HEADER = """# ARCHIVE — {name} (through {through})

{provenance}
`exo-experiment: channel-rotation`.

Nothing in this file is a work queue and nothing here is live.{holds}
Do not append here; append to the active `{name}`.

{note}
- Entries archived: **{count}**
- Bytes: **{size:,}**
- Active file after rotation: `{name}`

---

"""

PROVENANCE = "**Immutable.** Rotated out of the active `{name}` on {date} by {card}."

# The holds sentence is HELM-specific and is NOT generic provenance. DRA-75
# wrote it into both its archives because both were Helm-adjacent feedback
# channels. Stamped into a SCRIBE.md or DECISIONS.md archive it asserts a
# hold mechanic that file does not have -- the same defect as a hardcoded card
# id, in a different field. Frozen for the DRA-75 pair, empty by default, and
# passable per rotation for a file that genuinely carries Helm holds.
DRA75_HOLDS = (
    " Holds live in\n"
    "`HELM.md` and only Helm lifts one — an archived line never revives a hold."
)

# Frozen verbatim, including the line wrap that falls mid-card-id. The two
# DRA-75 archives on main were written with exactly these bytes; reproducing
# them is a done-bar condition, so this literal is not re-derived from the
# generic template. New rotations use PROVENANCE above and pass their own card.
DRA75_PROVENANCE = (
    "**Immutable.** Rotated out of the active `{name}` on 2026-09-14 by DRA-75\n"
    "(M0-2), under the DRA-73 plan rev 2 approved by David on 2026-09-14."
)

POINTER = """<!-- {card}: history before {through} lives in {archive} — immutable, do not append there. -->

> **History rotated {date}.** Entries before {through} moved to
> [`{archive}`]({archive_rel}) ({count} entries, {size:,} bytes).
> This file carries the live working set only. Append at the top, in explicit
> UTF-8, additions-only (trap 60).

---

"""


def archive_header(name, through, note, count, size, *, card, date,
                   provenance=None, holds=""):
    """Archive banner. `card` and `date` are the CALLING card and the REAL
    rotation date -- DRA-75 hardcoded its own, which would stamp the wrong
    provenance into every later archive. `holds` is the same problem one field
    over: it defaults to empty, because the Helm-holds sentence is true of the
    two DRA-75 channels and of nothing else."""
    prov = (provenance or PROVENANCE).format(name=name, date=date, card=card)
    return ARCHIVE_HEADER.format(
        name=name, through=through, provenance=prov, holds=holds, note=note,
        count=count, size=size
    ).encode("utf-8")


def pointer_text(through, archive, count, size, *, card, date):
    return POINTER.format(
        card=card, date=date, through=through, archive=archive,
        archive_rel=archive, count=count, size=size,
    ).encode("utf-8")


# A LATER pass's marker, written into the ARCHIVE ahead of that pass's entries.
# It is a heading block of its own on purpose: anything without a heading would
# be swallowed by the last already-archived entry, changing that entry's bytes
# -- and `verify` compares entries byte-for-byte, so a decorative separator
# would report real history as LOST. DRA-175 / PR #696, with the card and date
# parameterised the way DRA-207 parameterised the header and the pointer.
#
# `{hashes}` is the rotation's own --unit, not a hardcoded `##`. A level-2
# marker in a level-3 file (SCRIBE.md) is not a block at that unit: it would be
# swallowed by the preceding entry, which is the exact failure the heading is
# there to avoid, one unit over.
PASS_MARKER = """{hashes} {date} — ROTATION PASS APPENDED ({card}; entries dated before {through})

Everything BELOW this line was moved out of the active `{name}` by
`scripts/channel-rotate.py rotate --apply` in a later pass than the one this
file's header above describes. Nothing above this line was touched: the write is
a byte-exact append, and `write_archive` refuses any proposal that is not a
prefix extension of what was already on disk.

- Entries appended in this pass: **{count}**
- Bytes appended: **{size:,}**  (sha {sha})

---

"""

HF_NOTE = """**This archive is RECOVERED TEXT, not the bytes that were in the file.**
Commit `c7a597a8` flattened the entire 12,254-line `HELM-FEEDBACK.md` onto a
single line and re-encoded it through cp437 (trap 60c); a later append did it
again. The result was 4.93 MB holding **two mojibake copies of one history**.

The readable history survived in git at `f4af3b5f` and is reproduced here
verbatim. `scripts/probe-uncovered.py` proves the only content unique to the
flattened bytes was the 4,519-byte **PR #564** entry, which is recovered
(de-mojibaked) and placed at the top of this file. The corrupt bytes are kept
verbatim in `HELM-FEEDBACK.original-flattened.md` beside this file, so the claim
above is checkable against them.
"""


# ---------------------------------------------------------------- commands


def fmt_date(d) -> str:
    return "%04d-%02d-%02d" % d if d else "(undated)"


def cmd_report(args):
    cutoff = tuple(int(x) for x in args.cutoff.split("-"))
    holds = tuple(getattr(args, "hold", ()) or ())
    marker_hold = not getattr(args, "no_marker_hold", False)
    for name in args.files:
        data = (REPO / name).read_bytes()
        preamble, blocks = split_blocks(data, args.unit)
        dated = dated_of(blocks, args.date_from)
        reasons = [is_held(h, holds, marker_hold) for _, h, _ in dated]
        held = [x for x, r in zip(dated, reasons) if r]
        old = [x for x, r in zip(dated, reasons)
               if not r and x[0] is not None and x[0] < cutoff]
        undated = [x for x in dated if x[0] is None]
        dates = sorted(d for d, _, _ in dated if d)
        print(f"{name}")
        print(f"  bytes={len(data):,}  unit=h{args.unit}  date-from={args.date_from}  "
              f"blocks={len(blocks)}  preamble={len(preamble):,}B")

        # The unit census, so the operator PICKS the unit instead of guessing.
        census = {u: len(heading_re(u).findall(data)) for u in (2, 3)}
        print(f"  headings: h2={census[2]} h3={census[3]}")
        if census[args.unit] == 0:
            print(f"  *** no level-{args.unit} headings -- wrong unit for this file")
        # A heading of the OTHER level inside a block is a sub-section being
        # swallowed (fine, it travels with its entry) or an entry being missed
        # (not fine). Surfaced either way; the operator decides which it is.
        other = 3 if args.unit == 2 else 2
        swallowed = sum(len(heading_re(other).findall(b)) for _, _, b in dated)
        if swallowed:
            print(f"  h{other} headings carried INSIDE h{args.unit} blocks: {swallowed}"
                  f"  (sub-sections travel with their entry; confirm they are not entries)")

        if dates:
            print(f"  dates {fmt_date(dates[0])} .. {fmt_date(dates[-1])}")
        print(f"  older than {args.cutoff}: {len(old)} blocks / "
              f"{sum(len(b) for _, _, b in old):,}B")
        print(f"  undated (never archived by a date cut, kept live): "
              f"{len(undated)} blocks / {sum(len(b) for _, _, b in undated):,}B")
        # Held entries are ALWAYS named, never just counted: the hold is decided
        # by matching prose, and a silent prose match is a guess nobody audited.
        # Under --show-blocks the listing below already names them with their
        # reason, and printing them twice under a count of 1 reads as two holds.
        print(f"  held live regardless of date: {len(held)} blocks / "
              f"{sum(len(b) for _, _, b in held):,}B")
        if not args.show_blocks:
            for (d, h, b), r in ((x, y) for x, y in zip(dated, reasons) if y):
                print(f"    HOLD ({r}) {fmt_date(d)} {len(b):,}B  "
                      f"{h.rstrip(chr(13).encode()).decode('utf-8', 'replace')[:88]}")

        if args.show_blocks:
            for (d, h, b), r in zip(dated, reasons):
                if r:
                    mark = f"HOLD ({r})"
                elif d is not None and d < cutoff:
                    mark = "ARCHIVE"
                else:
                    mark = "keep"
                head = h.rstrip(b"\r").decode("utf-8", "replace")[:88]
                print(f"    {mark:<24} {fmt_date(d)} {len(b):>8,}B  {head}")
        print()


def rotate_fable(cutoff, through, apply: bool, repo: Path | None = None):
    repo = repo or REPO
    name = "FABLE-FEEDBACK.md"
    data = (repo / name).read_bytes()
    preamble, blocks = split_blocks(data)
    dated = dated_of(blocks)
    old = [b for d, _, b in dated if d is not None and d < cutoff]
    new = [b for d, _, b in dated if d is None or d >= cutoff]

    moved = b"".join(old)
    archive = repo / ARCHIVE_DIR / name
    existing = archive.read_bytes() if archive.exists() else b""

    # DRA-175: nothing to move is not a reason to write. `write_archive` cannot
    # catch this one -- a header-only proposal against an EMPTY archive is a
    # legal first write, and against a populated one the refusal fires but the
    # run still exits 2 on a tree where the correct answer is "no work". This
    # is the exact state of the tree at the default cutoff.
    if not old:
        print(f"{name}: NOTHING TO ROTATE at cutoff {through} — "
              f"{len(blocks)} blocks, none older. Archive untouched "
              f"({len(existing):,}B, {len(split_blocks(existing)[1])} entries).")
        return 0

    if existing:
        # DRA-175: SECOND pass onward. The DRA-75 header stays exactly as
        # written -- a second header would be a second file claiming to be this
        # one -- and the new entries go on the END behind DRA-211's pass marker.
        # Without this arm the frozen path can only ever propose `header +
        # moved`, which `write_archive` correctly REFUSES: safe, but it makes a
        # second FABLE rotation impossible rather than append-only.
        addition = PASS_MARKER.format(
            hashes="#" * DEFAULT_UNIT, date=dt.date.today().isoformat(),
            card=DRA175_CARD, name=name, through=through,
            count=len(old), size=len(moved), sha=sha(moved),
        ).encode("utf-8") + moved
        proposed = existing + addition
        # No second pointer, for the reason rotate_file states: the active
        # file's pointer prose is hand-written across the earlier passes and
        # regenerating it would be an Executor trimming channel content.
        pointer = b""
    else:
        # The first-pass composition is DRA-75's, unchanged and byte-for-byte.
        addition = archive_header(
            name, through, "", len(old), len(moved),
            card=DRA75_CARD, date=DRA75_DATE, provenance=DRA75_PROVENANCE,
            holds=DRA75_HOLDS,
        ) + moved
        proposed = addition
        pointer = pointer_text(
            through, f"{ARCHIVE_DIR}/{name}", len(old), len(moved),
            card=DRA75_CARD, date=DRA75_DATE,
        )

    write_archive(archive, proposed, apply)
    active = pointer + preamble + b"".join(new)
    if apply:
        (repo / name).write_bytes(active)

    verb = "append" if existing else "create"
    print(f"{name}: {len(data):,}B -> active {len(active):,}B "
          f"+ archive {len(existing):,}B +{len(addition):,}B = {len(proposed):,}B "
          f"({verb})   moved={len(old)} kept={len(new)} moved-sha={sha(moved)}")
    if existing:
        cum = split_blocks(proposed)[1]
        markers = sum(1 for h, _ in cum if b"ROTATION PASS APPENDED" in h)
        print(f"  NO new pointer was written; the existing pointer prose was left "
              f"alone and now names the earlier passes only. Cumulative after this "
              f"pass: {len(cum) - markers} entries + {markers} pass marker(s) / "
              f"{len(proposed):,}B in {ARCHIVE_DIR}/{name}. "
              f"Correct the pointer and the live rotation marker by hand.")
    return 0


def rotate_helm(apply: bool, repo: Path | None = None):
    repo = repo or REPO
    name = "HELM-FEEDBACK.md"
    data = (repo / name).read_bytes()
    lines = data.split(b"\n")
    idx = flat_line_indices(data)
    # DRA-175: zero flattened lines is not a failure -- it is DRA-75 having
    # already done this half. The assert below was written when exactly two
    # were guaranteed, and post-DRA-75 it aborts the WHOLE rotate command
    # before the FABLE half runs, so even the dry run cannot be read. Measured
    # on main at 6dc9b3b8: `rotate --cutoff 2026-09-08` and `--cutoff
    # 2026-09-15` both die here, rc=1, before FABLE is reached. This function
    # never re-rotates HELM-FEEDBACK.md; that room is DRA-154's.
    if not idx:
        print(f"{name}: SKIPPED — 0 flattened lines >= {HF_FLAT_MIN:,}B "
              f"({len(data):,}B on disk). DRA-75 discharged this half; "
              f"nothing here to move and no re-rotation is attempted.")
        return
    assert len(idx) == 2, f"expected 2 flattened lines, found {len(idx)}: {idx}"
    flat = [lines[i] for i in idx]
    rest = b"\n".join(l for i, l in enumerate(lines) if i not in idx)
    print(f"  flattened lines found at {idx} "
          f"({', '.join(f'{len(f):,}B' for f in flat)})")

    last_good = git_blob(HF_LAST_GOOD, name)
    l71 = peel(flat[0])
    h71 = list(re.finditer(rb"## 20\d\d-\d\d-\d\d", l71))
    entry564 = l71[: h71[1].start()]

    # Re-prove containment before destroying anything.
    hlg = list(re.finditer(rb"(?m)^## 20\d\d-\d\d-\d\d", last_good))
    assert sig(peel(flat[1])) == sig(last_good), "line 73 is not the last-good blob"
    assert sig(l71[h71[1].start():]) == sig(last_good[hlg[1].start():]), \
        "line 71 tail is not last-good-minus-first-entry"

    _, lg_blocks = split_blocks(last_good)
    # The recovered entry ends without a trailing newline pair; normalize.
    recovered = entry564.rstrip() + b"\n\n"
    moved = recovered + last_good
    archived_count = len(lg_blocks) + 1

    header = archive_header(
        name, "2026-09-11", HF_NOTE, archived_count, len(moved),
        card=DRA75_CARD, date=DRA75_DATE, provenance=DRA75_PROVENANCE,
        holds=DRA75_HOLDS,
    )
    pointer = pointer_text(
        "2026-09-11 (through the PR #564 ask)",
        f"{ARCHIVE_DIR}/{name}", archived_count, len(moved),
        card=DRA75_CARD, date=DRA75_DATE,
    )

    # The EXACT bytes that left, kept beside the recovery. Two reasons, and the
    # second is the load-bearing one:
    #   1. Forensics -- "the corrupt bytes are in git at 1e0f7232" is true but
    #      needs a SHA nobody will have. This is checkable from a checkout.
    #   2. It is what makes "nothing was lost" VERIFIABLE rather than asserted.
    #      channel-wipe-guard's ARCHIVE exemption asks whether the lines and
    #      entry keys that left are present under the archive dir. Against the
    #      recovery alone that lands at 89.5% -- not because anything is
    #      missing, but because an entry key recovered from a FLATTENED line
    #      absorbs body text up to the 80-char cap, while the same entry in the
    #      newline-delimited recovery stops at end of line. Same entries, keyed
    #      differently. Rather than argue with a guard that deliberately has no
    #      escape hatch, put the bytes where it can see them.
    raw_header = (
        f"# ORIGINAL FLATTENED BYTES — {name}\n\n"
        "**Do not read this file; read `HELM-FEEDBACK.md` beside it.** This is the\n"
        "verbatim content that DRA-75 removed from the active channel file on\n"
        "2026-09-14: two lines of ~2.4 MB each, holding two cp437-mojibake copies of\n"
        "one history (trap 60c, applied twice). It is unreadable on purpose — it is\n"
        "kept so that 'nothing was lost' can be checked against the bytes rather than\n"
        "taken on trust, and so `channel-wipe-guard`'s ARCHIVE exemption can see the\n"
        "entries that moved. The readable recovery of the same content is in\n"
        f"`{name}` in this directory.\n\n---\n\n"
    ).encode("utf-8")
    raw_name = name.replace(".md", ".original-flattened.md")

    active = pointer + rest.lstrip(b"\r\n")
    # Same append guard as everywhere else; a no-op on the pristine tree this
    # path is written for, and a refusal rather than a replace on any other.
    write_archive(repo / ARCHIVE_DIR / name, header + moved, apply)
    if apply:
        (repo / ARCHIVE_DIR).mkdir(parents=True, exist_ok=True)
        (repo / ARCHIVE_DIR / raw_name).write_bytes(
            raw_header + b"\n".join(flat) + b"\n"
        )
        (repo / name).write_bytes(active)
    print(f"  kept the exact removed bytes at {ARCHIVE_DIR}/{raw_name} "
          f"({sum(len(f) for f in flat):,}B)")
    print(f"{name}: {len(data):,}B -> active {len(active):,}B + archive "
          f"{len(header)+len(moved):,}B   archived={archived_count} entries "
          f"(recovered #564 + {len(lg_blocks)} from {HF_LAST_GOOD})")
    print(f"  dropped {sum(len(f) for f in flat):,}B of duplicate mojibake; "
          f"recovered-sha={sha(recovered)}")


def rotate_file(name, cutoff, through, *, card, date, unit=DEFAULT_UNIT,
                date_source="heading", apply=False, force=False, holds="",
                hold_terms=(), marker_hold=True, repo: Path | None = None):
    """Date-rotate one file. The general path: `report` and `rotate` agree
    because both partition through split_blocks(data, unit), date through
    dated_of(blocks, source), and hold through is_held(heading).

    Undated entries are KEPT LIVE, never archived by a date cut -- an entry with
    no date has not been shown to be old.

    Rotation markers are KEPT LIVE regardless of date. A marker is a dated `## `
    entry, so a naive date cut moves the file's own rotation record into the
    archive; it is metadata ABOUT the file, not an entry of its history.

    A SECOND pass APPENDS to the existing archive behind a pass marker, and
    writes no second pointer. It cannot replace: every archive write goes
    through `write_archive`.
    """
    repo = repo or REPO
    data = (repo / name).read_bytes()
    preamble, blocks = split_blocks(data, unit)
    if not blocks:
        print(f"{name}: no level-{unit} headings -- wrong unit, nothing done")
        return 1

    dated = dated_of(blocks, date_source)
    reasons = [is_held(h, hold_terms, marker_hold) for _, h, _ in dated]
    old = [b for (d, _, b), r in zip(dated, reasons)
           if not r and d is not None and d < cutoff]
    new = [b for (d, _, b), r in zip(dated, reasons)
           if r or d is None or d >= cutoff]

    # This tool MOVES bytes; it never rewrites them. Every byte of the input is
    # in exactly one of preamble / archived / kept. Asserted before any write.
    moved = b"".join(old)
    kept = b"".join(new)
    assert len(preamble) + len(moved) + len(kept) == len(data), (
        f"{name}: byte conservation failed "
        f"({len(preamble)}+{len(moved)}+{len(kept)} != {len(data)})"
    )

    for (d, h, b), r in ((x, y) for x, y in zip(dated, reasons) if y):
        print(f"{name}: HELD LIVE ({r}) {fmt_date(d)} {len(b):,}B  "
              f"{h.rstrip(chr(13).encode()).decode('utf-8', 'replace')[:88]}")

    if not old:
        print(f"{name}: nothing older than {through} -- nothing to rotate")
        return 0

    archive_path = repo / ARCHIVE_DIR / name
    existing = archive_path.read_bytes() if archive_path.exists() else b""
    pointer_present = b"history rotated" in data[:4000].lower()

    # The two already-rotated tells still gate a second pass behind --force,
    # because a second pass makes the active file's hand-written pointer prose
    # stale and a human has to fix it. What --force can no longer do is drop
    # archived history: the composition below is an APPEND and write_archive
    # refuses anything else, with no override.
    if existing and not force:
        print(f"{name}: REFUSING -- {ARCHIVE_DIR}/{name} already exists "
              f"({len(existing):,}B, {len(split_blocks(existing, unit)[1])} entries). "
              f"--force now APPENDS behind a pass marker and cannot overwrite it, "
              f"but it leaves this file's pointer prose naming pass-1 numbers only. "
              f"Pass --force once you are willing to correct that prose by hand.")
        return 1
    if pointer_present and not existing and not force:
        print(f"{name}: REFUSING -- the file carries a rotation pointer but "
              f"{ARCHIVE_DIR}/{name} does not exist. The archive this pointer names "
              f"has been moved or renamed; find it before rotating, or pass --force "
              f"to start a fresh archive at that path.")
        return 1

    if existing:
        # SECOND pass onward. The first pass's header stays exactly as written
        # -- a second header would be a second file claiming to be this one --
        # and the new entries go on the END behind a marker naming the pass.
        addition = PASS_MARKER.format(
            hashes="#" * unit, date=date, card=card, name=name, through=through,
            count=len(old), size=len(moved), sha=sha(moved),
        ).encode("utf-8") + moved
        proposed = existing + addition
    else:
        addition = archive_header(name, through, "", len(old), len(moved),
                                  card=card, date=date, holds=holds) + moved
        proposed = addition

    # No second pointer. The active file's pointer block is hand-written prose
    # (DRA-165 consolidated two passes into one table); regenerating it would be
    # an Executor trimming channel content, DRA-26 rev 3 section 5. The tool
    # reports the numbers this pass makes stale instead.
    #
    # An EXISTING ARCHIVE suppresses the pointer just as a present pointer does,
    # and that is not belt-and-braces. The template says "({count} entries,
    # {size} bytes)" and means the whole archive; on any pass after the first
    # those are this pass's numbers, so writing it would state a smaller archive
    # than the one on disk. A pointer that undercounts the archive is the same
    # class of false-green the replace was.
    pointer = b"" if (existing or pointer_present) else pointer_text(
        through, f"{ARCHIVE_DIR}/{name}", len(old), len(moved),
        card=card, date=date,
    )
    active = pointer + preamble + kept

    write_archive(archive_path, proposed, apply)
    if apply:
        (repo / name).write_bytes(active)

    verb = "append" if existing else "create"
    print(f"{name}: {len(data):,}B -> active {len(active):,}B + archive "
          f"{len(existing):,}B +{len(addition):,}B = {len(proposed):,}B ({verb})   "
          f"moved={len(old)} kept={len(new)} "
          f"undated-kept={sum(1 for d, _, _ in dated if d is None)} "
          f"held={sum(1 for r in reasons if r)} moved-sha={sha(moved)}")
    print(f"  unit=h{unit} date-from={date_source} stamp={card} {date}")
    if existing or pointer_present:
        cum_blocks = split_blocks(proposed, unit)[1]
        markers = sum(1 for h, _ in cum_blocks if b"ROTATION PASS APPENDED" in h)
        print(f"  NO new pointer was written; the existing pointer prose was left "
              f"alone and now names pass-1 numbers only. Cumulative after this pass: "
              f"{len(cum_blocks) - markers} entries + {markers} pass marker(s) / "
              f"{len(proposed):,}B in {ARCHIVE_DIR}/{name}. "
              f"Correct the pointer and the live rotation marker by hand.")
    return 0


def cmd_rotate(args):
    cutoff = tuple(int(x) for x in args.cutoff.split("-"))
    try:
        if not args.files:
            # No file named: the DRA-75 pair, on its frozen incident-specific
            # path. These two encode recoveries (a cp437 de-mojibake, a
            # flattened-line splice) that must NOT be generalised away.
            rotate_helm(args.apply)
            rotate_fable(cutoff, args.cutoff, args.apply)
            if not args.apply:
                print("\n(dry run -- pass --apply to write)")
            return 0

        if not args.card:
            print("rotate: --card is required (the card id stamped into the archive "
                  "header and the live pointer)")
            return 2

        rc = 0
        for name in args.files:
            rc |= rotate_file(name, cutoff, args.cutoff, card=args.card,
                              date=args.date, unit=args.unit,
                              date_source=args.date_from,
                              apply=args.apply, force=args.force,
                              holds=DRA75_HOLDS if args.holds else "",
                              hold_terms=tuple(args.hold or ()),
                              marker_hold=not args.no_marker_hold)
    except ArchiveReplace as e:
        print(f"\n*** {e}", file=sys.stderr)
        print("*** nothing was written.", file=sys.stderr)
        return 2
    if not args.apply:
        print("\n(dry run -- pass --apply to write)")
    return rc


def cmd_verify(args):
    """Verify against PRISTINE working-tree copies, not git blobs.

    `core.autocrlf=true` here, so `git show ref:path` returns the LF-normalized
    blob while the working tree holds CRLF. Comparing the rotated CRLF output
    against an LF blob is a false failure (it reported one, once). The honest
    reference is a byte copy of the file taken before the rotation ran.
    """
    ok = True
    pristine = Path(args.pristine)

    # FABLE-FEEDBACK: archive + active must reproduce the original blocks.
    name = "FABLE-FEEDBACK.md"
    original = (pristine / name).read_bytes()
    _, o_blocks = split_blocks(original)
    _, a_blocks = split_blocks((REPO / ARCHIVE_DIR / name).read_bytes())
    _, k_blocks = split_blocks((REPO / name).read_bytes())
    # Drop the archive header block (it is a `# ` heading, not `## `) and the
    # pointer, both of which contain no `## ` headings.
    union = sorted([b for _, b in a_blocks] + [b for _, b in k_blocks])
    orig = sorted(b for _, b in o_blocks)
    # The invariant is SURVIVAL, not equality: notes get appended to the active
    # file after a rotation, so the rotated set is allowed to be a superset.
    from collections import Counter
    lost = Counter(orig) - Counter(union)
    added = Counter(union) - Counter(orig)
    same = not lost
    ok &= same
    print(f"{name}: {len(o_blocks)} original blocks -> archive {len(a_blocks)} + "
          f"active {len(k_blocks)} = {len(union)}")
    print(f"  every original entry survives byte-exact: {'YES' if same else 'NO'}"
          f"   (lost {sum(lost.values())}, added since {sum(added.values())})")
    print(f"  original entry bytes {sum(len(b) for b in orig):,} all accounted for: "
          f"{'YES' if same else 'NO'}")
    for b in list(lost)[:3]:
        print(f"   LOST: {b.split(chr(10).encode())[0][:100]!r}")

    # HELM-FEEDBACK: the archive must contain the last-good blob verbatim, and
    # every readable entry of the pre-rotation file must survive somewhere.
    name = "HELM-FEEDBACK.md"
    arch = (REPO / ARCHIVE_DIR / name).read_bytes()
    act = (REPO / name).read_bytes()
    last_good = git_blob(HF_LAST_GOOD, name)
    # Line-ending agnostic ON PURPOSE. core.autocrlf=true means a fresh
    # checkout hands this file back as CRLF while the git blob is LF, so a
    # raw containment test would report NO on a clean clone for a file that is
    # byte-identical in the repository. Normalize the representation git owns;
    # everything else still has to match exactly.
    def lf(b: bytes) -> bytes:
        return b.replace(b"\r\n", b"\n")

    contains = lf(last_good) in lf(arch)
    ok &= contains
    print(f"{name}: archive contains {HF_LAST_GOOD} blob verbatim: "
          f"{'YES' if contains else 'NO'} ({len(last_good):,}B)")

    pre = (pristine / name).read_bytes()
    pre_lines = pre.split(b"\n")
    pre_idx = flat_line_indices(pre)
    pre_readable = b"\n".join(
        l for i, l in enumerate(pre_lines) if i not in pre_idx
    )
    _, pre_blocks = split_blocks(pre_readable)
    _, act_blocks = split_blocks(act)
    pre_lost = Counter(b for _, b in pre_blocks) - Counter(b for _, b in act_blocks)
    surv = not pre_lost
    ok &= surv
    print(f"{name}: all {len(pre_blocks)} readable pre-rotation entries survive in "
          f"active: {'YES' if surv else 'NO'} (lost {sum(pre_lost.values())})")
    for b in list(pre_lost)[:3]:
        print(f"   LOST: {b.split(chr(10).encode())[0][:100]!r}")

    # The recovered #564 entry must be readable in the archive.
    probe = "LIVE ASK: **SIGN PR #564**".encode()
    has564 = probe in arch
    ok &= has564
    print(f"{name}: recovered #564 entry present and readable: "
          f"{'YES' if has564 else 'NO'}")
    # And it must be clean UTF-8 with a real em-dash, not mojibake.
    clean = "— LIVE ASK: **SIGN PR #564**".encode("utf-8") in arch
    ok &= clean
    print(f"{name}: #564 entry is clean UTF-8 (real em-dash, not mojibake): "
          f"{'YES' if clean else 'NO'}")

    print()
    print("ALL CHECKS PASS" if ok else "*** VERIFICATION FAILED ***")
    return 0 if ok else 1


# The fixture is shaped like the real thing, not like a unit test: the marker
# heading is FABLE-FEEDBACK.md's verbatim (two dates, the em-dash, the pass
# parenthetical), because the hold is decided by matching that prose and a
# fixture that paraphrases it would prove nothing about the file on disk.
ST_MARKER = (
    "## 2026-09-14 / 2026-09-17 — THIS CHANNEL HAS BEEN ROTATED TWICE "
    "(pass 1: before 2026-09-08 · pass 2: before 2026-09-15)\n\n"
    "This marker stays live in every future pass.\n\n"
)
ST_POINTER = (
    "<!-- DRA-75: history before 2026-09-08 lives in the Q3 archive -->\n\n"
    "> **History rotated 2026-09-14.** 2 entries moved.\n\n---\n\n"
)
ST_MOVE = "## 2026-09-09 — an entry the second pass should MOVE\n\nbody three\n\n"
ST_KEEP = "## 2026-09-20 — an entry the second pass should KEEP\n\nbody four\n\n"
ST_ARCHIVE_1 = (
    "# ARCHIVE — FABLE-FEEDBACK.md (through 2026-09-08)\n\n"
    "- Entries archived: **2**\n\n---\n\n"
    "## 2026-09-01 — an entry from the first pass\n\nbody one\n\n"
    "## 2026-09-02 — a second entry from the first pass\n\nbody two\n\n"
)


def cmd_selftest(args):
    """Prove the DRA-211 arms, prove-failed rather than green-only (trap 78).

    Every claim here is entry-count or byte arithmetic. "the guard reported
    green" is explicitly not evidence -- the defect this card was opened on was
    a run that reported success while deleting an archive.
    """
    checks: list[tuple[str, bool, str]] = []

    def check(label, ok, detail=""):
        checks.append((label, bool(ok), detail))

    name = "FABLE-FEEDBACK.md"
    cutoff, through = (2026, 9, 15), "2026-09-15"

    with tempfile.TemporaryDirectory() as td:
        tmp = Path(td)
        (tmp / ARCHIVE_DIR).mkdir(parents=True)
        arch = tmp / ARCHIVE_DIR / name
        active = tmp / name
        first_pass = ST_ARCHIVE_1.encode("utf-8")
        arch.write_bytes(first_pass)
        before = (ST_POINTER + ST_MARKER + ST_MOVE + ST_KEEP).encode("utf-8")
        active.write_bytes(before)

        def rot(**kw):
            return rotate_file(name, cutoff, through, card="DRA-211",
                               date="2026-09-22", apply=True, force=True,
                               repo=tmp, **kw)

        # ---- 1. PROVE-FAIL FIRST. Without the hold, the marker is archived.
        #      This is the defect; if it does not reproduce, the hold below is
        #      proving nothing.
        rot(marker_hold=False)
        check("prove-fail: WITHOUT the hold the marker is archived",
              b"HAS BEEN ROTATED TWICE" in arch.read_bytes()
              and b"HAS BEEN ROTATED TWICE" not in active.read_bytes())

        # Reset and run the real path.
        arch.write_bytes(first_pass)
        active.write_bytes(before)
        rot()
        after_arch = arch.read_bytes()
        after_act = active.read_bytes()

        # ---- 2. The marker stays live, and only the dated entry moved.
        check("marker is present in the live file after the second rotation",
              ST_MARKER.encode("utf-8") in after_act)
        check("marker did NOT reach the archive",
              b"HAS BEEN ROTATED TWICE" not in after_arch)
        check("the dated old entry moved, the new one stayed",
              ST_MOVE.encode("utf-8") in after_arch
              and ST_MOVE.encode("utf-8") not in after_act
              and ST_KEEP.encode("utf-8") in after_act)

        # ---- 3. Byte arithmetic on the ACTIVE file: exactly the moved entry
        #      left, and nothing else changed.
        check("active shrank by exactly the moved entry",
              len(before) - len(after_act) == len(ST_MOVE.encode("utf-8")),
              f"{len(before):,} - {len(after_act):,} = "
              f"{len(before)-len(after_act):,}B, moved entry is "
              f"{len(ST_MOVE.encode('utf-8')):,}B")

        # ---- 4. Byte arithmetic on the ARCHIVE: every byte of the first pass
        #      is still there, in place, and the growth is marker + entry.
        check("archive still STARTS with the first pass byte-for-byte",
              after_arch.startswith(first_pass),
              f"{len(first_pass):,}B -> {len(after_arch):,}B")
        grew = len(after_arch) - len(first_pass)
        moved_b = ST_MOVE.encode("utf-8")
        want_marker = PASS_MARKER.format(
            hashes="##", date="2026-09-22", card="DRA-211", name=name,
            through=through, count=1, size=len(moved_b), sha=sha(moved_b),
        ).encode("utf-8")
        check("archive grew by exactly pass-marker + moved entry",
              grew == len(want_marker) + len(moved_b)
              and after_arch[len(first_pass):] == want_marker + moved_b,
              f"+{grew:,}B = marker {len(want_marker):,}B + entry {len(moved_b):,}B")

        # ---- 5. Entry arithmetic: 2 first-pass entries + 1 pass marker + 1
        #      appended entry, and both originals are still whole.
        _, a_blocks = split_blocks(after_arch)
        heads = [h for h, _ in a_blocks]
        check("both first-pass entries survive as whole entries",
              sum(b"first pass" in h for h in heads) == 2,
              f"{len(a_blocks)} blocks in the archive")
        check("the pass marker is a block of its own, not swallowed",
              sum(b"ROTATION PASS APPENDED" in h for h in heads) == 1)
        check("archive entry count is 2 old + 1 marker + 1 appended",
              len(a_blocks) == 4, f"got {len(a_blocks)}")

        # ---- 6. No second pointer.
        check("no second pointer was written",
              after_act.count(b"History rotated") == 1
              and after_act.startswith(ST_POINTER.encode("utf-8")))

        # ---- 7. Re-running at the same cutoff moves nothing and writes nothing.
        rot()
        check("a repeat pass at the same cutoff leaves the archive identical",
              arch.read_bytes() == after_arch and active.read_bytes() == after_act)

        # ---- 8. PROVE-FAIL: the DRA-75 composition (header + moved) against a
        #      populated archive must be REFUSED, and must write nothing. This
        #      is the one-way door the card names.
        stock = archive_header(name, through, "", 0, 0,
                               card="DRA-211", date="2026-09-22")
        fired = ""
        try:
            write_archive(arch, stock, apply=True)
        except ArchiveReplace as e:
            fired = str(e)
        check("stock replace proposal is REFUSED", bool(fired), fired[:110])
        check("the refused proposal wrote nothing", arch.read_bytes() == after_arch)

        # ---- 9. And the refusal is not a blanket no.
        try:
            write_archive(arch, after_arch + b"\n## 2026-09-21 - later\n\nx\n",
                          apply=False)
            check("a genuine append is NOT refused", True)
        except ArchiveReplace as e:
            check("a genuine append is NOT refused", False, str(e))

    # ---- 10. The pass marker's heading level follows --unit. A `## ` marker in
    #      a level-3 file is not a block at that unit -- it would be swallowed
    #      by the preceding entry, which is the exact failure the heading exists
    #      to avoid, one unit over.
    with tempfile.TemporaryDirectory() as td:
        tmp = Path(td)
        (tmp / ARCHIVE_DIR).mkdir(parents=True)
        n3 = "SCRIBE.md"
        a3 = tmp / ARCHIVE_DIR / n3
        first3 = b"# ARCHIVE\n\n---\n\n### an old level-3 entry\n\n2026-09-01 body\n\n"
        a3.write_bytes(first3)
        (tmp / n3).write_bytes(
            b"### an entry to move\n\n2026-09-09 body\n\n"
            b"### an entry to keep\n\n2026-09-20 body\n\n"
        )
        rotate_file(n3, cutoff, through, card="DRA-211", date="2026-09-22",
                    unit=3, date_source="body", apply=True, force=True, repo=tmp)
        got = a3.read_bytes()
        _, b3 = split_blocks(got, 3)
        check("level-3 rotation writes a level-3 pass marker",
              sum(b"ROTATION PASS APPENDED" in h for h, _ in b3) == 1
              and b"\n## " not in got.split(b"---", 1)[1],
              f"{len(b3)} level-3 blocks")
        check("level-3 archive still starts with the first pass",
              got.startswith(first3))
        # An archive already exists here, so no pointer may be written even
        # though this active file carries none -- the template's count would
        # name this pass only and undercount what is on disk.
        check("an existing archive suppresses the pointer too",
              b"History rotated" not in (tmp / n3).read_bytes())

    # ---- 11-16. DRA-175 / PR #696: the same three arms on the FROZEN pair,
    #      which rotate_file's checks above cannot reach -- rotate_helm and
    #      rotate_fable are the incident-specific path and have their own
    #      control flow.
    with tempfile.TemporaryDirectory() as td:
        tmp = Path(td)
        (tmp / ARCHIVE_DIR).mkdir(parents=True)
        arch = tmp / ARCHIVE_DIR / name
        active = tmp / name
        first_pass = ST_ARCHIVE_1.encode("utf-8")
        arch.write_bytes(first_pass)
        active.write_bytes((ST_POINTER + ST_MOVE + ST_KEEP).encode("utf-8"))
        # 0 flattened lines, exactly like the post-DRA-75 file on main today.
        helm = tmp / "HELM-FEEDBACK.md"
        helm_bytes = "## 2026-09-16 — a normal readable entry\n\nbody\n".encode("utf-8")
        helm.write_bytes(helm_bytes)

        # The HELM half SKIPS instead of asserting, and writes nothing. On main
        # this raises AssertionError and the FABLE half below never runs.
        rotate_helm(apply=True, repo=tmp)
        check("frozen: helm-skip leaves HELM-FEEDBACK.md byte-identical",
              helm.read_bytes() == helm_bytes)
        check("frozen: helm-skip creates no HELM archive",
              not (tmp / ARCHIVE_DIR / "HELM-FEEDBACK.md").exists())

        # The FABLE half APPENDS rather than proposing a replace. On main this
        # composes `header + moved` and write_archive refuses it.
        rotate_fable(cutoff, through, apply=True, repo=tmp)
        after = arch.read_bytes()
        check("frozen: archive still STARTS with the first pass verbatim",
              after.startswith(first_pass), f"{len(first_pass):,}B -> {len(after):,}B")
        heads = [h for h, _ in split_blocks(after)[1]]
        check("frozen: both first-pass entries survive and the moved one arrived",
              sum(b"first pass" in h for h in heads) == 2
              and any(b"should MOVE" in h for h in heads)
              and sum(b"ROTATION PASS APPENDED" in h for h in heads) == 1)
        act = active.read_bytes()
        check("frozen: the kept entry stayed live and no second pointer landed",
              b"should KEEP" in act and b"should MOVE" not in act
              and act.count(b"History rotated") == 1)

        # Nothing to move writes NOTHING -- the state of the real tree at the
        # default cutoff, and the state in which the pre-DRA-211 code replaced
        # 188 entries with a header.
        rotate_fable(cutoff, through, apply=True, repo=tmp)
        check("frozen: a repeat pass with nothing to move writes nothing",
              arch.read_bytes() == after)

    print()
    for label, ok, detail in checks:
        print(f"  [{'PASS' if ok else 'FAIL'}] {label}" + (f"   {detail}" if detail else ""))
    bad = [c for c in checks if not c[1]]
    print(f"\n{len(checks)-len(bad)}/{len(checks)} checks pass")
    return 0 if not bad else 1


def main():
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)

    def partition_args(p):
        p.add_argument("--unit", type=int, default=DEFAULT_UNIT, choices=(2, 3),
                       help="heading level that delimits one entry (default 2, the "
                            "DRA-75 behaviour). SCRIBE.md needs 3. Pick it from "
                            "`report`; do not guess it.")
        p.add_argument("--date-from", default="heading", choices=("heading", "body"),
                       dest="date_from",
                       help="where an entry's date is read: the heading line "
                            "(default, DRA-75 behaviour) or, when the heading has "
                            "none, the first date in the block body. SCRIBE.md "
                            "needs `body`: 0 of its 89 headings carry a date.")
        p.add_argument("--hold", action="append", default=[], metavar="SUBSTRING",
                       help="hold live any entry whose heading contains this "
                            "substring, regardless of its date. Repeatable. Use it "
                            "for standing rules that must not age out (DRA-178 "
                            "re-pinned two in SCRIBE-FEEDBACK.md by hand).")
        p.add_argument("--no-marker-hold", action="store_true",
                       dest="no_marker_hold",
                       help="turn OFF the default hold on rotation markers. A "
                            "marker is a dated entry, so without the hold a later "
                            "cutoff archives the file's own rotation record. Every "
                            "held entry is named in the output either way.")

    r = sub.add_parser("report")
    r.add_argument("files", nargs="+")
    r.add_argument("--cutoff", default="2026-08-15")
    r.add_argument("--show-blocks", action="store_true", dest="show_blocks",
                   help="list every entry with its resolved date and archive/keep "
                        "disposition -- read this back before rotating")
    partition_args(r)
    r.set_defaults(func=cmd_report)

    o = sub.add_parser("rotate")
    o.add_argument("files", nargs="*",
                   help="files to rotate. With NO file argument this runs the "
                        "frozen DRA-75 pair (HELM-FEEDBACK.md + FABLE-FEEDBACK.md) "
                        "on their incident-specific recovery path.")
    o.add_argument("--cutoff", default="2026-09-08")
    o.add_argument("--card", help="calling card id, stamped into the archive header "
                                  "and the live pointer. Required with a file.")
    o.add_argument("--date", default=dt.date.today().isoformat(),
                   help="rotation date stamped into header and pointer "
                        "(default: today)")
    o.add_argument("--holds", action="store_true",
                   help="include the Helm-holds sentence in the archive banner. "
                        "OFF by default: it is true of HELM.md and the two DRA-75 "
                        "feedback channels, and false of SCRIBE.md / DECISIONS.md / "
                        "BEVEL.md. Do not set it for a file that has no holds.")
    o.add_argument("--force", action="store_true",
                   help="run a SECOND pass on an already-rotated file. It APPENDS "
                        "behind a pass marker and cannot overwrite the earlier "
                        "archive -- write_archive refuses any proposal that is not "
                        "a byte-prefix extension, and no flag overrides that. What "
                        "--force does accept is that the active file's hand-written "
                        "pointer prose will be left naming pass-1 numbers only.")
    o.add_argument("--apply", action="store_true")
    partition_args(o)
    o.set_defaults(func=cmd_rotate)

    v = sub.add_parser("verify")
    v.add_argument("--pristine", required=True,
                   help="directory holding byte copies of the files taken "
                        "BEFORE the rotation (not git blobs -- autocrlf)")
    v.set_defaults(func=cmd_verify)

    s = sub.add_parser("selftest",
                       help="prove the DRA-211 arms: marker hold, second-pass "
                            "append, and that the replace refusal FIRES")
    s.set_defaults(func=cmd_selftest)

    args = p.parse_args()
    sys.exit(args.func(args) or 0)


if __name__ == "__main__":
    main()
