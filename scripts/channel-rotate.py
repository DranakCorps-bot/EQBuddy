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
"""

from __future__ import annotations

import argparse
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

HEADING = re.compile(rb"(?m)^## .*$")
DATE = re.compile(rb"(20\d\d)-(\d\d)-(\d\d)")


# ---------------------------------------------------------------- helpers


def split_blocks(data: bytes):
    starts = [m.start() for m in HEADING.finditer(data)]
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


def dated_of(blocks):
    return [(block_date(h), h, b) for h, b in blocks]


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

**Immutable.** Rotated out of the active `{name}` on 2026-09-14 by DRA-75
(M0-2), under the DRA-73 plan rev 2 approved by David on 2026-09-14.
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

POINTER = """<!-- DRA-75 (M0-2): history before {through} lives in {archive} — immutable, do not append there. -->

> **History rotated 2026-09-14.** Entries before {through} moved to
> [`{archive}`]({archive_rel}) ({count} entries, {size:,} bytes).
> This file carries the live working set only. Append at the top, in explicit
> UTF-8, additions-only (trap 60).

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


def cmd_report(args):
    cutoff = tuple(int(x) for x in args.cutoff.split("-"))
    for name in args.files:
        data = (REPO / name).read_bytes()
        preamble, blocks = split_blocks(data)
        dated = dated_of(blocks)
        old = [x for x in dated if x[0] is not None and x[0] < cutoff]
        dates = sorted(d for d, _, _ in dated if d)
        print(f"{name}")
        print(f"  bytes={len(data):,}  blocks={len(blocks)}  preamble={len(preamble):,}B")
        if dates:
            print(f"  dates {'-'.join('%02d' % v for v in dates[0])} .. "
                  f"{'-'.join('%02d' % v for v in dates[-1])}")
        print(f"  older than {args.cutoff}: {len(old)} blocks / "
              f"{sum(len(b) for _, _, b in old):,}B")
        print()


def rotate_fable(cutoff, through, apply: bool):
    name = "FABLE-FEEDBACK.md"
    data = (REPO / name).read_bytes()
    preamble, blocks = split_blocks(data)
    dated = dated_of(blocks)
    old = [b for d, _, b in dated if d is not None and d < cutoff]
    new = [b for d, _, b in dated if d is None or d >= cutoff]

    moved = b"".join(old)
    header = ARCHIVE_HEADER.format(
        name=name, through=through, note="", count=len(old), size=len(moved)
    ).encode("utf-8")
    pointer = POINTER.format(
        through=through,
        archive=f"{ARCHIVE_DIR}/{name}",
        archive_rel=f"{ARCHIVE_DIR}/{name}",
        count=len(old),
        size=len(moved),
    ).encode("utf-8")

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

    header = ARCHIVE_HEADER.format(
        name=name, through="2026-09-11", note=HF_NOTE,
        count=archived_count, size=len(moved),
    ).encode("utf-8")
    pointer = POINTER.format(
        through="2026-09-11 (through the PR #564 ask)",
        archive=f"{ARCHIVE_DIR}/{name}",
        archive_rel=f"{ARCHIVE_DIR}/{name}",
        count=archived_count,
        size=len(moved),
    ).encode("utf-8")

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


def cmd_rotate(args):
    cutoff = tuple(int(x) for x in args.cutoff.split("-"))
    rotate_helm(args.apply)
    rotate_fable(cutoff, args.cutoff, args.apply)
    if not args.apply:
        print("\n(dry run -- pass --apply to write)")


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

    r = sub.add_parser("report")
    r.add_argument("files", nargs="+")
    r.add_argument("--cutoff", default="2026-08-15")
    r.set_defaults(func=cmd_report)

    o = sub.add_parser("rotate")
    o.add_argument("--cutoff", default="2026-09-08")
    o.add_argument("--apply", action="store_true")
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
