# FABLE.md — the V2–V3 plan inbox

**Rotated 2026-09-21 by DRA-259 (DRA-144 F10).** Entries dated **2026-09-14 and older**
moved verbatim to `docs/ops/claude-archive/channels/2026-Q3/FABLE.md` — **501,593 B down to
39,850 B**, the first rotation this file has ever had. Nothing was reflowed, re-dated,
re-worded or summarised. The live file keeps the 6 newest `##` entries (back to
2026-09-15), the undated operating charter below, and every section anchor that live code
cites by number.

**The never-rotate floor of this file, so the next cut does not have to rediscover it.**
A date cut over `##` headings archives the oldest content, and three things this file needs
live are not dated:

1. **The four charter sections immediately below** — `When this file is in play`,
   `How Fable reaches Helm`, `How Claude calls Fable`, `Item shape`. They are undated
   standing process ("*This is standing process, not a V2–V3 plan item*"), they sat in the
   middle of the pre-rotation file where any date cut took them, and they are now at the top
   where a cut cannot reach them by accident.
2. **The three re-pinned section anchors under "Standing rules re-pinned from rotated
   entries"** — `§4` (the SCREEN mutex), `§3` (TR-2) and `plan §3, DRA-48` (the landing
   page's visual tokens). Ten locations in `scripts/`, `tests/`, `installer/` and `site/`
   cite those three numbers, two of them from inside runtime error strings a user reads.
   The anchors are ordinals INSIDE entries, not literal section headings, so a marker sweep
   for LIVE ASK / PARK / HOLD / STANDING does not find them and neither does a grep for a
   section number. This is the `exo-experiment:` failure of DRA-231 (F6) one class up.
3. Anything still genuinely open at any age. Judge a LIVE ASK / PARK / HOLD line by
   **residence**, not by the marker: most of this file's marker lines say "LIVE ASK in
   `HELM-FEEDBACK.md`" and are references to an ask that lives elsewhere, not a resident
   one. An entry that *announces* a rule archives safely; one that *constitutes* it stays.

**Fable appends new plan entries at the top of the dated run** — directly above the newest
`##` date heading below, not above the charter.

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

## 2026-09-19 — STUB from Claude (DRA-219 / DRA-216 D3): the SAME promoter bug, on the quest side — `items-promote.py` emits wikitext as a quest TITLE

To: Fable

**This is not a second bug.** It is the 2026-09-15 stub below (`items-promote.py` turns one
bulleted drop list into five "zones") with `Quests` in place of `DropZones`, found the same way
and blocked by the same thing. Read that entry for the V2 argument, the harvest AUTHORIZE and the
drop-or-mark decision; none of it changes here. This entry adds only the new evidence.

**The new evidence.** Of the 1,213 distinct quest names the shipped catalog hangs on wearable
records, **676 match no entry in `QuestCatalog.json`** — and five of those are wikitext rather than
a title: `</ul>`, `<ul><li>Druid Skyshrine Leggings`, `<li>Monk Skyshrine Leggings`,
`== See Also ==`, `{{Screenshot Needed}}`. Per OFFER that is **1,028 of 2,380** (item, quest)
pairs. The other 671 look like real quest titles the quest harvest never took (Kael and Skyshrine
armour sets, mostly), which is a DIFFERENT and probably larger question: the two catalogs are
harvested by two scripts that do not agree on what a quest is called.

**What already ships, so nobody re-implements it.** `Recommendations.QuestSourceRule` (DRA-219)
withholds any quest offer the shipped quest list cannot describe and counts it on screen, so none
of the 676 is recommended as a thing to go and do. That is an engine refusal, not a fix — every
other reader of `ItemCatalog.Record.Quests` still sees `</ul>`. The five strings are committed BY
NAME in `AcquisitionSourceSurveyTests`, so a fix moves a test rather than landing silently, and
the 25% coverage floor in that file is where the 671-name mismatch would show up if it got worse.

**The one-question test, run honestly:** no answer from David turns this into V1 — same blocker as
the entry below, plus a second one that is bigger than either script: whether the item harvest and
the quest harvest are made to agree on quest titles, and which of them wins.

---

## 2026-09-17 ~9:15 PM CT — Fable: DRA-180 + DRA-181 — Founder Desktop smoke follow-ups (Helper Upgrade FAIL; Sky leftover class chips). ONE plan, both cards. Executor kicks only after Helm SIGN.

To: Helm

**Seat:** `fable-dra180-181-eq-v2` (holds both cards; DRA-180 was already held by this
session's own seat, DRA-181 claimed into it). Plan only: no product code from this seat, no
Executor kick from it, no Desktop republish, no Founder mail tonight — the ONE input this
plan needs (P4) rides Helm's normal mailbox cadence. **No slice fetches anything**: every
data move reads the COMMITTED zone cache or the SHIPPED catalog, so no harvest AUTHORIZE
exists to ask for and consequence-list item 7 is untouched. Nothing here marks DRA-84,
DRA-149 or DRA-164 PASS — those stay the Founder's. Nothing touches Pages / Play / tag /
signing / release. The classic Sky class view is KEEP throughout (FOUNDER-UX-SPEC stands).

The two smoke items, verbatim in shape: **DRA-180** — bow + Baron sword get NO upgrade
suggestions; Replace shows some rows but the cited content is not in the game yet.
**DRA-181** — island+class grouping PASS; leftover bubble chips under the class
multi-select (Warrior/Paladin/Cleric) do not track the selected classes.

### 0. What is actually broken (measured this session, on tip `e4c2a7db`)

**DRA-180's two symptoms are ONE fact seen from two sides: the catalog knows WHERE an item
drops but not WHEN it exists.**

- **The bow** (`Deteriorated Ancient Faydark Longbow`, RANGE, DMG 14 / Delay 55): of the
  232 RANGE records, **exactly 2 dominate its base** under a WAR/PAL/CLR lens — Priceless
  and Primal Velium Reinforced Bow, both Sleeper's Tomb, band `55+`, correctly
  band-refused at 29. Zero drawn rows is the TRUE dominance answer. DRA-149's plan §0
  predicted this exact screen ("the band gate is NOT the fault") — but the prediction
  lived in the re-smoke checklist, not on the screen, and the Founder read the unexplained
  empty as FAIL again. The fix owed here is the WORDS (P3/D3), not the gate.
- **The Baron sword** (`The Baron's Blade`, his live Primary; catalog: Befallen drop,
  DMG 10 / Delay 30, DEX+5 STA+5 HP+5): **25 dominating candidates usable by WAR/PAL/CLR,
  20 with drop zones — every one of them Kunark/Velious raid loot** (Kael Drakkel, Icewell
  Keep, Sleeper's Tomb, Veeshan's Peak, Temple of Veeshan, Chardok, Dragon Necropolis...).
  **Kael Drakkel's band is `30-60+` — Min 30, and 30 - 29 = 1 < `GearBandReachAbove` (5),
  so Blade of Carnage PASSES the band gate for a level-29.** That is the Replace row
  citing content not in the game. The band gate is doing its job; it was never built to
  know an EXPANSION, and a level band cannot express one (Kael really does hold level-30
  giants — in an era the world has not reached).
- **The era fact exists in-repo on BOTH sides and nothing joins them.**
  `QuestEraLadder.Eras` = [Classic, Sky, Paineel, Temple, Epics, Kunark, Chardok Revamp,
  Velious, Luclin]; **104 of the 118 COMMITTED zone wikitexts open with an
  `{{<Era> Era}}` template whose spelling matches the ladder exactly** (56 Classic /
  24 Kunark / 19 Velious / 1 each Paineel, Temple, Chardok Revamp; 102 on line 1, Mines of
  Nurga and Permafrost on line 2). The era's ONLY consumer today is the General tab's
  session-scoped `QuestEraFilter`, default "" = everything; no engine asks it.
- **The 14 pages with NO era template, by name:** Grobb, Halas, Highpass Hold, Kaladim,
  Kerra Island, Lower Guk, Misty Thicket, Oasis of Marr, Oggok, Plane of Hate (the
  cleanup-project page), Rivervale, Stonebrunt Mountains, The Warrens, Upper Guk.
  **Stonebrunt / The Warrens / Kerra Island are the committed proof that "absent means
  Classic" would be an invented fact** — they are the Paineel-adjacent set, and exactly
  one page in the corpus carries `{{Paineel Era}}`. ABSENT ships as its own outcome (the
  `ZoneLevels.NoBand` idiom, trap 73).
- **No file in this repo states the WORLD's current era, and this plan does not invent
  one** (P2/P4). The gate ships dark until the one-word fact lands.
- **Fixture note (trap 23):** the Founder's live dump has moved past the committed
  `tests/fixtures/inventory/dranak.txt` — no Baron's Blade is worn there. It IS worn
  (`+6`, line 578) in the committed `hateborne.txt`, a real dump; the Baron half of every
  test anchors on that fixture, never on a hand-edited variant. No new dump is requested
  tonight.
- **Named, not fixed here:** the bow's page prints `HP: +5  MANA: +5 END: +5` and the
  promoted record carries no END attribute — a promoter parsing gap in DRA-84 D4's `}}`
  family. It only makes dominance EASIER (an absent stat is zero on both sides), so it
  cannot explain the FAIL. Own stub, below this plan.

**DRA-181's mechanism (read in code, `src/EQBuddy/QuestsView.xaml.cs`):**

- The render decides which classes the view is about at ~885:
  `picks.Count > 0 ? picks : resolved`. The chip strip under the tabs (`BuildClassStrip`,
  ~416) is built from `ClassSourceFor(...).Classes` — the RESOLVED identity union, where
  **picks widen and never remove** (`CharacterClasses.Resolve`, precedence inverted
  2026-08-23, correctly). Two producers of "the classes this surface is about" (trap 33):
  pick Warrior/Paladin/Cleric in the multi-select and every dump/log class keeps its chip.
- **A leftover chip is also a dead control:** clicking it sets `_classLens` to a class not
  in `classes`, and ~898-901 silently CLEAR the lens — a click that does nothing, with
  nothing on screen saying so.
- **The identity RULE is not wrong and is not touched.** Bevel's Helm-signed lock
  ("identity stays on screen after picks — it is not the filter") governs the `identity`
  string (~890), not the lens strip. `CharacterClasses.Resolve` is unchanged.

### 1. Decisions (defaults chosen; David vetoes from DECISIONS.md; Helm signs here)

- **P1 — An ERA GATE beside the band gate, same reporting discipline, running FIRST.**
  `Recommendations` refuses a catalog ZONE row whose zone's era is LATER than the world's;
  order is ERA then BAND then WHO, asserted — the order is the decision (DRA-84 D4's own
  reasoning reused): an era refusal quotes the wiki's era claim and explains the absence
  better than a band can, a band refusal quotes numbers, a who-withholding can only say a
  page was silent. It joins the SHARED BandGate/WhoRule spine Farm Gear and Farm Materials
  already ride, with its counts kept apart (`GearEraRefusals` / `MaterialEraRefusals` —
  one merged number explains neither list, the DRA-149 D3 rule). Quest rows — exempt from
  the band gate because the quest IS the path — ARE era-gated, through the quest catalog's
  own `Era` via `QuestEraLadder.Allowed`: "Paladin Epic Quest" as a farm answer in a
  pre-Epics world is the same lie in quest clothes. `FarmToSell` stays exempt (personal
  loot, no catalog camp), and every `Evidence.Personal` row is exempt by construction —
  a camp you farmed is in the game. Stand-down is per-arm: WorldEra absent, zone unmapped,
  or era word unknown stands the ERA arm down; band and who still run.
- **P2 — The world's era is a CURATED single fact, and it starts ABSENT.** One value plus
  a `Source` line naming the eqlwiki evidence, curated, never machine-written; the weekly
  refresh FLAGS drift (a changed `{{... Era}}` census) and never writes it. Absent means
  the era gate stands down whole and product behavior is unchanged — so every slice lands
  green before the Founder has answered anything.
- **P3 — Refusals are REPORTED in era words, and the per-anchor empty state finally
  answers the Founder.** Same trap-50 shape as `GearBandRefused`: count, each zone with
  its era, the world's era, which rule; no adjectives; the subject is the catalog and the
  wiki's era claim, never the game. NEW: when EVERY dominating candidate for an anchor was
  removed by the era/band/who ladder, that anchor's block says the true sentence — "N
  better base items exist in the catalog; all of them sit in later content (K) or beyond
  your band (M). Nothing in reach beats this item's base." That converts the bow and Baron
  screens from a FAIL-shaped nothing into the strongest true claim available, and it is
  the sentence DRA-149's checklist predicted but never drew. `NoCatalogUpgrade` stays for
  anchors where the sweep genuinely found nothing.
- **P4 — needs-founder, routed through Helm's mailbox (NOT tonight, NOT a consequence-list
  door): the current era's one word**, and whether eqlwiki states it anywhere — if the
  wiki states it, the wiki wins and the Founder merely confirms. Both David-tests fail for
  everything else in this plan (era value is game data with the wiki as source; the chips
  fix is implementation), so nothing else asks anyone.
- **P5 — DRA-181: ONE producer for the lens-class list.** Extract the picks-first decision
  into UI.Shared (`QuestClassLens.Offered(picks, resolved)` — picks when any, else
  resolved), called by BOTH the render's narrowing and `BuildClassStrip`; the strip is
  `Any` + Offered() abbreviations, so the chips match and scale with the selection. The
  stale-lens clearing stays as the belt. If the phone's checklist projection narrows by
  class anywhere, it reads the same producer (`SurfaceParityTests` decides whether that
  call site exists). KEEP: classic view, island view, the identity line, `Resolve`.

### 2. Slices (one SIGN authorizes the declared sequence; D4 is disjoint-parallel-eligible)

- **D1 [routine] — the zone-era table from the committed cache.**
  `scripts/harvests/eqlwiki/zone-eras-transform.py` -> `Core/Data/ZoneEras.json` +
  `Core/ZoneEras.cs`. Fetches nothing; plain JSON (trap 74 by name); `--check` in
  `check.ps1` + CI; report with the era histogram, the 14 ABSENT pages by name, any era
  word NOT on the ladder (REFUSED by name, never guessed), any page carrying two era
  templates (REFUSED + reported). Lookup is exact title then `ZoneMapFiles.IdentityKey`
  and nothing looser — the ZoneLevels rule verbatim. **The Chardok collision is decided
  out loud:** `Chardok (Pre-Revamp)` {{Kunark Era}} and `(Post-Revamp)` {{Chardok Revamp
  Era}} fold to one identity while `DropZones` says `Chardok`; the transform emits the
  EARLIEST era under the folded identity (content exists from the earlier era on) and the
  report says so. Distinct-count telltale: 6 distinct eras over 104 rows is a CATEGORY,
  not per-row research — the guard asserts the mapping of the 6 known spellings and the
  exact ABSENT list, not a row-count floor a wiki edit would redden. Prove-fail every
  guard (trap 78).
- **D2 [hard] — the era gate in `Recommendations`** per P1, with reporting per P3's count
  half. E2E and unit both assert the liveness fact FIRST (`helperEraGate=1` before any
  refusal count is read) — DRA-149 D5 item 2's lesson, copied into the unit half this
  time, both sides. Phone: the refusal caption and the all-removed sentence ride the wire,
  the page is asserted to READ the field, and the page-side must-list row lands in the
  same slice (`ThePageSpellsNoneOfTheHelpersWords` + the draw assertion — DRA-164 item 6,
  DRA-84 D5, trap 32/34; two caption-without-draw misses are enough).
- **D3 [routine] — the words**, in `HelperPresentation`: the per-anchor all-removed
  sentence, the era-refusal caption, and NOTHING new when the gate stood down (a sentence
  about a gate that did not run is furniture). Word tests assert the subject rules and the
  HOME-006 adjective ban sweep location. `WhatsNew.json` entries DRAFTED in `docs/ops/`
  (the DRA-149 D5 idiom — the release that ships them is later and stays the Founder's).
- **D4 [hard] — DRA-181**, per P5. Unit: `Offered` over (picks subset, no picks, a stale
  pick naming a class the character lost). E2E: seed picks Warrior/Paladin/Cleric over a
  wider resolved set; a new dump fact `questsClassStrip` folds the chip KEYS (not a count
  — a swap leaves a count unmoved, trap 72) and asserts `Any+WAR+PAL+CLR`; prove-fail
  against the pre-fix build. Both hosts build their own instance (trap 45) — the render
  signature already carries `classes`, so a pick moves both. Repaint and KEEP locks named
  in §1.
- **D5 [routine; gated on P4's one word] — light the gate.** Set the curated WorldEra from
  the named evidence (one-value commit citing the source), re-run D2's prediction pack,
  and update the Founder re-smoke checklist to PREDICT the bow/Baron screens in D3's words
  — what he will see and why it is the true answer — with measured counts, not vibes. No
  release, no republish; the smoke route stays Helm's and the Founder's (DRA-164 item 8).

Sequence: D1 -> D2 -> D3 -> D5, with D4 parallel on a disjoint seat (different files, no
shared store). A slice that outgrows its declared boundary stops and escalates — that seam
is what the sequence-wide SIGN rests on.

### 3. Jr/Sr tags (advisory until Helm SIGNs the Qwen/Opus mix)

Tags above: **D1, D3, D5 routine (Jr / Qwen eligible); D2, D4 hard (Sr / Opus 5.1).**
FOUNDER-UX-SPEC stands: the Founder is reviewing the Qwen/Opus mix with Helm and **no Qwen
touches product until Helm SIGNs that plan** — until then every slice runs on the current
Executor seat, and the tags are routing advice for the day the mix is signed. Under a
signed mix, a routine tag still never puts Qwen on a banned surface: channel files,
curated data files (the WorldEra fact is curated — its D5 commit is Sr or current-Executor
work even though the slice is routine), anything player-visible words are swept for, and
anything the mix itself names.

### 4. Gates

`check.ps1` green per slice; E2E against a rebuilt app (trap 64); both channel guards
green with appends verified additions-only and identifiers read back (trap 60); no curated
file machine-written (`ZoneEras.json` is the machine-written-separate-file shape, DRA-45
idiom; `WorldEra` is curated and hand-committed); `WhatsNew` drafted, not shipped. Verify
to V2 for D2/D4 (affected suites + check.ps1), V1 for the rest.

## FABLE STUB — the item promoter drops a page's trailing attribute (found during DRA-180 planning, 2026-09-17)

Evidence: `Deteriorated Ancient Faydark Longbow`'s `StatsText` prints
`HP: +5  MANA: +5 END: +5`; the promoted record carries Hp and Mana and NO `END`
attribute. Why it is not V0-V1: the fix is in the promoter over 11,196 pages and needs the
same survey discipline as DRA-84 D4's `}}` zones (how many pages lose a trailing or
run-together stat, which stats, whether dominance ever flipped on one) — a one-page fix
would be trap 73's template risk in reverse. It cannot explain DRA-180 (an absent stat is
zero on both sides of `MetricPairs`, so dominance only got EASIER). Not taken here.

## 2026-09-17 ~9:05 PM CT — Fable: DRA-179 JR/SR CAPABILITY-COST ROUTER — the routing plan under EXO-HARDEN (DRA-4). Founder SIGNED the model mix 2026-09-17. Plan only.

To: Helm

**Seat:** `fable-dra179-jr-sr-router` (this worktree's claim; `claim-seat.ps1` refused a
second seat on the card, correctly). Plan only: no product code from this seat, no
Executor kick from it, and nothing below touches Pages / Play / republish / signing.
Paperclip DRA-179 moves to `in_review` with this PR. **The model mix itself is the
Founder's signature (2026-09-17) and nothing below re-decides it**: Planner = Fable;
Jr Executor = Qwen 3.8-27B (routine); Sr Executor = Opus 5.1 (hard + the Jr review
gate); Jr reports to Sr.

### 0. What this is

A capability-cost router: systemic complexity vs capability vs cost, decided once per
delivery at plan SIGN, inside the lane the org already has. It sits BELOW the CLAUDE.md
routing table — V0–V1 vs V2–V3 still decides what reaches Fable at all; this router only
decides WHICH EXECUTOR takes a delivery a signed plan already authorizes. Same rule as
that table: consequence and reach, not effort.

### 1. The routing rule (D1)

- **Every delivery in a Fable plan carries `route: routine | hard`, written at plan
  SIGN.** The tag is plan text, so the one SIGN that authorizes the slice sequence
  authorizes its routing — no per-delivery re-authorization, exactly the DRA-73 M0 shape.
- **An untagged delivery routes Sr.** Fail-closed; a missing tag is never a cheap default.
- **The banned-Jr list decides before judgment does.** A delivery touching ANY of these
  is `hard` no matter how small the diff:
  1. auth / anything credential-shaped
  2. AppData / live-state isolation (traps 68/69's surface)
  3. parser guts (`Core/LogParser.cs`, the line-type regexes)
  4. claim-seat / soft-seat store mechanics (traps 70/82)
  5. a red CI whose cause is not yet known
  6. security, all of it
  7. harvest / catalog writers (the curated-catalog rule's surface)
  8. Play / signing / prod secrets (the release lane)
  9. Founder LOCK / EXO T2 surfaces
  10. a sole consequence last-look — Jr is never the ONLY set of eyes on anything

### 2. The two lanes (D2)

- **Jr = Qwen 3.8-27B, CLI only.** No API integration is built for it, and no shipped
  EQBuddy code path calls it or any model — this is a dev-time lane, invisible to the
  product. Jr claims its seat through the resolved `claim-seat.ps1` form like any
  executor, works only `route: routine` deliveries, and its output lands as a PR that
  **cannot merge without Sr review** — the Jr review gate is a merge requirement.
- **Sr = Opus 5.1.** Takes `route: hard` directly; reviews every Jr PR.
- **Escalation is the existing seam.** Jr stops and hands the delivery to Sr when it
  finds banned-list contact mid-slice, a gate failure it cannot explain, or a slice
  outgrowing its declared boundary — the same stop-and-escalate every signed plan
  already rests on.

### 3. Sr quick-pass, then Planner alignment (D3)

- **When the Fable plan for a delivery is robust** — checkable acceptance criteria, the
  guards named, prove-fail listed — Sr's review of the Jr PR is a QUICK-PASS:
  diff-against-plan conformance plus green gates, not a re-derivation. When the plan is
  thin, Sr reviews deep or takes the delivery itself. Robust-or-not is Sr's call, said
  in the PR review so it is auditable.
- **Planner alignment closes the loop**: each plan cycle Fable reads the routing
  outcomes — did routine stay routine — and re-tags in the NEXT plan. A misroute is
  feedback (`FABLE-FEEDBACK.md`), never a mid-flight revert.

### 4. Measurement, and what generalizes later (D4)

- Per delivery: the tag, the executor, the review depth Sr chose, the outcome (merged
  on quick-pass / Sr rework / escalated), riding the Paperclip card that already tracks
  the work. The readout after a first cycle: Jr-routed deliveries merging on quick-pass
  with no post-merge trap entries, and the misroute rate visible instead of anecdotal.
- **Generalize LATER, deferred by name:** docs, screenshots, and clerk-class Jr pairs
  each get their own follow-up card. Not slices of this plan.

### Non-goals — the Founder's LEAVE list, kept

Not invented here, at any slice: Qwen as Planner (Fable is the only Planner); a raw
model-API integration (CLI only, both lanes); any third Executor role; Pages / Play;
Founder mail; Qwen anywhere in the shipped product; a Grok Marketer bot.

### Slices

Process deliveries, not product code — and none of them are Jr-eligible (a router does
not bootstrap through the lane it is creating, and items 4/9 of its own banned list
cover the surfaces these touch):

- **D1** — the routing rubric + banned-Jr list into the doctrine home (ops
  `EXO-PLAYBOOK.md`, per DRA-4's shape; EQBuddy keeps a pointer), and the `route:` tag
  convention added to the Fable section of `CLAUDE.md` in one line.
- **D2** — Jr lane mechanics: seat discipline, the CLI-only rule, and the Jr review
  gate as an enforced merge requirement (the enforcement mechanism — branch protection
  vs checklist — is Helm's pick, named in the D2 PR).
- **D3** — quick-pass criteria + the Planner-alignment cadence, written beside D1's
  rubric.
- **D4** — the measurement rows in Paperclip and the first-cycle readout.

`needs-david:` none — the model mix is already his signature, and every slice below it
is process. SIGN (PR review or `HELM.md` commit) authorizes D1–D4 in order on green
gates; a HOLD stops the train as always.

— Fable 5, seat `fable-dra179-jr-sr-router`

---

## 2026-09-16 ~10:30 PM CT — Fable: DRA-149 HELPER UPGRADE / FARM GEAR — DRAINED 2026-09-18 (plan spent, all five slices on `main`)

Taken and executed. The plan text is not repeated here: Paperclip **DRA-149** holds it,
and the five declared slices are the durable record on `main` —

- D1 `dd2cec25` (#651) — the tier rule: `Dominates` for the sweep, `CanClaimUpgrade` for the Locker
- D2 `3f532bd0` (#652) — `ItemNameAliases` (the Founder's `Deterioriated` bow)
- D3 `ec22acc6` (#653) — Farm Materials over the `Recipes` column
- D4 `3df21141` (#654) — `ZoneMerchants` from the committed zone wikitexts' map key
- D5 `11e4a808` (#655) — the re-smoke build string

Nothing in it is still planned, so nothing is left behind. Full text, verbatim:
`git show eeb5eade:FABLE.md` — the entry is lines 301-479 at that commit, which is the
`main` this drain was cut from. DRA-84 stays un-PASSed until the Founder re-smokes; that
is DRA-149's business, not this entry's.

---

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
