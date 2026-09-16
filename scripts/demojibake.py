#!/usr/bin/env python3
"""DRA-55 slice 2 - repair CP1252 double-encoded text in the channel ledgers.

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
"""

from __future__ import annotations

import argparse
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


def selftest() -> int:
    bad = 0
    for i, (src, want) in enumerate(SELFTEST_VECTORS, 1):
        got = repair_line(src)
        ok = got == want
        bad += 0 if ok else 1
        print(f"  vector {i}: {'ok' if ok else 'FAIL'}  {src!r} -> {got!r}")
    print("selftest:", "ok" if not bad else f"{bad} FAILED")
    return 1 if bad else 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("files", nargs="*")
    a = ap.parse_args()

    if a.selftest:
        return selftest()
    if not a.files or not (a.check or a.write):
        ap.error("need --check or --write plus at least one file (or --selftest)")

    total_m = total_r = 0
    for f in a.files:
        p = Path(f)
        data = p.read_bytes()
        out, m, r = repair_bytes(data)
        total_m += m
        total_r += r
        print(f"{f:24s} marker_lines={m:5d} repaired={r:5d} bytes {len(data)} -> {len(out)}")
        if a.write and out != data:
            p.write_bytes(out)
    print(f"{'TOTAL':24s} marker_lines={total_m:5d} repaired={total_r:5d}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
