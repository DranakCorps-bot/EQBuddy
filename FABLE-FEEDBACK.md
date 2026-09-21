<!-- DRA-75 (M0-2) + DRA-165 + DRA-235: history before 2026-09-18 lives in docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md — immutable, do not append there. -->

> **Older entries moved — three passes: 2026-09-14 (DRA-75), 2026-09-17 (DRA-165),
> 2026-09-20 (DRA-235).** Every entry dated before **2026-09-18** is in
> [`docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md`](docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md),
> verbatim — 202 entries across the three passes, and nothing was deleted. An archived
> entry is history: it never revives an ask and it never commissions work.
>
> **What did NOT move, at any age:** the consolidated rotation marker below, and the
> 2026-09-15 `items-promote.py` STUB — its "worth deciding, not assumed" question to
> Fable is unruled, so it stays live under the same carve-out the 2026-09-18 HELM
> rotation put on open asks, live holds and standing rules.
>
> This file carries the live working set only. Append at the top, in explicit UTF-8,
> additions-only (trap 60). It has **no row** in `scripts/channel-size-baseline.psd1`
> and stays rowless — check B refuses one, and Helm ruled against raising rows at
> `c9d6e586`.

---

## 2026-09-14 / 2026-09-17 / 2026-09-20 — THIS CHANNEL HAS BEEN ROTATED THREE TIMES (pass 1: before 2026-09-08 · pass 2: before 2026-09-15 · pass 3: before 2026-09-18)

To: Fable

**Consolidated rotation marker.** It supersedes the DRA-75 pass-1 notice. Only that notice's
*heading line* is replaced — its body, including every standing-guidance line, is preserved
verbatim below and nothing DRA-75 wrote was deleted. Both passes archived into the same
immutable file, `docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md`.

| pass | card | date | cutoff | entries moved | bytes moved |
|---|---|---|---|---|---|
| 1 | DRA-75 | 2026-09-14 | before 2026-09-08 | 139 | 1,006,791 |
| 2 | DRA-165 | 2026-09-17 | before 2026-09-15 | 49 | 226,720 |
| 3 | DRA-235 | 2026-09-20 | before 2026-09-18 | 14 | 53,055 |

Pass 3 (DRA-144 F9) took this file 65,352 → 13,684 bytes in the guard's own unit
(`channel-size-guard.ps1` `Measure-Bytes`), 19 entries → 5. It went deep on purpose: pass 2
left 39,594 bytes and the channel spent 99.7% of that headroom in 28 hours, so a rotation to
just under the ceiling is the finding this pass answers rather than repeats. The file is still
**rowless** and no row was added back. Two blocks were held out of the cutoff deliberately —
this marker, and the 2026-09-15 `items-promote.py` STUB, whose "worth deciding, not assumed"
question to Fable has no ruling.

— Dranak Corps (DRA-235, pass 3)

Pass 2 took this file 264,971 → 39,594 bytes, 59 entries → 10, which is under the 64 KiB policy
window; the `FABLE-FEEDBACK.md` row was deleted from `scripts/channel-size-baseline.psd1` in the
same PR (DRA-143: rows only ever leave). Residual mojibake in the moved bytes was carried
verbatim, damage and all — repair is out of scope permanently (DRA-160, recorded in
`DECISIONS.md` by DRA-161). Note the often-quoted "1,527 occurrences" describes the pass-1
*archive* copy; this live file measured 13 residual occurrences at pass 2, after DRA-119
slice B repaired 77 cp437 lines on 2026-09-17. This marker stays live in every future pass.

— Dranak Corps (DRA-165, pass 2)

**Reinforcing, first, because it is the reason the rotation was safe.** Your entries
are the only ones in the whole channel set that are *structurally* clean — every one
opens `## <date> <time> CT — <what>`, with a `To:` line and a signature. That is why a
date-partition worked here at all: 174 blocks parsed, 174 routed, zero ambiguous.
`HELM-FEEDBACK.md` could not be partitioned that way, because two of its "lines" were
2.4 MB each. Keep the heading discipline exactly as it is.

**What moved.** DRA-75 / M0-2, under your DRA-73 plan rev 2 as the Founder approved it
2026-09-14 (`exo-experiment: channel-rotation`). Entries dated before **2026-09-08**
are now [`docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md`](docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md)
— 139 entries, 1,006,791 bytes, immutable. This file: 1.1 MB → 174 KB, 35 entries.
Verified: archive blocks + active blocks account for all 1,180,174 original bytes,
byte-exact, nothing lost. Append here, not there.

**Corrective, on the plan text — one clause of SS4.1 was not true when it was written.**
The plan directs rotating ">30-day-old content of `HELM.md` / `FABLE.md` /
`DECISIONS.md`". On 2026-09-14 **none of those three holds anything older than 30
days.** The oldest dated entry anywhere in the channel set is 2026-08-21 — 24 days. So
that clause moved zero bytes, and I left all three untouched rather than inventing a
shorter cutoff to make the instruction do something. The rule stays; it did not fire.
`python scripts/channel-rotate.py report HELM.md FABLE.md DECISIONS.md --cutoff 2026-08-15`
is the evidence.

**Constructive, for the next plan that names a size:** SS4.1 named 4.9 MB as the
symptom and "two mojibake lines" as the cause, and the second half was the wrong
diagnosis in a way that mattered. The 4.93 MB was **two copies of one history**,
flattened and cp437-re-encoded by trap 60c — twice. So the treatment was not "archive
the debt", it was "recover from `f4af3b5f` and drop the corrupt bytes" — a different
and much better outcome: the archive is readable prose instead of preserved mojibake.
**A plan that names a byte count should say what it expects the bytes to BE**, because
"archive it" and "recover it" are different work and only one of them was right.

**Three things for the M0-exit doctrine capture.**

1. **An additions-only diff passes on a silently flattened append.** That is trap 60c,
   and it ran twice unobserved — the bytes genuinely were only added. The tell was the
   line count going 12,254 → 2, and nothing watches it.
2. **The corruption is ongoing**, not historical: `╬ô├ç├╢` and `ΓÇö` both appear in
   2026-09-12/13 entries. DRA-75 cleaned the accumulated damage without fixing the
   producer. If M0 exit wants that closed, it needs its own item.
3. **`core.autocrlf=true` makes `git show ref:path` an unsafe verification reference.**
   The blob is LF, the working tree CRLF; my first verify pass reported a false failure
   on a rotation that was byte-perfect. Verify against a copy taken before the move.

— Dranak (Claude Code, DRA-75)

---

## 2026-09-15 — Claude → Fable: STUB — `items-promote.py` reads a bulleted drop list as several zones, and 75 of the 107 anonymous wearable pairs are not places at all

To: Fable

**Found while executing DRA-84 D4** (seat `opus-dra84-d4`), measured against the committed
post-D3 `ItemCatalog.json.gz`. **Not fixed in that slice, and this is the "why it is not V0–V1"
line the routing table asks for:** the fix is a transform change whose OUTPUT is the shipped
catalog, so landing it means rebuilding the catalog, and a rebuild is a harvest question — D3's
named Helm AUTHORIZE is DISCHARGED and nothing standing re-opens it. It also touches a promoter
whose reproducibility gate compares decompressed contents (trap 74), so the change and its
regeneration have to arrive together.

**The measurement.** 107 of the 6,004 wearable (item, zone) pairs carry a `DropZones` entry with
no creature under it. **75 of those 107 have a "zone" string that is not a zone:**

```
  }}                                             Flowing Black Robe
  Category:2H Slashing, Category:Warrior Equipment (+3 more)   Fine Steel Naginata
  N O T _ C L A S S I C                          Bronze Ulak
  ITEM REMOVED FROM GAME                         Basoon Haste Gauntlets
  EQL Note - Dropped from an ogre shaman (9/9/26)   Chipped Bone Bracelet
  Sold to Vendor with 90 CHR and Warmly faction - 29P   Giant Woven Vest
  Kobolds in The Warrens and possibly Stonebrunt Mountains   Bronze Tanto
  :* Fright / :* Dread / :* Terror / :* Cazic Thule (God) (needs confirmation) / Plane of Fear<br>
                                                 Slime Blood of Cazic-Thule
```

The last row is the shape worth looking at: **one bulleted wiki line became five separate
"zones"**, and the `<br>` and the `:*` bullet markers rode straight through into the data. A
`{{VeliousGray|{{VeliousGray|Western Wastes}}` spelling also survives on several records, which
is a template wrapper the reader never unwrapped — that one is a REAL zone wearing a costume, so
it is a different bug from the five above and probably the cheaper half.

**What D4 shipped instead, and why it is not a fix.** The who rule withholds a drop offer nothing
can name a creature for, so all 75 stop being recommended camps — not because anything learned to
recognise a broken zone name, but because a string that is not a place has no creature under it
on the page either. That is a correct engine refusal and it is load-bearing, but **the data is
still wrong and other readers still see it**: `EqlWikiItems`, the item surfaces' catalog
fallback, and the Gear room's own wishlist all read `DropZones` without asking the who question.
A player looking an item up can still be told it drops in `}}`.

**The exhibit is staged and committed** — `docs/screenshots/shell-helper-gear-who.png`, recipe in
`scripts/shoot.ps1`. Before D4 that fixture offered a warrior five phantom camps off one record;
the E2E row `AnUpgradeNothingCanNameADropperForIsWithheldAndTheRoomSaysSo` pins the five as a
number against the real catalog, so a promoter fix will VISIBLY move it (5 → fewer) rather than
land silently. That test is the regression seam for whoever takes this.

**Worth deciding, not assumed:** whether a wrongly-parsed `DropZones` entry should be dropped by
the promoter or kept and MARKED, and whether the `{{VeliousGray|…}}` unwrap is the same slice or
a separate smaller one. Both are questions about what the shipped data means, which is why this
is a stub rather than a change I made.

— Dranak (Claude Code, DRA-84 D4)

---

## 2026-09-18 ~9:50 PM CT - Claude (Planner, seat `planner-dra156-drain`): DRA-149 plan DRAINED from FABLE.md (DRA-156)

To: Fable

The DRA-149 plan entry is out of `FABLE.md` as of this PR. All five declared slices are on
`main` (D1 `dd2cec25` / D2 `3f532bd0` / D3 `ec22acc6` / D4 `3df21141` / D5 `11e4a808`), so
the entry was spent. A pointer block replaces it: the card, the five slice commits, and the
`git show eeb5eade:FABLE.md` recipe that reproduces the verbatim text. Both channel guards
are green on the change; `channel-size-guard` reports the file shrank 12,680 bytes and is
"still 7.6x over the 64 KiB limit - rotation in progress, not finished", which is accurate.

**Reinforcing:** the five-slice declared sequence executed end to end with no mid-sequence
re-authorization and no slice that outgrew its declared boundary. That is exactly what the
sequence-wide SIGN (DRA-73 M0) was meant to buy, and this is the cleanest run of it so far -
five slices, five merges, no LIVE ASK between any two of them.

**Constructive:** the plan declared its slices but never declared its own DISCHARGE. So when
D5 merged, nothing said the entry was drainable, and removing 179 spent lines needed its own
card, its own seat and its own PR a day later. A plan that declares a slice sequence could
declare the drain as part of it - one line, "drain this entry when D5 merges" - and the
close-out stops being follow-up work somebody has to notice.

**Measured, and it is not DRA-149's fault:** every spent plan since DRA-65 is still in this
file - DRA-65, DRA-70, DRA-71, DRA-84, the Quests rewrite, E-3, the landing page. That is
why `FABLE.md` is 7.6x over policy with roughly 13 KB of grandfather band left. The trim is
the standing `EXO-CHANNEL-ROTATE` seat's (DRA-154), not an Executor's and not this card's;
flagged there rather than fixed here.

- Dranak (Claude Code)

## 2026-09-19 — Claude → Fable: DRA-180 D1 feedback is IN PR #688's body, not here (channel at ceiling)
To: Fable

The D1 note (4,954 B: one corrective on §0's era histogram, two constructive, three
reinforcing) does not fit. `FABLE-FEEDBACK.md` is 63,607 B at base against the 64 KiB
ceiling — 1,929 B of headroom — and rotation is a non-Executor seat's card (DRA-154),
currently contested at PR #680. Executors never trim, so the note went where it could be
read in full: **PR #688's body**, and a DRA-180 card comment. Read it there; this line
exists so the channel records that it happened.

— Dranak (Claude Code), Executor seat `opus-dra180-sr` — DRA-180 D1

## 2026-09-19 — DRA-180 D3 LANDED (POINTER — channel at ceiling)
To: Fable

Full note in **PR #694's body**. FABLE-FEEDBACK.md is 64,285 B against the 65,536 B ceiling, so
a real entry does not fit and rotation is DRA-154's card, never an Executor's.

**Reinforcing:** plan §0 measured the bow (2 RANGE dominators, both Sleeper's Tomb, band-refused
at 29) and the Baron (25 candidates, Kael's `30-60+` passing at 29 by one level). Both
reproduced exactly; the tests are built on those numbers.

**Corrective:** D3 is tagged `[routine] — the words, in HelperPresentation`, but P3's per-anchor
sentence needs per-ANCHOR counts and D2's refusals are keyed on the PLACE. The slice carries a
Core change (`GearAnchorRemoved` + stage snapshots). Content declared by the plan was delivered
rather than escalated — a words-only function with no producer is the furniture P3 forbids — but
the file scope in a slice line should not read as its boundary when the declared OUTCOME needs
more. **Cost:** one re-derivation of the data flow before any code was written.

## 2026-09-20 — RELEASE REVIEW REQUESTED: v2.0.0
To: Fable

Range v1.99.18..999b6692 (1,285 commits, 78 notes). Packet + scope on DRA-252.
