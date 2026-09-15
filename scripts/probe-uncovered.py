#!/usr/bin/env python3
"""DRA-75 — final containment proof for the two flattened lines.

Claim being tested:
  line 73 (peeled)      == f4af3b5f blob                        [pure duplicate]
  line 71 (peeled)      == [#564 entry] + f4af3b5f-minus-#562   [one unique entry]
  => the ONLY content unique to 4.93 MB of flattened bytes is the #564 entry.

Comparison is on an ASCII+whitespace-normalized signature, because flattening
turned every newline into a space and mojibake mangled the non-ASCII.
"""

import re
import subprocess
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent

# Found by SIZE, never by index -- every channel append re-numbers them.
FLAT_MIN = 100_000


def peel(data: bytes, rounds: int = 6) -> bytes:
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
    """Whitespace/encoding-immune signature."""
    return re.sub(rb"\s+", b" ", re.sub(rb"[^\x20-\x7e]+", b" ", b)).strip()


# Where the flattened lines live depends on whether DRA-75's rotation has run.
# BEFORE it: in the active channel file. AFTER it: in the archive, kept verbatim
# precisely so this proof stays reproducible from a plain checkout. A cited proof
# that errors out is worse than no proof at all (trap 74's shape).
ARCHIVED = REPO / "docs/ops/claude-archive/channels/2026-Q3/HELM-FEEDBACK.original-flattened.md"
SOURCES = [ARCHIVED, REPO / "HELM-FEEDBACK.md"]

for _src in SOURCES:
    if not _src.exists():
        continue
    _data = _src.read_bytes()
    lines = _data.split(b"\n")
    _idx = [i for i, l in enumerate(lines) if len(l) >= FLAT_MIN]
    if len(_idx) == 2:
        break
else:
    raise SystemExit(
        "could not find the two flattened lines in either "
        f"{ARCHIVED.name} or HELM-FEEDBACK.md -- nothing to prove against."
    )

print(f"source: {_src.relative_to(REPO).as_posix()}")
print(f"flattened lines at {_idx}")
l71, l73 = peel(lines[_idx[0]]), peel(lines[_idx[1]])
lg = subprocess.run(["git", "show", "f4af3b5f:HELM-FEEDBACK.md"],
                    cwd=REPO, capture_output=True).stdout

# Boundary: second heading of line 71 / second heading of last-good.
h71 = list(re.finditer(rb"## 20\d\d-\d\d-\d\d", l71))
hlg = list(re.finditer(rb"(?m)^## 20\d\d-\d\d-\d\d", lg))
cut71, cutlg = h71[1].start(), hlg[1].start()

entry564 = l71[:cut71]
print(f"recovered #564 entry: {len(entry564):,}B")
print(f"last-good #562 entry: {cutlg:,}B")
print()

checks = {
    "line73 == last-good (whole)":            (sig(l73), sig(lg)),
    "line71 tail == last-good minus #562":    (sig(l71[cut71:]), sig(lg[cutlg:])),
}
ok = True
for label, (a, b) in checks.items():
    same = a == b
    ok = ok and same
    print(f"{label:<40} {'MATCH' if same else 'DIFFER'}  ({len(a):,} vs {len(b):,})")
    if not same:
        for i in range(min(len(a), len(b))):
            if a[i] != b[i]:
                print(f"   first divergence @{i}:")
                print(f"     got : {a[max(0,i-70):i+70]!r}")
                print(f"     want: {b[max(0,i-70):i+70]!r}")
                break
        else:
            longer, shorter = (a, b) if len(a) > len(b) else (b, a)
            print(f"   prefix match; extra {len(longer)-len(shorter):,}B: {longer[len(shorter):][:200]!r}")

print()
print("=" * 70)
print(f"CONTAINMENT PROOF {'HOLDS' if ok else 'FAILED'}")
print("=" * 70)
print()
print("=== recovered #564 entry (de-mojibaked, first 1200 chars, ascii-safe) ===")
sys_out = entry564.decode("utf-8", "replace")[:1200]
print(sys_out.encode("ascii", "backslashreplace").decode("ascii"))
