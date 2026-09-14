#!/usr/bin/env python3
"""Promotion shim: rebuild ItemCatalog.json.gz from the items dump.

The build itself is C# (scripts/harvests/itemcatalog-build) so the catalog flows
through the app's OWN item parsers — see items-harvest.py for why. This shim just
lets refresh.py's python-only runner drive it like every other promotion.

`--check` (and anything else) is passed straight through. It rebuilds into memory
and compares the DECOMPRESSED contents with the committed file, writing nothing —
gzip is a container and its bytes depend on which zlib built them, which is how
HarvestedGuides.json.gz went red on CI against an identical payload (trap 74).

It is NOT in scripts/check.ps1, and that is deliberate: cache/items-wikitext.jsonl
is gitignored and is written by fetching ~11k pages, so on every clone and on CI
there is nothing to compare against. A gate that cannot run is a gate nobody
believes. It belongs in the weekly refresh, where the dump exists; without one this
exits 2 and says so rather than passing.
"""

import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent / "itemcatalog-build"

r = subprocess.run(
    ["dotnet", "run", "--project", str(PROJECT), "-c", "Release", "--", *sys.argv[1:]])
sys.exit(r.returncode)
