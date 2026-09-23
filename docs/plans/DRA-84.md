<!-- DRA-287-SPLIT -->
*Moved verbatim out of `FABLE.md` by DRA-287 on 2026-09-21, byte for byte. Not reflowed, re-dated, re-worded or summarised. `FABLE.md` holds the index row that points here; the operating charter and the code-cited section anchors stayed in `FABLE.md`. Card: **DRA-84 D4**.*

## 2026-09-15 — STUB from Claude (DRA-84 D4): `items-promote.py` turns one bulleted drop list into five "zones" — V2, and it cannot be taken as V0–V1

To: Fable

**The problem.** The shipped catalog's `DropZones` carries 75 wearable (item, zone) pairs whose
zone string is not a place: `}}`, `Category:2H Slashing`, `N O T _ C L A S S I C`,
`ITEM REMOVED FROM GAME`, vendor-price prose, and — the shape that names the bug —
`Slime Blood of Cazic-Thule`'s five entries `Plane of Fear<br>`, `:* Fright`, `:* Dread`,
`:* Terror`, `:* Cazic Thule (God) (needs confirmation)`, which are one bulleted wiki line read as
five locations. A `{{VeliousGray|{{VeliousGray|Western Wastes}}` spelling also survives on several
records — a REAL zone wearing a template wrapper, which is a different and probably cheaper bug.

**The evidence, and the exhibit.** Numbers, the full sample and the reader-by-reader blast radius
are in `FABLE-FEEDBACK.md` under the same date. Staged picture:
`docs/screenshots/shell-helper-gear-who.png`. The E2E row
`AnUpgradeNothingCanNameADropperForIsWithheldAndTheRoomSaysSo` pins the count at 5 against the
real catalog, so a fix moves a committed number rather than landing silently.

**Why it is not V0–V1.** The change is in a transform whose output is the shipped
`ItemCatalog.json.gz`, so landing it means REBUILDING the catalog — and a rebuild is a harvest
question. D3's named Helm AUTHORIZE is discharged and nothing standing re-opens it. The promoter's
reproducibility gate compares decompressed contents (trap 74), so the parser change and its
regeneration have to arrive in one commit or the gate reddens on a file nobody changed on purpose.
There is also a real decision in it that is not mine: whether a wrongly-parsed entry is DROPPED or
kept and MARKED — a question about what the shipped data means.

**The one-question test, run honestly:** there is no single answer from David that turns this into
V1, because the blocker is a harvest AUTHORIZE plus a data-semantics call, not a missing preference.

**What already ships, so nobody re-implements it.** DRA-84 D4's who rule withholds any drop offer
nothing can name a creature for, so none of the 75 is recommended as a camp any more. That is an
engine refusal, not a fix: `EqlWikiItems`, the item surfaces' catalog fallback and the Gear room's
wishlist all still read `DropZones` directly, so a player looking an item up can still be told it
drops in `}}`.

---

*Older entries — 2026-09-14 back to 2026-08-2x — are in
`docs/ops/claude-archive/channels/2026-Q3/FABLE.md`. No other items.*
