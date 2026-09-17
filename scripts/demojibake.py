#!/usr/bin/env python3
"""DRA-55 slice 2 / DRA-119 slice B - repair double-encoded text in the channel ledgers.

Two DIFFERENT defects live in these files, produced by two different code pages.
They need two different inverses, and neither arm can repair the other's damage:

    --mode cp1252   DRA-55.  UTF-8 bytes decoded through cp1252 and re-encoded.
    --mode cp437    DRA-119. UTF-8 bytes decoded through cp437 and re-encoded.

`--mode cp1252` is the default and is byte-for-byte the original DRA-55 tool.

The defect: UTF-8 bytes were decoded through the Windows ANSI code page
(cp1252) and re-encoded as UTF-8, so a correct em dash `E2 80 94` became
`C3 A2 E2 82 AC E2 80 9D`. See DRA-55 for the byte-level proof.

The repair is the inverse of that trip and nothing else:

    text.encode('cp1252') -> the original UTF-8 bytes -> .decode('utf-8')

It is applied PER LINE, and only where the round trip is total: a line is
rewritten only if it re-encodes to cp1252 without loss AND the result decodes
as valid UTF-8 AND the result is actually shorter (a real un-doubling). Any
line that fails any of those is left byte-identical. That is what makes this
safe to run over a file that is only partly corrupt - the intentional mojibake
quotes in BEVEL.md and SCRIBE-FEEDBACK.md are outside the repair set and, even
if they were not, a single-encoded line does not round-trip and is passed
through untouched.

Line splitting is on b'\n' over BYTES and the terminators are preserved, so a
file with CRLF, a missing final newline, or a lone CR inside a line comes out
with the same line structure it went in with.

Usage:
    demojibake.py --check  FILE...   report per-file marker/repair counts, write nothing
    demojibake.py --write  FILE...   repair in place
    demojibake.py --selftest         prove the round trip on built-in vectors

    ... any of the above with `--mode cp437` to run DRA-119's arm instead.
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

# The marker is the double-encoded lead-in shared by every U+2xxx punctuation
# character that went through this trip (em dash, en dash, curly quotes,
# ellipsis). Built from bytes, never from a literal glyph in this source -
# a literal would itself be re-encoded by the very defect being repaired.
MARKER = b"\xc3\xa2\xe2\x82\xac"


# cp1252 leaves five byte slots undefined (0x81, 0x8D, 0x8F, 0x90, 0x9D), but the
# Windows conversion that CAUSED this corruption passes them straight through to
# the matching C1 code point. Strict cp1252 therefore refuses to re-encode the
# very text it produced: a curly close-quote doubles to a run ending `C2 9D`, and
# U+009D is one of the five. Encoding through this "sloppy" table is what lets a
# right-double-quote and an ellipsis be repaired at all - without it, 29 of
# WhatsNew.json's lines and every quoted ledger line stay corrupt.
SLOPPY = {0x81: b"\x81", 0x8D: b"\x8d", 0x8F: b"\x8f", 0x90: b"\x90", 0x9D: b"\x9d"}

# One pass undoes one trip. `f640532b` ran the trip over text that some lines had
# ALREADY been through, so HELM.md carries lines at depth 2 - CLAUDE.md's own
# trap 54 note spells that shape out. Iterate to a fixed point rather than
# assuming a depth.
MAX_PASSES = 5


def _sloppy_cp1252(text: str) -> bytes:
    """Encode as cp1252, passing the five undefined slots through as their byte."""
    out = bytearray()
    for ch in text:
        try:
            out += ch.encode("cp1252")
        except UnicodeEncodeError:
            b = SLOPPY.get(ord(ch))
            if b is None:
                raise
            out += b
    return bytes(out)


def _one_pass(line: bytes) -> bytes:
    """Undo a single cp1252 double-encode trip, or return the input unchanged."""
    if MARKER not in line:
        return line
    try:
        fixed = _sloppy_cp1252(line.decode("utf-8")).decode("utf-8").encode("utf-8")
    except (UnicodeDecodeError, UnicodeEncodeError):
        # Lossy either way: a character not even the sloppy table can spell, or
        # bytes that are not valid UTF-8 once un-doubled. Leave it alone.
        return line
    if len(fixed) >= len(line):
        # A genuine un-doubling always shrinks. Anything else is not the defect.
        return line
    return fixed


def repair_line(line: bytes) -> bytes:
    """Return the fully un-doubled line, or the input unchanged if it never round-trips."""
    for _ in range(MAX_PASSES):
        nxt = _one_pass(line)
        if nxt == line:
            break
        line = nxt
    return line


def repair_bytes(data: bytes) -> tuple[bytes, int, int]:
    """Repair a whole file's bytes. Returns (out, marker_lines, repaired_lines)."""
    parts = data.split(b"\n")
    marker = 0
    repaired = 0
    out = []
    for part in parts:
        # Hold the CR of a CRLF out of the round trip so line structure is exact.
        cr = part.endswith(b"\r")
        body = part[:-1] if cr else part
        if MARKER in body:
            marker += 1
        fixed = repair_line(body)
        if fixed != body:
            repaired += 1
        out.append(fixed + b"\r" if cr else fixed)
    return b"\n".join(out), marker, repaired


# ======================================================================================
# DRA-119 - the cp437 arm
# ======================================================================================
#
# Same shape of damage, different table: UTF-8 bytes decoded through cp437 and
# re-encoded. An em dash `E2 80 94` becomes U+0393 U+00C7 U+00F6, and that form
# put through the trip a SECOND time becomes U+256C U+00F4 U+251C U+00E7 U+251C
# U+2562. Both depths are live in the ledgers, so this arm iterates to a fixed
# point exactly as the cp1252 arm does.
#
# The inverse is the cp437 codec itself - a TABLE, not a special case for the em
# dash. 68 of the 69 depth-1 lines carry a dash, but HELM-FEEDBACK.md:874 carries
# a section sign (U+252C U+00BA -> U+00A7) and a dash-only rule leaves it corrupt
# while reporting success.
#
# Three things this arm does that the cp1252 arm does not, each one load-bearing:
#
# 1. It repairs maximal NON-ASCII RUNS, not whole lines. Whole-line was the first
#    design and it silently skipped BEVEL-FEEDBACK.md:1117 - a line holding 20
#    corrupt strings - because one unrelated character elsewhere on the line has
#    no cp437 spelling at all, so the whole line failed to encode.
#
# 2. Every byte of an inverted run must be >= 0x80. U+00A7 is legitimately IN
#    cp437, at byte 0x15, so it re-encodes happily and SHRINKS - which is exactly
#    what the cp1252 arm's "a real un-doubling always shrinks" test looks for.
#    Without this check the second pass over :874 would eat the section sign this
#    arm had just correctly restored, and turn it into a control character.
#
# 3. It refuses to touch four enumerated lines. See PRESERVE below.

# The two depth markers, for COUNTING only - the repair itself is driven by the
# round trip, not by these, which is how :874 gets repaired at all. Built from
# bytes for the same reason MARKER is.
CP437_MARKER_D1 = b"\xce\x93\xc3\x87"
CP437_MARKER_D2 = b"\xe2\x95\xac\xc3\xb4"


def _cp437_invert_run(seg: str) -> str | None:
    """Undo one cp437 trip over a single non-ASCII run, or None if it is not the defect."""
    try:
        raw = seg.encode("cp437")
    except UnicodeEncodeError:
        # A character with no cp437 spelling cannot have come out of a cp437
        # decode, so this run is not the damage.
        return None
    if any(b < 0x80 for b in raw):
        # Guard 2 above: a genuine un-trip is made of UTF-8 lead and continuation
        # bytes, every one of which is >= 0x80.
        return None
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return None


def _nonascii_runs(text: str):
    """Yield (start, end) for each maximal run of non-ASCII characters."""
    i, n = 0, len(text)
    while i < n:
        if ord(text[i]) < 0x80:
            i += 1
            continue
        j = i
        while j < n and ord(text[j]) >= 0x80:
            j += 1
        yield i, j
        i = j


def repair_line_cp437(line: bytes) -> bytes:
    """Return the fully un-tripped line, or the input unchanged."""
    try:
        text = line.decode("utf-8")
    except UnicodeDecodeError:
        return line
    for _ in range(MAX_PASSES):
        out: list[str] = []
        prev = 0
        changed = False
        for a, b in _nonascii_runs(text):
            got = _cp437_invert_run(text[a:b])
            if got is None or got == text[a:b]:
                continue
            out.append(text[prev:a])
            out.append(got)
            prev = b
            changed = True
        if not changed:
            break
        out.append(text[prev:])
        text = "".join(out)
    return text.encode("utf-8")


# --------------------------------------------------------------------------------------
# The preserve list
# --------------------------------------------------------------------------------------
#
# Four of the 81 repairable lines are not damage. They are DRA-75's own report
# text QUOTING both mojibake families as code spans, e.g.
#
#     `<depth-2 form>` and `<depth-1 form>` both appear in 2026-09-12/13 entries
#
# Repairing them collapses both spans to the same em dash and the sentence reads
# "`-` and `-` both appear in" - the evidence these ledgers keep ABOUT this defect,
# silently deleted by the tool that was supposed to be fixing it.
#
# DECISIONS.md is the one that catches people. The sentence wraps across 1225-1226
# and only 1225 carries the "the corruption is ongoing" tell, so a line-local test
# for "is this line also talking about mojibake" preserves 1225 and repairs 1226.
# Both are listed, by hand, for that reason.
#
# This is an ASSERTION, not a filter. Each entry is pinned to the sha256 of the
# line it is protecting, and a run REFUSES TO WRITE ANYTHING if either
#
#   - a pinned line is not the line it expected (the ledger moved under the list,
#     so the line numbers no longer mean what they meant), or
#   - a pinned line needs no repair (the entry is protecting nothing, which means
#     the list has gone stale and is no longer evidence that anything was skipped).
#
# A filter that silently matches nothing looks exactly like a filter that worked.
PRESERVE = {
    ("HELM-FEEDBACK.md", 249): "506ad92b3e9b4018c27464d1b418016cf608114e02f0e45ba6f041cd32ea2f43",
    ("FABLE-FEEDBACK.md", 239): "51e22008d07e52a14260aa5d545e96dd7aac4bc011698e3033c1609e5109186c",
    ("DECISIONS.md", 1225): "0913cfdb32cbb3700e99913581884e48c51e325cc3c016020ff6309c97492ac7",
    ("DECISIONS.md", 1226): "24a003fe0f9ed9b9be0217286d4825d6753eea9f6a645d4c766655d05ee8db8e",
}


class PreserveViolation(Exception):
    """A pinned line is not what the preserve list says it is."""


def repair_bytes_cp437(data: bytes, name: str) -> tuple[bytes, int, int, int]:
    """Repair one file. Returns (out, marker_lines, repaired_lines, preserved_lines).

    `name` is the basename the preserve list is keyed by. Raises PreserveViolation
    rather than writing anything questionable.
    """
    parts = data.split(b"\n")
    marker = repaired = preserved = 0
    out = []
    for idx, part in enumerate(parts, 1):
        cr = part.endswith(b"\r")
        body = part[:-1] if cr else part
        if CP437_MARKER_D1 in body or CP437_MARKER_D2 in body:
            marker += 1
        fixed = repair_line_cp437(body)
        pin = PRESERVE.get((name, idx))
        if pin is not None:
            got = hashlib.sha256(body).hexdigest()
            if got != pin:
                raise PreserveViolation(
                    f"REFUSING to write - {name}:{idx} is not the line the preserve "
                    f"list pins (sha256 {got[:16]}..., expected {pin[:16]}...). "
                    f"The file moved under the list; re-derive the line numbers."
                )
            if fixed == body:
                raise PreserveViolation(
                    f"REFUSING to write - {name}:{idx} is on the preserve list but "
                    f"needs no repair - the entry is protecting nothing. Drop it or "
                    f"re-derive the list."
                )
            preserved += 1
            fixed = body
        elif fixed != body:
            repaired += 1
        out.append(fixed + b"\r" if cr else fixed)
    return b"\n".join(out), marker, repaired, preserved


SELFTEST_VECTORS = [
    # (corrupt bytes, expected repair)
    (b"PR #381 OE-8 \xc3\xa2\xe2\x82\xac\xe2\x80\x9d the door", b"PR #381 OE-8 \xe2\x80\x94 the door"),
    # already-correct text is untouched
    (b"PR #381 OE-8 \xe2\x80\x94 the door", b"PR #381 OE-8 \xe2\x80\x94 the door"),
    # pure ASCII is untouched
    (b"plain ascii line", b"plain ascii line"),
    # empty line is untouched
    (b"", b""),
]


CP437_SELFTEST_VECTORS = [
    # (corrupt bytes, expected repair)
    # depth 1: em dash through cp437 once
    (b"night-3 ACK \xce\x93\xc3\x87\xc3\xb6 DRA-53", b"night-3 ACK \xe2\x80\x94 DRA-53"),
    # depth 2: the same form put through the trip again. Proves the fixed-point
    # loop, which a single pass would leave half-repaired rather than untouched.
    (b"Soft merge #582 \xe2\x95\xac\xc3\xb4\xe2\x94\x9c\xc3\xa7\xe2\x94\x9c\xe2\x95\xa2 YES",
     b"Soft merge #582 \xe2\x80\x94 YES"),
    # not a dash: HELM-FEEDBACK.md:874's section sign. A dash-only rule scores
    # this line 0 and leaves it corrupt.
    (b"extends PRD \xe2\x94\xac\xc2\xba12; own-room", b"extends PRD \xc2\xa712; own-room"),
    # ... and the repaired form is a FIXED POINT. U+00A7 encodes to cp437 0x15,
    # so without the >= 0x80 guard this pass would eat it into a control char.
    (b"extends PRD \xc2\xa712; own-room", b"extends PRD \xc2\xa712; own-room"),
    # a run this arm cannot encode is left alone, and - the L1117 lesson - it does
    # NOT suppress repair of a different run on the same line.
    (b"\xce\x93\xc3\x87\xc3\xb6 buff \xe2\x98\x85 timer \xce\x93\xc3\x87\xc3\xb6 x",
     b"\xe2\x80\x94 buff \xe2\x98\x85 timer \xe2\x80\x94 x"),
    # cp1252 damage is the OTHER defect and passes through this arm untouched
    (b"PR #381 OE-8 \xc3\xa2\xe2\x82\xac\xe2\x80\x9d the door",
     b"PR #381 OE-8 \xc3\xa2\xe2\x82\xac\xe2\x80\x9d the door"),
    # already-correct text, pure ASCII, and an empty line are all untouched
    (b"night-3 ACK \xe2\x80\x94 DRA-53", b"night-3 ACK \xe2\x80\x94 DRA-53"),
    (b"plain ascii line", b"plain ascii line"),
    (b"", b""),
]


def _run_vectors(vectors, fn) -> int:
    bad = 0
    for i, (src, want) in enumerate(vectors, 1):
        got = fn(src)
        ok = got == want
        bad += 0 if ok else 1
        print(f"  vector {i}: {'ok' if ok else 'FAIL'}  {src!r} -> {got!r}")
    return bad


def selftest(mode: str) -> int:
    if mode == "cp437":
        bad = _run_vectors(CP437_SELFTEST_VECTORS, repair_line_cp437)
        bad += _preserve_selftest()
    else:
        bad = _run_vectors(SELFTEST_VECTORS, repair_line)
    print(f"selftest ({mode}):", "ok" if not bad else f"{bad} FAILED")
    return 1 if bad else 0


def _preserve_selftest() -> int:
    """Prove both refusal arms fire. A preserve list that cannot fail is a filter."""
    bad = 0
    corrupt = b"quoting \xce\x93\xc3\x87\xc3\xb6 the marker"
    pin = hashlib.sha256(corrupt).hexdigest()
    saved = dict(PRESERVE)
    try:
        # arm 1: the pinned line is not the line we expected
        PRESERVE.clear()
        PRESERVE[("T.md", 1)] = "0" * 64
        try:
            repair_bytes_cp437(corrupt, "T.md")
            print("  preserve arm 1: FAIL  wrong-line pin did not refuse")
            bad += 1
        except PreserveViolation as e:
            print(f"  preserve arm 1: ok  {str(e)[:72]}...")
        # arm 2: the pinned line is correct but needs no repair
        clean = b"a clean ascii line"
        PRESERVE.clear()
        PRESERVE[("T.md", 1)] = hashlib.sha256(clean).hexdigest()
        try:
            repair_bytes_cp437(clean, "T.md")
            print("  preserve arm 2: FAIL  no-op entry did not refuse")
            bad += 1
        except PreserveViolation as e:
            print(f"  preserve arm 2: ok  {str(e)[:72]}...")
        # and the happy path: a correctly pinned, genuinely corrupt line survives
        PRESERVE.clear()
        PRESERVE[("T.md", 1)] = pin
        out, _, rep, pres = repair_bytes_cp437(corrupt, "T.md")
        if out == corrupt and rep == 0 and pres == 1:
            print("  preserve arm 3: ok  pinned corrupt line left byte-identical")
        else:
            print(f"  preserve arm 3: FAIL  out={out!r} repaired={rep} preserved={pres}")
            bad += 1
    finally:
        PRESERVE.clear()
        PRESERVE.update(saved)
    return bad


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--mode", choices=("cp1252", "cp437"), default="cp1252")
    ap.add_argument("files", nargs="*")
    a = ap.parse_args()

    if a.selftest:
        return selftest(a.mode)
    if not a.files or not (a.check or a.write):
        ap.error("need --check or --write plus at least one file (or --selftest)")

    total_m = total_r = total_p = 0
    # Repair every file into memory BEFORE writing any of them. A PreserveViolation
    # on file 3 must not leave files 1 and 2 already rewritten on disk.
    staged = []
    for f in a.files:
        p = Path(f)
        data = p.read_bytes()
        if a.mode == "cp437":
            out, m, r, pres = repair_bytes_cp437(data, p.name)
        else:
            out, m, r = repair_bytes(data)
            pres = 0
        total_m += m
        total_r += r
        total_p += pres
        staged.append((p, data, out))
        print(f"{f:24s} marker_lines={m:5d} repaired={r:5d} preserved={pres:3d} "
              f"bytes {len(data)} -> {len(out)}")
    if a.write:
        for p, data, out in staged:
            if out != data:
                p.write_bytes(out)
    print(f"{'TOTAL':24s} marker_lines={total_m:5d} repaired={total_r:5d} "
          f"preserved={total_p:3d}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except PreserveViolation as exc:
        # Exit 2, distinct from a selftest failure, and nothing has been written.
        print(str(exc), file=sys.stderr)
        sys.exit(2)
