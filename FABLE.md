# FABLE.md — the V2–V3 plan inbox (index)

**Split at source 2026-09-21 by DRA-287**, the successor to DRA-259 (DRA-144 F10). Plan
bodies now live one file each under `docs/plans/`; this file is the **index** plus the two
things that must never leave it. Nothing was reflowed, re-dated, re-worded or summarised —
every moved entry is a byte slice of the file DRA-259 left behind.

**Why, and not merely "it was long".** DRA-259 rotated this file 501,593 B → 39,850 B and
discharged its grandfather row, which puts it on `channel-size-guard`'s **ceiling arm with
no tolerance band at all**: 65,536 B, and `check B` refuses any pull request that writes the
row back. At the measured append rate that was under two days of green before Fable's own
plan-writing pull requests started failing CI. A second rotation buys days. Moving the plan
bodies out means the only thing that ever lands in this file again is **one index row**, so
the append rate collapses and the problem is over rather than deferred. Rotation of this
file becomes a rename under `docs/plans/`, not a byte-splice.

```
before (DRA-259 tip)   39,850 B
  preamble              2,226 B   rewritten (this text)
  charter               5,260 B   KEPT verbatim
  re-pinned anchors     6,287 B   KEPT verbatim
  six plan entries     26,077 B   MOVED verbatim to docs/plans/
```

## Where a plan goes now

**Fable writes the plan body to `docs/plans/DRA-<n>.md`** — one file per card, named for the
Paperclip card it plans — and adds **one row** to the index at the bottom of this file. Do
not paste a plan body into this file. A stub with no card yet takes a `STUB-<slug>.md` name
until one exists; there is one of those below.

`docs/plans/` is **not** a rostered channel file, so neither size guard measures it and a
plan may be as long as it needs to be. This file is rostered, and staying an index is what
keeps it under the ceiling.

**The never-rotate floor of this file, so a future cut does not have to rediscover it.**
Two things below are not plan bodies and must not be moved or re-worded:

1. **The four charter sections** — `When this file is in play`, `How Fable reaches Helm`,
   `How Claude calls Fable`, `Item shape`. They are undated standing process ("*This is
   standing process, not a V2–V3 plan item*"). DRA-259 moved them to the top of the file so
   a date cut could not reach them; they are still here, byte for byte.
2. **The three re-pinned section anchors** under "Standing rules re-pinned from rotated
   entries" — `§4` (the SCREEN mutex), `§3` (TR-2) and `plan §3, DRA-48` (the landing page's
   visual tokens). **Ten** locations in `scripts/`, `tests/`, `installer/` and `site/` cite
   those three numbers, two of them from inside runtime error strings a user reads. The
   anchors are ordinals INSIDE entries, not literal section headings, so no marker sweep and
   no grep for a section number finds them. This is the `exo-experiment:` failure of DRA-231
   (F6) one class up.

   **The split made `FABLE.md §n` LESS ambiguous, not more.** Before it, the moved entries
   carried their own `### 3.` and `### 4.` headings, so a reader resolving `FABLE.md §4` had
   several candidates. They are now in `docs/plans/`, and the only `### 3.` / `### 4.` left
   in this file are the cited ones.

Anything still genuinely open at any age stays reachable from the index. Judge a
LIVE ASK / PARK / HOLD line by **residence**, not by the marker: most of this file's marker
lines say "LIVE ASK in `HELM-FEEDBACK.md`" and are references to an ask that lives
elsewhere. An entry that *announces* a rule archives safely; one that *constitutes* it stays.

Entries archived by DRA-259 are in `docs/ops/claude-archive/channels/2026-Q3/FABLE.md`,
unaltered and in their entries.

---
## When this file is in play

**V2–V3 only.** Cross-cutting architecture, significant refactor, ambiguous root cause,
security/privacy/migration, complex parallel decomposition.

Fable 5 writes the plan. Helm last-looks. **Claude executes it** — unless the plan carries a
`needs-david:` line, which names a decision from the consequence list in `CLAUDE.md`
("What needs David, and what does not") and waits for him to answer THAT. David reads this
file as a digest he can veto; the release gate is where anything he dislikes is caught.

**Approval by exception, not by gate** (David, 2026-08-22). The old shape — Fable plans,
David marks `approved`, Claude executes — had him reading every plan in full to say yes to
work the release gate already protected him from. The first two plans through here were
approved without a word changed.

**V0–V1 does not belong here.** Cosmetic, mechanical, localized, straightforward work
stays one Claude loop. Do not pay a planning-handoff tax without reason. The test before
stubbing: *if David answered one question right now, could this be V1?* If yes, ask the
question instead.

This is not a fourth gate on Scribe intake or Bevel critique. Those files stay their
own inboxes. Org-level proposals do not go in this file.

There is no Fable Grok Bot. Point Fable 5 at this file.

## How Fable reaches Helm

**You reach Helm by webhook, not by David** (David, 2026-08-24). After you write
or change `HELM-FEEDBACK.md` and push it (a LIVE ASK or a loop-close Helm must
see), trigger the private wake:

`gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane`

Optional: `-f reason="HELM-FEEDBACK.md changed"`. File writes are not a wake. A
push alone is not. The URL and key are Actions secrets on that private repo,
never in this file. Do not paste them here.

Helm last-looks, then pages Dranak to run `claude -p` if the executor needs a
kick. David is not the courier. Page him only for a consequence-list door.

This is standing process, not a V2–V3 plan item. Do not stub it as a work item.

## How Claude calls Fable

Claude does not start you (David, 2026-08-24). Claude files a `To: Fable` note
(this file or `FABLE-FEEDBACK.md`), pushes, and wakes Helm with the same
`gh workflow run` command above. Helm last-looks and pages Dranak to start a
Fable-shaped `claude -p` in this repo. You plan; Claude executes. Do not wait
for David to carry the ask.

## Item shape

- **Priority:** `ready` (plan written; Claude may take it) Â· `needs-david: <the decision>`
  (names ONE consequence-list decision; waits for his answer, never for a generic "approve") Â·
  `someday`. David may still write `approved` as an explicit mark; it means `ready`.
- **Class:** `V2` or `V3` (if you cannot say why it is not V0–V1, it does not go here)
- **Source:** discussion/issue, Bevel/Scribe item, or David's words
- **Plan:** architecture, risks, decomposition, verification, what is out of scope
- **Bevel pre-design: yes / no, because…** — required on any plan with a presentation PR.
  Fable plans the architecture; Bevel judges whether the player can still do the job. The
  executor treated a plan as the design pass once (2026-08-22) and should not have had to guess.
- **Shot offline: yes / no** — for any staged screenshot. `shoot.ps1` is NOT offline by
  default, so a "not read yet" prediction for an unseeded wiki page is wrong before it runs.
- **Column budgets: <the fixed widths this touches>** — for any plan that puts a new string
  into an existing surface. The Sky glance overflowed a fixed 150 px column and was found from
  a screenshot after it was built; measure before writing the string.
- **Guards run eight times** — a new test that guards a fix is not green until it has passed
  eight consecutive runs. `SettingsClobberTests` was flaky one run in three from the hour it
  shipped and would have passed any single review.
- **What clamps it: <the stored setting's other readers>** — for any formula that takes a
  persisted value as an input. The 320-cap plan named `ContentHeight` as "what the player
  dragged" when `SectionMaxHeight` clamps it to the work area first, so the body could claim
  room the stack was never granted (executor, 2026-08-31). One grep for the setting's readers
  before it becomes a plan input.
- **Must-list rows on this surface: <the `GameCommandsTests` / `ImportReportReachesASurfaceTests`
  rows>** — for any plan that reshapes a surface carrying one. "Defer to the window's scroller"
  read as "delete the inner scroller" until the executor found the â§‰ that scroller pins is a
  trap-34 row on that exact tab (2026-08-31). Name the row and the plan cannot un-pin it.
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.
- **Decided without asking:** the implementation calls the plan made that could have gone the
  other way, one line each — these go to `DECISIONS.md` when the item is taken.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`. Fable last-looks the
executed diff (H4) and answers in the same file; a defect found there is a V1 item for the
next loop, not a reopening of the plan.

---

## Standing rules re-pinned from rotated entries

The three blocks below are **verbatim copies**, byte for byte, of `###` sections whose
parent entries were archived by this rotation. They are here because live code cites them
**by section number**, and `FABLE.md §n` does not resolve to a number that is not in this
file. Each carries the provenance of the entry it came from, because the numbers are
ambiguous on their own — this file has always had several `### 4.` and `### 3.` headings,
and two different plans each call their own third section `§3`. The originals are still in
`docs/ops/claude-archive/channels/2026-Q3/FABLE.md`, in their entries, unaltered.
**Do not re-word these.** A citation resolves to the text or it does not (trap 73).

**Re-pin 1 — "`FABLE.md` §4", the SCREEN mutex.** Verbatim from `### 4. Concurrency on
David2026`, formerly under `## E-3 completion — the parallel build-out plan (Fable,
2026-09-05, on tip d55de151)`. Cited by six locations, two of them inside runtime error
messages the user sees: `scripts/shoot.ps1:4866` and `:4905`, `scripts/drag-verify.ps1:357`
and `:369`, `tests/EQBuddy.E2E/ScreenLock.cs:142`, `tests/EQBuddy.E2E/README.md:55`.

### 4. Concurrency on David2026

**Recommended: 3 concurrent `claude -p` steady state, 4 peak.** Concretely: two product
lanes building .NET in their own worktrees (own `obj/`/`bin/` — trap 18 stays per-tree), one
docs/design lane (no build contention), and at peak a fourth that is Bevel/Fable channel
work (no build at all). Beyond that the cost is not CPU, it is Helm: every product PR takes
a last-look, and five simultaneous asks serialize inside Helm's mailbox anyway — a queue in
front of the signer is parallelism spent on waiting.

**The one hard mutex is the SCREEN.** `e2e-windows` and `shoot.ps1` launch and stand down
the real exe, enumerate windows by title+pid, and own the desktop (traps 24/51/53). Exactly
one lane holds the screen at a time; the others push and let CI's `e2e-windows` answer
(it runs on every push since 2026-09-04). Dranak enforces this by kick order, not by tooling:
a lane's kick prompt says whether it has the screen.

**Re-pin 2 — "`FABLE.md` §3", TR-2.** Verbatim from `### §3 Updater vs separate download`,
formerly under `## 2026-09-07 ~2:00 PM CT — Fable: V1 (MIT) → EQBUDDY EVOLVED (Windows)
TRANSITION PRODUCT plan (owner ask ~1:44 PM CT / PR #397)`, Helm-signed #399. Cited as
standing authority by three locations: `scripts/evolved-channel-guard.ps1:48`,
`scripts/release.ps1:143`, `installer/EQBuddyEvolved.iss:6`.

### §3 Updater vs separate download — DECIDED by the frozen contract: separate download

- **`EQBuddyEvolvedSetup.exe`, a NEW Inno AppId, install dir `{autopf}\EQBuddy Evolved`, its
  own Start-menu shortcut** — the "heavier version" `install-local.ps1` already names as the
  right move when Evolved becomes the daily driver. Signed and verified exactly like
  everything else; the signing rule does not bend for a transition.
- **Why not the v1 updater staging it:** (a) an asset named `EQBuddySetup.exe` on the v2
  release would be downloaded and run by every Windows v1 install's existing update flow — a
  major-line replacement with no consent moment; (b) with v1's AppId it would install over
  `{autopf}\EQBuddy` and inherit the v1 profile in place, which `LEGACY-V1.md` publicly
  promises we will not do; (c) v1 cannot be patched to behave differently — the contract is
  frozen.
- **`EQBuddySetup.exe` is therefore a RESERVED NAME belonging to the v1 line forever.** The
  deployed updaters match on it the way `shot.ps1` matched on window titles (trap 53) — the
  name is the identity, and only one side of it has a compiler. Guard row (TR-2):
  `evolved-channel-guard.ps1` fails any 2.x path that produces an artifact with that name, and
  asserts the installer script's AppId is the NEW one (its existing check-1 fourth member
  flips from "never build the installer" to "only ever build it under the new identity").
- **Accepted, named cost:** a kept v1 install's banner will say a newer version exists,
  permanently, linking the release page. That is honest — a newer version does exist — and
  the only lever on its wording is D3.
- **Dual-install is a FEATURE of this decision, not a defect:** the player who tries Evolved
  keeps a working v1 to fall back to, which is what makes the transition low-stakes enough to
  say yes to. The costs it creates are §5's dual-run risks, handled there.

**Re-pin 3 — "`FABLE.md` plan §3, DRA-48", the landing page's visual tokens.** Verbatim
from `### 3. Visual tokens — Example 1 chrome mapped onto Turquoise`, formerly under
`## 2026-09-10 ~12:40 PM CT — Fable: EVOLVED LANDING PAGE on GitHub Pages — the plan
(DRA-48, Founder ask 2026-09-10)`. Cited by `site/assets/css/landing.css:2`, which is a
shipped stylesheet whose token values this section defines. **Not named in the DRA-259
card** — found by re-deriving the citation set from the repo rather than taking the card's
list of seven, and it is the same defect class the card was filed to prevent.

### 3. Visual tokens — Example 1 chrome mapped onto Turquoise

From `ThemePalettes.cs["Turquoise"]` (the shoot.ps1 default since the Founder lock):

| Token | Value | Replaces (Ex1) |
|---|---|---|
| `--bg` | `#131C1C` | `#050816` |
| `--deep` | `#0C1312` | `#02040d` |
| `--panel` / `--panel2` | `rgba(22,33,31,.80)` / `rgba(26,39,37,.85)` (from `#16211F`/`#1A2725`) | slate panels |
| `--line` | `rgba(224,242,239,.14)` | slate line |
| `--text` | `#E0F2EF` | `#f8fafc` |
| `--muted` | `#87A6A0` | `#a9b6ca` |
| `--accent` | `#3FCFBE` | `--cyan #22d3ee` |
| `--accent2` | `#35AB9E` | `--blue #60a5fa` |
| `--good` | `#6FBF7F` | `--green` |
| `--warn` | `#E0A030` | `--amber` |
| `--alert` | `#D9634F` | `--rose` (sparingly; alert copy only) |

Radial glows: teal at 11%/10% and 88%/30%, desaturated grey-green at 50%/88% — the violet
glow does not survive. Gradient text: `#7FE7DA → #3FCFBE → #A8C5BF` (teal into grey — no
violet endpoint). Progress bar: `accent2 → accent → good`. Keep Ex1's grid overlay at
opacity ~.10, its 1030/730 breakpoints, `prefers-reduced-motion`, and print stylesheet.
Card accent inset shadows: teal / good / warn only.

---

## Plan index

One row per live plan. **The body is in the linked file, not here.** Newest first; add a new
row at the top of the table.

| Plan | Card | What it is | Body |
|---|---|---|--:|
| [`docs/plans/DRA-305.md`](docs/plans/DRA-305.md) | DRA-305 | 2026-09-21 ~10:15 PM CT - Challenger gate, the EQBuddy Soft-loop delta. **SPEC only; binds nothing, and is NOT `ready`** - reshaped 2026-09-21 against the merged ops SPEC (`9c8faf51`, ops #58); upstream gates discharged, now waits only on its own Helm SIGN (its §7.1) after its §7.2 gate walk | 32,652 B |
| [`docs/plans/DRA-219.md`](docs/plans/DRA-219.md) | DRA-219 / DRA-216 D3 | 2026-09-19 STUB from Claude - the promoter emits wikitext as a quest TITLE | 2,047 B |
| [`docs/plans/DRA-180.md`](docs/plans/DRA-180.md) | DRA-180 + DRA-181 | 2026-09-17 ~9:15 PM CT - Founder Desktop smoke follow-ups. ONE plan, both cards. | 14,123 B |
| [`docs/plans/STUB-items-promote-trailing-attribute.md`](docs/plans/STUB-items-promote-trailing-attribute.md) | no card yet | Undated FABLE STUB - the item promoter drops a page trailing attribute | 706 B |
| [`docs/plans/DRA-179.md`](docs/plans/DRA-179.md) | DRA-179 | 2026-09-17 ~9:05 PM CT - JR/SR CAPABILITY-COST ROUTER under EXO-HARDEN (DRA-4) | 5,616 B |
| [`docs/plans/DRA-149.md`](docs/plans/DRA-149.md) | DRA-149 | 2026-09-16 ~10:30 PM CT - HELPER UPGRADE / FARM GEAR. DRAINED 2026-09-18. | 988 B |
| [`docs/plans/DRA-84.md`](docs/plans/DRA-84.md) | DRA-84 D4 | 2026-09-15 STUB from Claude - one bulleted drop list read as five "zones" | 2,597 B |

Entries older than 2026-09-15 were archived by DRA-259 to
`docs/ops/claude-archive/channels/2026-Q3/FABLE.md` and are not indexed here.
