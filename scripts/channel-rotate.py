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
    Their output is byte-for-byte what is on main; that is a done-bar condition.

Two rules the general path enforces that DRA-75 did not need:

  * Undated entries are KEPT LIVE. An entry with no date has not been shown to
    be old. (SCRIBE.md: 23 entries, 25,799 B.)

  * Re-rotating a file OVERWRITES its earlier archive with a smaller one and
    still reports success. `rotate` refuses when the archive already exists or
    the file already carries a rotation pointer, unless `--force`.

Byte conservation (preamble + archived + kept == input) is asserted before any
write. This tool MOVES bytes; it never rewrites them.

Pick the unit and the date source per file from `report`; do not guess them.
`report --show-blocks` lists every entry with its resolved date and its
archive/keep disposition -- read that back before rotating anything.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import re
import subprocess
import sys
from pathlib import Path

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

Nothing in this file is a work queue and nothing here is live. Holds live in
`HELM.md` and only Helm lifts one — an archived line never revives a hold.
Do not append here; append to the active `{name}`.

{note}
- Entries archived: **{count}**
- Bytes: **{size:,}**
- Active file after rotation: `{name}`

---

"""

PROVENANCE = "**Immutable.** Rotated out of the active `{name}` on {date} by {card}."

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


def archive_header(name, through, note, count, size, *, card, date, provenance=None):
    """Archive banner. `card` and `date` are the CALLING card and the REAL
    rotation date -- DRA-75 hardcoded its own, which would stamp the wrong
    provenance into every later archive."""
    prov = (provenance or PROVENANCE).format(name=name, date=date, card=card)
    return ARCHIVE_HEADER.format(
        name=name, through=through, provenance=prov, note=note, count=count, size=size
    ).encode("utf-8")


def pointer_text(through, archive, count, size, *, card, date):
    return POINTER.format(
        card=card, date=date, through=through, archive=archive,
        archive_rel=archive, count=count, size=size,
    ).encode("utf-8")


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
    for name in args.files:
        data = (REPO / name).read_bytes()
        preamble, blocks = split_blocks(data, args.unit)
        dated = dated_of(blocks, args.date_from)
        old = [x for x in dated if x[0] is not None and x[0] < cutoff]
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

        if args.show_blocks:
            for d, h, b in dated:
                mark = "ARCHIVE" if (d is not None and d < cutoff) else "keep   "
                head = h.rstrip(b"\r").decode("utf-8", "replace")[:88]
                print(f"    {mark} {fmt_date(d)} {len(b):>8,}B  {head}")
        print()


def rotate_fable(cutoff, through, apply: bool):
    name = "FABLE-FEEDBACK.md"
    data = (REPO / name).read_bytes()
    preamble, blocks = split_blocks(data)
    dated = dated_of(blocks)
    old = [b for d, _, b in dated if d is not None and d < cutoff]
    new = [b for d, _, b in dated if d is None or d >= cutoff]

    moved = b"".join(old)
    header = archive_header(
        name, through, "", len(old), len(moved),
        card=DRA75_CARD, date=DRA75_DATE, provenance=DRA75_PROVENANCE,
    )
    pointer = pointer_text(
        through, f"{ARCHIVE_DIR}/{name}", len(old), len(moved),
        card=DRA75_CARD, date=DRA75_DATE,
    )

    if apply:
        (REPO / ARCHIVE_DIR).mkdir(parents=True, exist_ok=True)
        (REPO / ARCHIVE_DIR / name).write_bytes(header + moved)
        (REPO / name).write_bytes(pointer + preamble + b"".join(new))
    print(f"{name}: {len(data):,}B -> active {len(pointer)+len(preamble)+sum(len(b) for b in new):,}B "
          f"+ archive {len(header)+len(moved):,}B   moved={len(old)} kept={len(new)} "
          f"moved-sha={sha(moved)}")


def rotate_helm(apply: bool):
    name = "HELM-FEEDBACK.md"
    data = (REPO / name).read_bytes()
    lines = data.split(b"\n")
    idx = flat_line_indices(data)
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
    if apply:
        (REPO / ARCHIVE_DIR).mkdir(parents=True, exist_ok=True)
        (REPO / ARCHIVE_DIR / name).write_bytes(header + moved)
        (REPO / ARCHIVE_DIR / raw_name).write_bytes(
            raw_header + b"\n".join(flat) + b"\n"
        )
        (REPO / name).write_bytes(active)
    print(f"  kept the exact removed bytes at {ARCHIVE_DIR}/{raw_name} "
          f"({sum(len(f) for f in flat):,}B)")
    print(f"{name}: {len(data):,}B -> active {len(active):,}B + archive "
          f"{len(header)+len(moved):,}B   archived={archived_count} entries "
          f"(recovered #564 + {len(lg_blocks)} from {HF_LAST_GOOD})")
    print(f"  dropped {sum(len(f) for f in flat):,}B of duplicate mojibake; "
          f"recovered-sha={sha(recovered)}")


def rotate_file(name, cutoff, through, *, card, date, unit=DEFAULT_UNIT,
                date_source="heading", apply=False, force=False):
    """Date-rotate one file. The general path: `report` and `rotate` agree
    because both partition through split_blocks(data, unit) and date through
    dated_of(blocks, source).

    Undated entries are KEPT LIVE, never archived by a date cut -- an entry with
    no date has not been shown to be old.
    """
    data = (REPO / name).read_bytes()
    preamble, blocks = split_blocks(data, unit)
    if not blocks:
        print(f"{name}: no level-{unit} headings -- wrong unit, nothing done")
        return 1

    dated = dated_of(blocks, date_source)
    old = [b for d, _, b in dated if d is not None and d < cutoff]
    new = [b for d, _, b in dated if d is None or d >= cutoff]

    # This tool MOVES bytes; it never rewrites them. Every byte of the input is
    # in exactly one of preamble / archived / kept. Asserted before any write.
    moved = b"".join(old)
    kept = b"".join(new)
    assert len(preamble) + len(moved) + len(kept) == len(data), (
        f"{name}: byte conservation failed "
        f"({len(preamble)}+{len(moved)}+{len(kept)} != {len(data)})"
    )

    if not old:
        print(f"{name}: nothing older than {through} -- nothing to rotate")
        return 0

    archive_path = REPO / ARCHIVE_DIR / name
    # Re-running a rotation on an already-rotated file OVERWRITES the previous
    # archive with a smaller one and reports success -- the prior history is
    # gone and the run looks green. Refuse both tells unless forced.
    if archive_path.exists() and not force:
        print(f"{name}: REFUSING -- {ARCHIVE_DIR}/{name} already exists. Re-running a "
              f"rotation overwrites the earlier archive and still reports success. "
              f"Archive the existing file under a new name first, or pass --force.")
        return 1
    if b"history rotated" in data[:4000].lower() and not force:
        print(f"{name}: REFUSING -- the file already carries a rotation pointer. "
              f"Rotate from the live working set, or pass --force.")
        return 1

    header = archive_header(name, through, "", len(old), len(moved),
                            card=card, date=date)
    pointer = pointer_text(through, f"{ARCHIVE_DIR}/{name}", len(old), len(moved),
                           card=card, date=date)
    active = pointer + preamble + kept

    if apply:
        (REPO / ARCHIVE_DIR).mkdir(parents=True, exist_ok=True)
        archive_path.write_bytes(header + moved)
        (REPO / name).write_bytes(active)
    print(f"{name}: {len(data):,}B -> active {len(active):,}B + archive "
          f"{len(header)+len(moved):,}B   moved={len(old)} kept={len(new)} "
          f"undated-kept={sum(1 for d, _, _ in dated if d is None)} "
          f"moved-sha={sha(moved)}")
    print(f"  unit=h{unit} date-from={date_source} stamp={card} {date}")
    return 0


def cmd_rotate(args):
    cutoff = tuple(int(x) for x in args.cutoff.split("-"))
    if not args.files:
        # No file named: the DRA-75 pair, on its frozen incident-specific path.
        # These two encode recoveries (a cp437 de-mojibake, a flattened-line
        # splice) that must NOT be generalised away.
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
        rc |= rotate_file(name, cutoff, args.cutoff, card=args.card, date=args.date,
                          unit=args.unit, date_source=args.date_from,
                          apply=args.apply, force=args.force)
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
    o.add_argument("--force", action="store_true",
                   help="override the already-rotated guards. Re-rotating a file "
                        "overwrites its earlier archive and still reports success.")
    o.add_argument("--apply", action="store_true")
    partition_args(o)
    o.set_defaults(func=cmd_rotate)

    v = sub.add_parser("verify")
    v.add_argument("--pristine", required=True,
                   help="directory holding byte copies of the files taken "
                        "BEFORE the rotation (not git blobs -- autocrlf)")
    v.set_defaults(func=cmd_verify)

    args = p.parse_args()
    sys.exit(args.func(args) or 0)


if __name__ == "__main__":
    main()
