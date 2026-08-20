# EQBuddy — handoff

**Don't re-derive the codebase.** `CLAUDE.md` loads automatically and carries the commands,
the non-negotiable rules, the where-things-live index, the trap list (32) and the
surface-allocation rule. `docs/Architecture.md` and `docs/TestPlan.md` sit behind it, and
`DocumentationTests` fails the build if any go stale. Start with
`pwsh -NoProfile -File scripts/status.ps1`.

---

## 2026-08-20: both scoped items landed, and both were bigger than the ask

`main` clean and pushed at `1937270`. Gates green — **2,154 unit + 250 Avalonia + 15 E2E**.
1.96.2 is still **prepared, not released**: version bumped, `WhatsNew.json` written (dated
2026-08-20 now, since most of its content is from today), waiting on David's go.

### 1. EQBuddy Mobile exists on Linux and macOS (#208, sbaum23) — `e4825ca`

The port, not a toggle, exactly as scoped. Same `CompanionHost`, same `CompanionSources`
record copied whole rather than reduced, same 50 ms pump gated by `CompanionPumpGate`, same
1 Hz reconciliation tick, a `CompanionWindow` twin, the title-bar button, the menu entry and
the Options block. **Replied on the thread, signed.**

- **`CompanionWiringTests` is the guard that matters.** It reflects over `CompanionSources`
  and fails if this lane leaves one unwired — verified by dropping `Raids` and watching it
  fail, which is the exact omission the handoff warned a port would make. A missing source
  is not a compile error; it is a surface that arrives empty on Linux and full on Windows.
- **Trap 13 needed nothing.** `SingleInstance` is keyed on the profile, not the toolkit, so
  the two builds can no longer both reach the constructor and race for the port.
- **`CompanionPairingText` (UI.Shared) now owns the window's copy for both widgets**, and
  picks the firewall paragraph by OPERATING SYSTEM. My own hand-ported copy told a Cosmic
  player to open "Windows Security → Firewall" and named the page's fullscreen control with
  a glyph that draws tofu in a default Linux font set. One window wrong, invisible to every
  gate — which is the argument for the shared module, made by me, against me.
- Also: `QrRaster` (UI.Shared) owns the QR quiet zone for both renderers, and Avalonia's
  `AppTheme` stopped carrying a byte-identical hand-copy of all fifteen `IconPaths` entries.

**Two live WPF defects fell out of it.** The title-bar phone button had never once been
visible — `Visibility="Collapsed"` from 2026-08-14, un-collapsed by a preview gate that was
later deleted while the menu entry's `Visibility` was removed and the button's was not. And
the gear beside it asked for the emoji variation selector three lines under a comment
explaining that colour emoji ignore `Foreground`. Both drawn now. **Trap 29.**

### 2. The quick tour shows the app that ships, and can be looked at — `1937270`

All five images retaken. The real deliverable is that the tour is now **reviewable**:
`EQBUDDY_TOUR=<page>` opens it on any page and `shoot.ps1` has a shot per illustrated page
(`tour-widget`, `tour-combat`, `tour-watch`, `tour-mini`, `tour-history`). They went a month
stale because seeing page 4 meant installing the app and clicking Next three times.

- `t-watch` uses a new `watch-solo` shot that hides the other cards through
  `HiddenSections` — **a real state, not a pixel crop**, because a crop is a number that
  keeps producing a picture of the wrong part as soon as a card gains a row.
- `t-combat` is the **damage breakout**, because the Combat card is 994 px tall today and
  arrives 109 px wide in the tour's 528×320 frame. One sentence added to that page so the
  words and the picture agree. The old image was a chromeless hand-crop.
- `t-history`: `EQBUDDY_HISTORY` already existed (the handoff said it didn't) — what was
  missing was DATA. Sessions only reach `history.db` when one ENDS and the fixture
  compresses every idle gap into one live session, so `shoot.ps1` **primes** it: run the
  app, close it gracefully, once on a prefix of the log under another character so the two
  sessions differ. The window now opens on the newest session instead of on "Select a
  session.", which was half an empty screen.
- **`mini-bar` had silently stopped photographing the mini bar** — it disables every
  `BreakoutKind` by hand and `Progress` joined that enum without being added, so it was
  shooting the Progress breakout. Re-running it would have overwritten a correct committed
  screenshot with the wrong window. **Trap 30.**
- Captures now pin their palette (**trap 31**): `AppTheme`'s brushes are process-wide
  singletons and `AppThemeTests` walks the catalog, so the first EQBuddy Mobile capture came
  back in Turquoise while its settings said ParchmentBrass.

### Left undone deliberately

- **The tour frame's `MaxHeight` is still 320.** The widget is 488 px tall now, so it renders
  at 221 px wide — legible, but smaller than the July image was. Raising the cap would make
  the tour window taller, and I could not verify that it still fits a 768-tall laptop.
- **#208's actual subject is still open.** Chips and alerts land on the wrong monitor under
  Cosmic/Wayland; the Mobile port sidesteps it for the review surfaces and fixes nothing
  about the deadline ones. sbaum23 is building locally and testing — the top-window opt-out
  described in the thread is the piece worth doing and he was asked to say so before going
  far, so we don't both write it.
- **`BreakoutKind` still disagrees across the two UIs** (WPF has Watch/Loot/Progress,
  Avalonia doesn't). Still in `SCRIBE.md`, still unreported, still don't raise it with a
  poster.

---

## Still open from before, unchanged

### 3. #202 (bjstrange) — the page could never have received the fix

Looked at it with the room left over, and the handoff's own framing was the thing to test:
*"either a second clock reaches that card or the reporter's binary isn't what we think."*
Neither. **The fix is genuinely in `v1.94.1`** (`fcdc412` is an ancestor of the tag), the
exclusion list is keyed for the camelCase the wire actually uses, and the gate lifted
verbatim out of the shipped page holds still against a real loot payload when only the
rates move and still repaints on an actual drop.

**The page never re-fetches itself.** The socket reconnects forever, so updating EQBuddy
restarts the server and the phone reconnects — still running the JavaScript it downloaded
when the tab was first opened. `no-store` is irrelevant; nothing asks for the HTML again.
His PC had the fix; his page almost certainly did not, and both were reporting 1.94.1.
**Trap 32**, and it would have quietly explained every future page-side fix the same way.

`identity.appVersion` was already in every envelope and only ever printed in the footer.
The page now reloads itself once when it changes, and records what it reloaded FOR so a
cache it cannot see becomes a message instead of a loop. Verified by running the guard
under node — reloads exactly once, never loops, ignores a missing version — and
`CompanionPageUpdateTests` fails against the old page.

⚠️ **This is a diagnosis, not a confirmation.** Nobody has reproduced bjstrange's card
churning with a page that is definitely current. **Ask him what the FOOTER on his device
says** — not what version his PC is on — and whether a hard refresh changes anything. If
it churns on a page whose footer reads 1.96.2, the cause is something else and this fix is
still worth having. He has not been replied to yet: I would rather ask that one question
than announce a fix for a symptom I cannot see.
- **Waiting on reporters — do not chase, do not close.** #218 n3cr0nk1tt3n (does he have an
  update folder? the fix only bites that path). #221 NeONDaRoO (a verbatim instance-charge
  log line; if the game doesn't log it, that is the honest answer). #101/#193 (the
  token-unlock half needs a token-side achievements dump — do NOT implement from one file).

⚠️ **Still to confirm with David:** Frank's closing note on #65 describes the wiki pack's
Ask 2 (full history, account-wide, no per-session toggle) as *"approved in principle"*. That
is a reporter summarising, not authorization, and `SCRIBE.md` still has it as his scope call.
Get his own word first.

---

## State: 1.96.1 is LIVE and SIGNED — a same-day regression fix (2026-08-19 late)

`main` clean and pushed at `1a185c5`, tag `v1.96.1`, 7 assets, workflows green.
**2,153 unit + 246 Avalonia + 15 E2E.** Signed and verified end to end
(`Get-AuthenticodeSignature` = `Valid`, issuer `Microsoft ID Verified CS EOC CA 03`, the
published asset hash-matches its `.sha256` after download).

### What 1.96.1 fixed, and the lesson in each

- **#219 (typical-usual-chaos) — motes/hour vanished from the widget, and it was MINE.**
  Reported 90 minutes after 1.96.0. The Progress fold put the Motes card into the Wealth
  tab and my own launcher edit dropped the RATE to stop the line truncating — so it
  survived only inside a tab, two clicks away, and Options no longer listed Motes so it
  could not be put back. His third screenshot (Options → Cards & windows, no Motes row)
  was the half the text alone did not convey. **The launcher now carries what MOVES WHILE
  YOU PLAY** — xp, coin, mote rate — with faction and raids moved to their own tab badges.
  → **Every gate was green when that shipped, because nothing asked what the line said.**
  `ProgressThemeTests` now does. When a fold changes a STRING a player reads, pin the
  string.
- **A long zone name printed straight THROUGH the session timer.** `ZoneText` and
  `SessionText` were both children of a `Grid` with no `ColumnDefinitions`, so they shared
  one cell and overprinted — trap 14's family. The Avalonia twin has had the two-column
  grid all along. **Nobody ever reported it**; the fixture's zone names are far too short
  to show it, which is why every capture ever taken here missed it. New `long-zone` shot.
- **#218 (n3cr0nk1tt3n) — updating went one hop at a time.** `FindBestAsync` kept an early
  return: "if the shared folder beats what is INSTALLED, take it and skip the network", so
  a folder one release behind hid every release after it.
  → **`UpdateCheckerTests` already asserted this exact rule and stayed green, because the
  caller never reached `PickBest`.** A decision function can be correct, and tested, and
  bypassed. When a rule matters, ask who is allowed NOT to call it. **Still open with the
  reporter:** I could not confirm he even has a shared folder, and said so rather than
  closing it — if he answers "no", something else is going on.
- **#217 ask 4 (Frankthetankk) — the wiki edit summary no longer says "EQBuddy"** (David
  approved). Now `observed drops (2 items, 12 kills)`. The pack's own HEADER still names
  EQBuddy and should: that is the app titling a document for its reader, not text going
  onto someone else's wiki. The first test asserted the whole pack contained no "EQBuddy"
  and failed on that header — which is exactly the distinction the ask is about.

### Standing rules reconfirmed tonight

- **Publishing is `release.ps1` and it signs — there is no other way** (David: *"this is
  the way we need to publish going forward"*). `az login` is the only human step.
- Post GitHub replies for finished work without asking, signed `— Dranak (Claude Code)`.
  Releases wait for David's explicit go; he gave it for both 1.96.0 and 1.96.1.

---

## State: 1.96.0 is LIVE and SIGNED — the first trusted release (2026-08-19 evening)

`main` clean and pushed at `c4bad7e`, tag `v1.96.0`, **7 assets**, both workflows green.
**2,147 unit + 246 Avalonia + 15 E2E.**

### Publishing changed, permanently

**Every release is signed, and there is no path that isn't** (David, 2026-08-19: *"note
this is the way we need to publish going forward"*). `release.ps1` signs through
`scripts/signing.ps1` — Azure Artifact Signing, `CN=FlossworksCross-Stitch`, issued by
`Microsoft ID Verified CS EOC CA 03`. The self-signed cert and `new-cert.ps1` are gone.

- **The only human step is `az login`** when the session expires. The dlib restores itself.
- `artifact-signing.json` (repo root) and `tools/` are gitignored — absent on a fresh clone.
- **Verified independently, not just reported:** `Get-AuthenticodeSignature` returns
  **`Valid`** on `EQBuddy.exe`, `EQBuddySetup.exe` and the OneDrive copy, and the
  downloaded GitHub asset hash-matches its published `.sha256`. The old path could only
  ever say "signature embedded".
- **The certificate is valid for THREE DAYS** (2026-08-19 → 08-22). The countersigned
  timestamp is the only reason a release stays verifiable afterwards — never drop it.
- **Do not add a `-SkipSign` or a warn-and-continue.** That is exactly how the old path
  could ship unsigned while reporting success. CLAUDE.md carries this as a hard rule.

Announced in [#123](https://github.com/DranakCorps-bot/EQBuddy/discussions/123), the thread
that had been promised this update since 2026-08-14. **SmartScreen reputation starts at
zero for a new publisher** — say honestly that warnings fade rather than stop dead. The
DONATIONS half of that thread is still deliberately unbuilt and I promised no date.

### What shipped in 1.96.0

The **Progress theme** (`638ee68`) — the second theme, and the first since themes became
the frame. Progress, Money, Motes, Faction and Raids became one launcher card and one
window with four tabs (Experience · Wealth · Faction · Raids). **14 cards → 10**, on the
WPF widget, the Avalonia widget and EQBuddy Mobile in one change.

- Core `ProgressSurface` owns the tabs; UI.Shared `ProgressTheme` owns the badges and the
  launcher line; all three surfaces read them (#210's rule).
- **E2E pinned BEFORE the move** and the numbers came back identical three times — from
  the card, from `RaidsCardView` after the lift, from the window after the fold: 29 raid
  rows, 24 sold items, 1 motes row, 5 factions. MainWindow baseline lowered 4355 → 4324.
- **Four things only a screenshot could say**, all fixed: the launcher line truncated
  mid-word; the tab strip was a `StackPanel` so the Raids chip was clipped off the window
  entirely (trap 14 with chips — the #184 bug); four shots now share one window title so
  `shot.ps1` photographed the wrong app (it takes `-OwnerPid` now); "1 factions".
- **Linux/macOS bug found on the way:** Avalonia's `WindowZoom` passed `TryGetValue`'s
  `out` value (0.0 when absent) into the width calculation, so the Quest Tracker opened at
  **zero width** the first time, before any Ctrl+wheel was saved. Windows was never
  affected — the WPF twin does a second lookup with its own fallback. `WindowZoomTests`
  pins it. **Ask the Linux/macOS reporters to confirm**; David is Windows-only.
- CLAUDE.md gained traps 24–26.

### Next

**Alerts is next by the plan — but re-measure first.** ROADMAP.md and docs/Themes.md now
say why Progress jumped the queue: the ordering predated Gate 5b lifting four card bodies.
Alerts needs `RenderBuffs` (107 lines, into the buff-set evaluator) lifted before it buys
anything, and would only take 14 → 13. The right question is "which theme is most nearly
built already", not "what does the plan say next".

**Still waiting on David:** the Gate 5d chevron (before/after sent 2026-08-19, no comment).

**Still blocked, correctly:** `/consider` rare word (#185, #217) — neither reporter has
pasted the verbatim line and we are last comment on both. Do not reconstruct it (#206).

---

## State: THEMES is the direction now — Progress is first, half-built (2026-08-19 evening)

`main` clean and pushed at `06ab1cc`, CI green. **2,142 unit + 243 Avalonia + 12 E2E.**
1.95.0 is the released version; everything below it is unreleased source.

### THE FRAME — read this before picking anything up

**David ruled on 2026-08-19: themes are a DIRECTION, not a proposal.**
*"I do want to move to themes, this is part of the major UX revamp that organizes
everything much better. it's not a proposal, it's a direction we need to go, just as we
did with quests."* [docs/Themes.md](docs/Themes.md) is the plan and the six-step recipe;
`ROADMAP.md` now carries the theme table. **The gates restyle what exists; themes change
what exists.** 14 cards → 6.

**And he named the guide: "I really want to use the approach for Quests as a guide for how
we integrate."** So copy `QuestSurface.cs`, `QuestsWindow`, `MigrateQuestSections` — do not
invent a second way to do this.

### PROGRESS THEME — steps 1 and 5 done, 2/3/4/6 are the work

**Why Progress and not Alerts, which the plan lists first:** the plan was ordered on
2026-08-17, BEFORE Gate 5b lifted four card bodies. Measured 2026-08-19 and David chose
accordingly. Readiness, and this table is the thing to re-check before starting any theme:

| Theme | Absorbs | Already lifted | Result |
|---|---|---|---|
| **Progress** | progress, money, motes, faction, raids | **4 of 5** (only Raids inline) | 14 → 10 cards |
| Alerts | tracked, buffs | 1 of 2 — `RenderBuffs` is 107 lines into the buff-set evaluator | 14 → 13 |
| Loot & Items | loot, gear | 1 of 2 | |
| Live Meters | combat, healing, kills | 1 of 3 | |
| World | misc (Travels & Deaths) | 0 of 1 | |

**Done (`06ab1cc`):**
- `Core/ProgressSurface.cs` — step 1. Four tabs (Experience · Wealth · Faction · Raids),
  labels, stable keys, `AbsorbedCardKeys`, `ThemeCardKey`, `LauncherSummary`. 22 tests.
- `AppSettings.MigrateProgressSections` — step 5, generalised from `MigrateQuestSections`.
  9 tests. **Its idempotence test caught a real bug**: `progress` is both the surviving key
  AND an absorbed key, so a folded profile re-folded every load — same order out, but
  reporting a change, which forces a settings save each launch (trap 13 rewrites the whole
  file). It returns early now unless a non-theme key remains.

**Remaining, in order:**
1. **Lift Raids** — `RenderRaids` (MainWindow ~line 1325) into `RaidsCardView.cs`. The
   only one of the five not on the seam. `WatchCardView`/`ProgressCardView` are the pattern.
2. **Step 4 — `ProgressWindow`**, tabs inside, hosting the five already-lifted views.
   `QuestsWindow` is the template; use `EqChip`/`EqSegmentedStrip` for the strip, never
   hand-build one.
3. **Step 3 — the launcher card.** Five `Expander`s (`ProgressSection`, `MoneySection`,
   `MotesSection`, `FactionSection`, `RaidsSection`) become ONE `Button` with
   `Style="{StaticResource SectionLink}"`, exactly like `QuestsSection` at
   `MainWindow.xaml:490`. Wire `MigrateProgressSections` into the load path.
4. **Step 6 — Mobile, in the same change.** `CompanionProjection` — #210's whole lesson.
5. **Pin in E2E BEFORE the move** (facts into `EQBUDDY_EXPAND`), then **lower the hotspot
   baseline in the same commit**.

**David wants BEFORE/AFTER screenshots of the consolidation.** Shoot `widget-cards` before
touching the XAML, and again after. `scripts/shoot.ps1` — and **close the real EQBuddy
first**, which bit twice today.

### Waiting on David

- **The Gate 5d chevron.** Before/after sent; he has not commented. The card-header
  chevron is now a vector and is noticeably bigger than the `▸` it replaced (which read as
  a faint tick). If he dislikes it, drop `DesignTokens.IconInline` at that one call site.
- **1.96.0.** Not cut. He said *"if all is good, we can push live"* and then asked for the
  consolidation first. Unreleased: the Avalonia widget (30 glyphs → vectors, 91 sizes →
  tokens), the Progress card lift, Gate 5d, and the two theme surfaces.

### Rules learned or re-learned today

- **CLAUDE.md trap 23** — fixture staging in the wrong SHAPE renders a REAL state, so the
  shot looks correct and is a picture of something else. **Predict a shot's numbers before
  running it.**
- **`release.ps1` relaunches the real EQBuddy, and `shot.ps1` matches on window TITLE.**
  Cost two wrong captures today, one of them of David's live profile.
- **A capture surface needs profile isolation MORE than an assertion does** — its entire
  output is a picture of whatever profile it finds (`WidgetSheetTests`).
- **Migrations that survive their own second run.** See the fold bug above.
- **David is Windows-only** (*"others will need to give feedback there"*). Avalonia changes
  can never be verified before a release — ship on headless evidence, name the
  Linux/macOS changes in the notes, ask those reporters to look.
- **Standing: post GitHub replies for finished work without asking**, always signed
  `— Dranak (Claude Code)`. Releases still need his explicit go.

### Still blocked, correctly

**`/consider` rare-creature signal** (#185 n3cr0nk1tt3n, #217 Frankthetankk). Neither has
pasted the verbatim con line; we are last comment on both. `ConsiderRx` is
`^(?<name>…)(verb).*\(Lvl: N\)$` — the `.*` already swallows anything before the tail, so
rarity text BEFORE it is a one-line capture-group addition and AFTER it breaks the `$` and
needs a second pattern. **That is the entire question. Do not reconstruct the line (#206).**
David approved the feature in principle. `LogParser.cs` has 25 lines of ratchet room.

---

## State: 1.95.0 LIVE and verified (2026-08-19 midday)

Tag `v1.95.0`, **7 assets** (both Mac builds + the Linux tarball), OneDrive updated, both
workflows green. `main` clean and pushed. **2,073 unit + 240 Avalonia + 11 E2E.**
`status.ps1`: *"none — all 25 open threads have our reply last."*

Two things shipped in it, both from the previous handoff's ordered list.

### 1. #217 Ask 1 — the wiki contribution pack is its own window (David ruled today)

Under Data & imports, both UIs, `UI.Shared/WikiPackPresentation.cs` owning every word.

**The finding worth keeping:** the old `✦ Copy for wiki` button read `_snapshot.Mobs`
*plus the Drops window's filter box*, so the only thing making "this session only" legible
was standing in front of that list. A relocated menu command would have copied a silent
scope — which is why it became a WINDOW that states what it pooled, and why **Asks 1 and 2
were never independent**. Neither the thread nor Scribe's entry said so.

Drops by Creature keeps its live view. Its ✦ now OPENS the pack instead of copying.

### 2. #108 — "who wants this drop?" restored, and it had SHIPPED once already

`QuestChecklistLayout.SearchByItem`, in Core, called by both desktops **and Mobile**.

**This is the third instance of one signature** (after `SkyQuestCompleted` #204/#209 and
`EpicQuestCompleted` #210): 1.69.0 shipped item-grouped cross-class search for
liminalwarmth, the Gate 2 rebuild kept the *box* and lost the *behaviour* — it became a row
filter inside the per-class sections, and started obeying the class and state filters it was
built to ignore. `DeadSettingTests` catches the SETTINGS shape of this. Nothing catches the
BEHAVIOUR shape. `QuestChecklistSearchTests` holds this one specifically.

→ **When a feature reads as "never built", grep `WhatsNew.json` for the discussion number
first.** "We shipped this and it is gone" is a different and more urgent item.

**#210 IS NOW COMPLETE — all seven asks.** liminalwarmth was asked whether to narrow it to
#108 or close it and never answered; the question is moot. **A closing note is owed and was
NOT posted** — David approved the #217 reply only, and permission is per-action. Ask before
posting.

### New traps and tools

- **CLAUDE.md trap 23** — fixture staging in the wrong SHAPE renders a state that is REAL,
  so the shot looks correct and is a picture of something else. Cost two wrong screenshots
  in one sitting: the wiki cache keyed on log names (`an asp`) when lookups use stored names
  (`Asp`), so the app quietly fetched the LIVE wiki; then wikitext as free prose when
  `EqlWikiMobs.Parse` reads only `{{Namedmobpage}}`'s `known_loot`, so all thirteen creatures
  read "page lists no loot". **Predict the shot's numbers before running it.**
- **Two new shots:** `wiki-pack` (seeds `wiki-cache/mobs`, offline and deterministic) and
  `sky-item-search` (`EQBUDDY_QUESTS=sky:<query>` — the hook now takes a search after the
  colon, because the item layout exists only while a query is live). Both reviewed in
  ParchmentBrass and Solarized.
- **`quest-search.png` in `docs/screenshots/` is a hand-taken orphan** — no shot writes it
  and no doc embeds it. Do not reuse that name (trap 21).

### Scribe is getting better — say so

Its `Checked:` lines were accurate for the first time (3/3, verified independently), and its
"where it might live: `QuestChecklistLayout` (shared)" hypothesis for #108 was exactly right.
Both taken items are deleted from `SCRIBE.md` and written up in `SCRIBE-FEEDBACK.md`, with
the two asks for next compile: **name the DATA SOURCE, not just the control** (that is what
would have surfaced the Ask 1/Ask 2 coupling), and **check the release notes before writing
"already shipped"**. David, 2026-08-19: *"please keep providing feedback to Scribe so he can
get better at supporting you."*

### 3. Gate 5 — the Avalonia widget is DONE (and the biggest file finally has a ratchet)

`EQBuddy.Avalonia/MainWindow.cs` is on **both** ratchets now: 30 glyphs → 0, 91 literal
sizes → 0, `DesignRatchetTests.Migrated`, and a hotspot entry it never had.

**Two findings worth carrying:**

- **It was the LARGEST file in the repo (5,127 lines, ~700 more than the WPF widget) with
  no ratchet at all.** Missed because the hotspot list was written while the WPF
  decomposition was the work in front of us, and nothing since re-read the list. Worth
  asking of any ratchet: is the list itself still the right list?
- **Off-scale values were snapped by COPYING the WPF twin's answer** (the hand-nudged KPI
  cell, the grip hairline, the KPI font size), never re-decided. A migration that invents
  its own answer to a settled question is how two builds drift apart again.

**`WidgetSheetTests` is new and is the point:** the Avalonia lane had NO way to look at its
own widget, so every screenshot lesson this repo has paid for was learned where it could
not be checked on the side that ships to Wine. Opt-in, like `IconSheetTests` — the command
is in CLAUDE.md. It caught two things in its first ten minutes: a capture of **David's live
profile** (an arbitrary, unseeded one — spotted by the character name, which is itself
fine) and a rule name drawn on top of
its own countdown. Neither was visible to 241 passing tests.

**Gate 5 remains:** the three heavy card BODIES (sparkline, breakdown lists, ding unlocks —
these are what buy hotspot headroom on both sides), then **5d**, `Theme.xaml`'s 6 glyphs
inside shared `ControlTemplates`. And the Avalonia hotspot entry should come DOWN the way
`SessionStats` did — by lifting card bodies out, `LootCardView.cs` being the worked example
on that side.

**One parity gap found and NOT fixed:** `EQBUDDY_EXPAND` takes card keys on WPF
(`loot,motes`) and only `1` on Avalonia, so a single Avalonia card cannot be photographed
alone. Small, and it would make the new capture surface sharper.

---

### 4. Gate 5b — WHY the three heavy card bodies are still in MainWindow

Measured 2026-08-19, and this is the finding: it is not that nobody got to them. **`CardRow`
cannot model what they need.**

`EqCardRows.Fill` is what replaced `FillList` everywhere else, and `CardRow` is name, value,
indent, note, item-ness and value ink. The Progress/Combat/Healing bodies need two things it
has no field for: a **per-row tooltip** (an AA row shows the wiki effect, a spell row shows
which classes get it and when) and a **per-row click** (a spell row opens its own page, an AA
row opens the single AA page). So they still call `MainWindow.FillList`, which has
`tooltip:` and `onNameClick:` parameters — and that is the real reason those bodies cannot
move: the surface would have to reach back into the window for its drawing routine, which is
exactly the dependency the seam exists to cut.

**The decision to make before lifting any of them** (do not just start the lift):

- **Extend `CardRow`** with `Tooltip` and `Click` — touches the shared row model that four
  surfaces already use, so it is the honest fix but wants care; OR
- **give `EqCardRows.Fill` optional lookups** (`Func<string,string?> tooltip`,
  `Action<string> onClick`) mirroring `FillList`'s signature, leaving `CardRow` alone.

The second is smaller and keeps the row model a pure data record; the first puts the fact on
the row that owns it. Either way it lands BEFORE the bodies move, not during.

**Already done and safe to build on:** the Progress card's reach-backs are gone
(`LevelUnlockRows` in UI.Shared, 8 unit tests), and its rendered shape is pinned from a
launched app — `EQBUDDY_EXPAND` dumps `dingShown/dingRows/nextShown/nextRows/aaNew/aaAll`
and `ProgressCard_DrawsItsUnlockListsOnADing` asserts the three conditions a move could
silently drop. That assertion passed against the OLD code first. The safety net is installed;
only the `CardRow` decision is in the way.

### Releasing: David cannot test Avalonia, so do not wait for him to

**David, 2026-08-19: *"I can't run Linux or macOS. I only have Windows. others will need to
give feedback there."*** So Avalonia changes are never verified BEFORE a release — only
after, by Linux/macOS reporters (DonThompson, KoboldCoterie, quasarj, sbaum23). Holding a
release for Avalonia verification is waiting for something that cannot arrive.

→ Ship on the headless evidence that exists (`WidgetRenderTests`, a `WidgetSheetTests`
capture, CI's Linux build), **say plainly in the release notes which changes are
Linux/macOS-side, and ask those reporters to look.** Getting it in front of them IS the
verification step. The unreleased Avalonia work (30 glyphs → vectors, 91 sizes → tokens) is
waiting only on Gate 5 being a complete capability, which was his call: *"I'm okay waiting
for a complete capability if we're still midstream."*

---

### STILL THE NEXT TASK — the `/consider` rare-creature signal, still blocked

**Nothing changed here and that is correct.** Neither #185 (n3cr0nk1tt3n) nor #217
(Frankthetankk) has pasted the verbatim con line; we are last comment on both, so nobody is
waiting on us. Asked again in the #217 reply posted today.

**The block is smaller than it sounds, and now measured precisely.** `ConsiderRx` is
`^(?<name>…)(verb).*\(Lvl: (?<level>\d+)\)$` — the `.*` already swallows anything between
verb and tail. So:

- rarity text **before** `(Lvl: N)` → a one-line change to capture a group already matched
  and thrown away;
- rarity text **after** it → the `$` anchor breaks and it is a second pattern.

**That is the entire question.** Do not reconstruct the line (#206). David approved the
feature in principle today: a con-confirmed `rare` outranks a kill-count band, an
unconfirmed band stays a suggestion. **`LogParser.cs` has 25 lines of ratchet room — lift,
don't split.**

### Then, in order

1. **#217 Ask 2 — pool the full logged history** into the wiki pack. **David approved in
   principle today**, account-wide, no per-session toggle. The open question is COST across
   a large archive; if it is felt, the answer is "pool, and say what it pooled", which the
   new window already does. `WikiPackPresentation.ScopeLine` is the one line that changes.
2. **#208 — the Linux/macOS Mobile port.** Its own session. `CompanionEnabled` appears
   nowhere in `src/EQBuddy.Avalonia/` and that csproj has **no reference to
   `EQBuddy.Companion` at all**. Small first step: the per-window "don't fight to be
   topmost" opt-out promised to sbaum23.
3. **Gate 5 continues** — `EQBuddy.Avalonia/MainWindow.cs` (~5,100 lines, the largest file
   in the repo and NOT on the hotspot ratchet, worth fixing while in there), then the three
   heavy card bodies, then 5d (`Theme.xaml` templates).
4. **#191 configurable mini bar** — approved, unblocked.

### Waiting on someone else — do not start these

- **#153** (adndmike) — needs liminalwarmth's volume test with EQ closed.
- **#193** (wizen / n3cr0nk1tt3n) — needs a quested vs token-unlocked achievements export PAIR.
- **#202** (bjstrange) — fixed in 1.94.1; he should confirm the flicker is gone.
- **#215** — server rollback. David: *"bigger fish to fry"*, `someday`.
- **#7 #50 #53 #58 #66** — Don Thompson's Avalonia parity issues. His to close, not ours.

---

## State: 1.94.1 live, every thread answered, nothing in flight (2026-08-19 morning)

`main` clean and pushed, CI green. **2,029 unit + 237 Avalonia + 11 E2E.** No open PRs.
`status.ps1` reads *"none — all 25 open threads have our reply last."* Nothing is
half-done and nothing is waiting on a decision that was already made.

**Hotspot headroom** — `MainWindow*.xaml.cs` 4,422 / 4,864 (442 left, after the Watch
lift). **`LogParser.cs` is the tight one now: 913 / 938, 25 lines.** It is the next file
that will refuse a change, and CLAUDE.md's rule applies to it too — lift, don't split.

### THE NEXT TASK — the `/consider` rare-creature signal

**Two reporters arrived at the same log line from opposite directions in one week, and
the parse site already exists.** That combination is why this is first rather than the
bigger asks below.

- **#185 (n3cr0nk1tt3n)** wants it to SUPPRESS: article-less names include townsfolk, and
  he does not want a spawn chip for every NPC with a proper name.
- **#217 (Frankthetankk)** wants it to CONFIRM: rarity for wiki contributions is currently
  guessed from kill count against published bands, while `/consider` states it outright.
  He took the `known_loot` / `common_loot` question to the wiki admins and came back with
  an answer from the template source — a wiki admin is explicitly supportive of an
  in-game-sourced `rare=true` flag and offered CSS for it.

**Measured, not assumed:** `LogParser.ConsiderRx()` already parses `/consider` — it pulls
`name` and `(Lvl: N)` for the level-range work. **Nothing in the repo reads a rarity
word.** So this is a field we stand next to and do not pick up, and one parse serves both
features.

**Blocked on one thing, and both were asked for it in-thread: the verbatim con line.**
Frankthetankk quotes `a rare creature` from his own log. The existing regex is anchored on
a trailing `(Lvl: N)`, so where the rarity text sits relative to that decides whether it is
one pattern or two. **Do not reconstruct the line** — that is how #206 went wrong. If
neither has replied, the honest move is to wait, or ask again; #192 and #207 both went
report → fix in one step precisely because the exact string arrived first.

When it does arrive: the parse belongs in Core, the "observation beats heuristic" rule
already exists (typed spawn timers), and a con-confirmed `rare` should outrank a
kill-count band while an unconfirmed band stays a suggestion.

### Then, in order

1. **#217 Ask 1 — move the wiki contribution pack out of Drops by Creature** into
   Data & imports and rename it. **David has not ruled on this**; it was flagged as the
   default plan and he has seen the reply. Confirm before building. Asks 2 (pool across
   full history) and 3 (the rare flag) are scope decisions that are his, and the reply
   says so — do not treat the thread as approval.
2. **#108 — item-grouped Sky search**, "who wants this drop?" as one row per class under
   the item. This is now the ENTIRE remaining scope of #210, verified against the code:
   the other six asks shipped in 1.92.0/1.93.0. liminalwarmth was asked whether to narrow
   #210 to this or close it and carry #108 alone — check for his answer first.
3. **#208 — the Linux/macOS Mobile port.** Approved, and bigger than its inbox entry:
   `CompanionEnabled` appears nowhere in `src/EQBuddy.Avalonia/` and that csproj has **no
   reference to `EQBuddy.Companion` at all**. There is no switch to add. Its own session.
   sbaum23 was also promised consideration of a per-window "don't fight to be topmost"
   opt-out, which is small and does not depend on Wayland cooperating — that is the part
   to do first if the port is too big for the day.
4. **Gate 5 continues**: `EQBuddy.Avalonia/MainWindow.cs` (~39 glyphs, ~104 sizes, and at
   5,100 lines the largest file in the repo — it is NOT on the hotspot ratchet, which is
   worth fixing while you are in there), then the three heavy card bodies, then 5d
   (`Theme.xaml`'s templates).
5. **#191 configurable mini bar** — approved, and unblocked now Gate 5c is done.

### Waiting on someone else — do not start these

- **#153** (adndmike) — needs liminalwarmth's volume test with EQ closed.
- **#193** (wizen / n3cr0nk1tt3n) — needs a quested vs token-unlocked achievements export
  PAIR. Asked again this morning.
- **#202** (bjstrange) — fixed in 1.94.1, but he should confirm the flicker is gone.
- **#215** — server rollback. David: *"bigger fish to fry"*, `someday`.

### Five stale-ish trackers worth a look, not a close

`#7 #50 #53 #58 #66` are Don Thompson's own Avalonia parity issues, untouched since
15 August while that lane moved a lot — #213 landed there, and the Companion finding above
is news to it. They are his to close, not ours. A status note would be welcome; do not
close them.

---

## Earlier: Gate 5c FINISHED (2026-08-19). 1.93.2 was live at the time

**2,000 unit + 231 Avalonia + 10 E2E green.** All four widget files are on
`DesignRatchetTests.Migrated` — `MainWindow.xaml`, both `BreakoutWindow` files, and now
**`MainWindow.xaml.cs`**, the 4,571-line hotspot that §11.8 predicted could not join.
Full write-up in `docs/DesignSystem.md` **§11.10**; the two new traps are CLAUDE.md 21–22.

**Then four fixes landed on top of it, all from David testing the build (2026-08-19):**

| What | Why it is worth reading |
|---|---|
| **Slow chip stops wearing the respawn hourglass** | He spotted one picture doing two jobs. The real repair was `IconSheetTests` — nothing in the repo could SHOW an icon, which is why the snail got cut on a guess the day before. It renders every icon at 12px and 24px now; the snail really does die at 12px, and there is a picture proving it |
| **#93 — the Mac update banner handed out the Linux tarball** | `UpdateOffer` took a single `bool isWindows`, so "not Windows" silently meant Linux. The Mac artifacts have been on every release since the workflow added FOR that discussion. It is an enum now, so a fourth platform is a compiler error rather than a silent inheritance |
| **A five-letter ability name stopped hoarding two thirds of the row** | #182's fix over-corrected: proportional columns take their share whether they need it or not. `BreakdownRowLayout.NameCap` caps instead of allocating — and `NameWidth`, which it uses, **had unit tests and no caller**. Trap 20, third time in three days |
| **The WPF and Avalonia builds can no longer both run on one profile** | The cause of his port error. A guard implemented per TOOLKIT guards nothing: WPF had a named mutex, Avalonia had a lock file, neither could see the other. Standing down was also a *crash* on the Avalonia side and had been since the guard landed. See trap 13's second arrow |

**Unreleased player-visible work exists now** — chip icons, the Watch card's sort strip,
the alert banner's lost ★, reworded tooltips, the breakdown row widths, the Mac update
link, and the single-instance fix. Whatever release ships next needs a `WhatsNew.json`
entry covering the lot, crediting **Amatyr (#93)** and **sbaum23/David** where due. Nothing
has been released; source only, as always.

**Still open from that testing round, and NOT fixed:** the fight-side chip stack has never
been seen on Windows — no fixture produces a live mez, slow or spawn timer, so the two WPF
chip windows have no test and no shot. `SCRIBE-TESTING.md` names the job that would close
it (seed a named kill and a mez into the fixture log; propose-and-check, because the
fixture feeds E2E).

### NEXT — in this order

1. **`EQBuddy.Avalonia/MainWindow.cs`** — the other 4.5k-line widget, ~39 glyphs and ~104
   literal sizes. Two of its glyph sites were already fixed in passing (the mez/slow chip
   icons and the buff-set "missing" label) because the parity rule required it, so the
   count is slightly lower than the last measurement. Same two-pass shape: sizes, look,
   glyphs, look. Its own screenshot path is `tests/EQBuddy.Avalonia.Tests` render tests
   plus `CaptureRenderedFrame`, not `shoot.ps1`.
2. **The three heavy card BODIES** — sparkline, breakdown lists, ding unlocks. These want
   their own session and they are the ones that buy hotspot headroom (§11.9's seam).
   `FillList` is the shared drawing routine they all still use; `EqCardRows` is what
   replaced it everywhere else.
3. **5d — `Theme.xaml`'s 6 glyphs**, inside shared `ControlTemplates`, so they belong to
   no single card.

**Hotspot headroom is down to 130 lines** (`MainWindow*.xaml.cs` 4,571 against a 4,274
baseline, limit 4,701). Pass 2 spent ~100 of it, nearly all on comments. The next change
in that file should be a LIFT, not an addition — see CLAUDE.md's note on why another
partial buys nothing.

**Open in `SCRIBE.md`, still untaken:** item-grouped Sky search (#108/#210, "who wants
this drop?"). **#191** (configurable mini bar) is approved and was deferred until Gate 5
finished — Gate 5c is done, so it is unblocked as soon as 5d lands; it reworks the bar
`MiniBarPresentation` now owns.

**And one that is bigger than its inbox entry: #208, the Linux Mobile switch.** Scribe
guessed "the toggle is missing from Avalonia Options". Measured 2026-08-19:
`CompanionEnabled` appears nowhere in `src/EQBuddy.Avalonia/`, and that csproj has **no
reference to `EQBuddy.Companion` at all** — there is no server in that build to switch on.
It is a port, not a checkbox, and it deserves its own session. Worth doing: the two things
CLAUDE.md calls EQBuddy's only uncontested ground — the phone and the Linux/macOS build —
currently cannot be used together.

---

## Run things in the form the allowlist grants

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File C:/Users/david/source/EQBuddy/scripts/release.ps1 -Tag vX.Y.Z
```

`check.ps1`, `status.ps1`, `shoot.ps1` and `shot.ps1` take the same shape and work through
Bash. `shoot.ps1 -Shot a,b,c` needs `pwsh -Command "& '…/shoot.ps1' -Shot a,b,c"` — the
`-File` form passes the list as one string. Chaining with `&&` sometimes trips the
classifier where the same commands run fine apart; split them.

---

## State: Gate 4 + four fixes, shipped as 1.91.0

`main` carries Gate 4 (Loot) plus four community fixes — #135, #182, #189 and #197.
**1,834 unit + 207 Avalonia + 10 E2E green.**

All four discussion replies are posted, including the correction on #182 where a wrong
public diagnosis had to be withdrawn.

Three releases went out on 2026-08-17:

- **1.89.0** — Gate 2 (Quests rebuilt as list + detail pane), plus liminalwarmth's #198
  loot provenance, #199 mini-bar double-click and #200 (Disabled) alert sound.
- **1.90.0** — Gate 3 (Spawns rebuilt with progress bars and a state-aware countdown),
  plus quasarj's #194 CrossOver overlay fix.
- **1.91.0** — Gate 4 (Loot), plus #135 item-clicky charms, #182 breakdown rows,
  #189 auto-hide satellites and #197 sound formats.

---

## Gate 4, as built — Loot. Full write-up in `docs/DesignSystem.md` §11.7

Four files joined `DesignRatchetTests.Migrated`:

| File | What it is |
|---|---|
| `UI.Shared/LootPresentation.cs` | The decisions, once: strip options + tooltips, view/sort normalization, strip visibility, empty-slice wording, both headers, the target heading. **34 unit tests where there were none** |
| `EQBuddy/LootCardView.cs` | The widget's Loot card, lifted out the way `QuestChecklistView` was |
| `EQBuddy/LootBreakoutView.cs` | The Loot breakout's contents, lifted out of the six-kind `BreakoutWindow` |
| `EQBuddy.Avalonia/LootCardView.cs` | The Linux/macOS card — **which was a whole feature behind** |

Three things worth carrying forward:

1. **The strips were the symptom.** The duplicated *rules* were the disease: which strips
   are up, which chip is lit, whether "recent" is offered, and what an empty slice says
   were derived twice from the same four lists and had already drifted. When a gate's
   surface looks like a paint job, check whether the same decision is being made in two
   places — that is where the value is.
2. **The Avalonia card had never called the shared row builder**, so #198's filters and
   provenance simply were not there. Worth assuming, on every gate, that the other lane is
   further behind than the file list suggests.
3. **The screenshot review earned itself for the third gate running.** The Loot breakout's
   strips were built, selected, painted — and invisible, because the XAML host they hang in
   was declared `Visibility="Collapsed"` and only the panel inside it was ever toggled.
   That is trap 15 now.

Two new shots exist: `shoot.ps1 -Shot loot-card` (via `EQBUDDY_EXPAND=loot`, which now
takes card keys as well as `1`) and `-Shot loot-breakout` (no hook needed — the window
shows whenever the widget is minimized and its stat is starred, both plain settings).

---

## CharmTracker is out of SessionStats (2026-08-18)

`EQBuddy.Core/CharmTracker.cs`, 550 lines, and the ratchet baseline came down **2,766 →
2,375** with it. `Apply()` went from 787 lines to about 570. `MezTracker.cs` was the
precedent; this is the same move for the same reason.

**How it was verified**, because "behaviour-preserving" is a claim and not a fact: all
seven logs bjstrange attached to #135 were replayed before and after, tracing every
charm-state transition and every watch-rule label they produced. The two traces are
identical byte for byte. Do that again for the next refactor down there — the logs are
public on the discussion and the harness is twenty lines.

`CharmTrackerTests` is the half that could not be written before: 18 cases that ask the
state machine a question without building a session.

**Two process notes worth keeping:**

- The audit that led here found the SessionStats ratchet entry was a **literal path** while
  MainWindow's is a glob, so `SessionStats.Tracked.cs` had never been counted. Check the
  shape of a ratchet entry before trusting its number.
- Writing a test file with a shell heredoc failed exactly as `CLAUDE.md` says it will.
  Use the editing tools.

### Open: charm4.txt still reports no held time

Found while building the corpus, NOT fixed, and deliberately not guessed at. bjstrange's
charm4.txt replays with no `held` on its break — the charm is never claimed at all, so
there is nothing for the wear-off to measure. The public reply on that log addressed the
BREAK (two creatures sharing a name); the claim never happening is a different question.

A first look says `_petName` was already set when the landing arrived, so the
unknown-cast candidate path was skipped — but that is a hypothesis, and the last two times
this thread was reasoned about rather than replayed, the reasoning was wrong. **Replay it
and print every state change before touching anything.** A synthetic test written from a
guess passed while the real log failed, again, during this very session — it was written,
seen to pass for the wrong reason, and deleted.

---

## Gate 5a–5b history. Full write-up in `docs/DesignSystem.md` §11.8–§11.9

**Superseded in part by the section at the top of this file: 5c is finished.** The
prediction below that `MainWindow.xaml.cs` "probably cannot" join the ratchet was wrong,
and §11.10 records why — the count was mostly comments, and the concession that looked
reasonable (exempt string literals) would have exempted the rule's own target.


**Gate 5 does not fit in one change.** Measured before starting: **473 ratchet violations**
across the two widget files and their Avalonia twin — 127 literal font sizes, 174 spacing
tuples, 167 glyphs, over 10,400 lines. The ratchet is per-file and all-or-nothing, so the
gate has to be staged by VOCABULARY (finish one shared thing everywhere it appears) rather
than card by card. 5a did the two things every card has: **the fourteen headings** and
**the sort strips**. 427 left.

Both landed in both UIs, and the screenshot review earned itself twice inside one gate —
the strips first OVERLAPPED their headings (one-cell Grid, fine as four small words, a
collision as pills), then TRIMMED them ("Damage b…") until the redundant "sort:" caption
went. Neither is visible in a diff or a test.

**5b has started: the card SEAM exists and is proved on the Kills card** (§11.9). The
lesson that shaped it: lifting files was moving lines without moving dependencies —
`MainWindow` carries **61 internal members**, most of them there so a lifted view can reach
back. `IWidgetCard` + `ICardContext` (six methods, implemented explicitly) fix that, and
`KillsPresentationTests` is the first card content ever asserted without launching a
window. Convert the remaining cards onto that seam, one at a time, presentation into
UI.Shared first.

**Batch one converted: Motes, Money, Faction** (plus Kills with the seam). Two shared
things came out of it and should be spent, not re-invented, by every later batch:
`UI.Shared/CardRow.cs` (what a row IS — name, value, indent, note, item-ness, value ink)
and `EQBuddy/EqCardRows.cs` (the one place a row is drawn, replacing `FillList` and its
per-surface copies). A card's `Item: true` rows get the wiki click, the stats hover and the
quest badge through `ICardContext` and nothing else does.

**Batch two: Combat, Healing and Progress SUMMARIES** moved to
`UI.Shared/CombatPresentation.cs` and `ProgressPresentation.cs` with tests. Their bodies
did NOT move — those three own the sparkline, the fight split, the breakdown lists with
their resist/blocked lookups and the ding-unlock rows, which is heavy WPF machinery and a
separate job. The summaries were the prize: a dozen conditional fragments each, on the
cards a player reads most, and the densest untested text left in the app.

**Batch three started, and stopped at a safe point rather than half-done.**
`EQBuddy/EqIcon.cs` is the XAML-addressable icon (`<local:EqIcon Glyph="Copy"/>`) — WPF's
`Path` is sealed, so it wraps one — plus Bolt, Paw and Phone in `IconPaths`. The Combat
card's ⧉/⧗ buttons and the 🐾/⚡ star glyphs are vectors now.
`MainWindow.xaml` is at **79 literal attributes and 16 glyphs**, down from 87/20.

**Read this before continuing — the finish line is not where §11.8 assumed.**

`MainWindow.xaml` can be made ratchet-clean: the remaining 16 glyphs are chevrons in
toggle labels (▸/▾, flipped from code), three menu headers, and **six occurrences inside
XML COMMENTS**, which the ratchet counts because it scans every line. All are convertible
or rewordable.

**`MainWindow.xaml.cs` probably cannot, and that is a real finding rather than a shortfall.**
It carries ~74 glyphs and most of them are not controls: they are inside user-facing
STRINGS — alert text, tooltips, "re-enable in ⚙ Options → Breakout windows". `CLAUDE.md`
explicitly permits emoji in "user-facing TEXT where they are content rather than UI", and
`DesignRatchetTests` cannot tell the two apart. So either those strings move to a resource
the ratchet doesn't scan, or the glyph test needs a way to exempt string literals, or that
file joins the list only after a deliberate pass over its copy. **Decide which before
starting 5d** — it changes what "Gate 5 complete" means.

**Remaining cards: Gear, Watch, Buffs, Raids, Travels & Deaths** — plus the three bodies
above
— then 5c (chrome) and 5d (`Theme.xaml` templates).

**The old note, still true for the rest: 5b — the card bodies.** Lift surfaces into their own files the way `LootCardView`
was; that is the only thing that buys hotspot headroom as well as ratchet coverage. Then
**5c** the chrome (carries #191, and §8b's reserved widths are non-negotiable — #173), then
**5d** `Theme.xaml`'s templates, where the ⭐ and ▸ glyphs live inside shared
ControlTemplates and so belong to no single card.

---

## THE OLD NEXT TASK — Gate 5 of the UI/UX rework: the main widget

`docs/DesignSystem.md` §11.5 is the amended order; §10, §11.6 and §11.7 are the three
worked examples. Gate 5 is the widget itself — the card chrome, the thirteen card headers,
and the **~14 hand-built segmented strips still in `MainWindow.xaml`**.

Two things Gate 4 deliberately left for it, and the reasons matter:

- **Card headers were not touched.** Thirteen cards wear the same `Section` expander and
  the same emoji-and-count header; migrating one of them reads as a bug rather than a
  migration. They change together or not at all.
- **`MainWindow.xaml.cs` still cannot join the ratchet**, which is why Gate 4 lifted its
  surface out rather than migrating in place. Gate 5's real deliverable is getting that
  file onto the list — and `LootCardView`/`QuestChecklistView` are the pattern for how.

The hotspot ratchet has room: `MainWindow*.xaml.cs` is 4,507 lines against a 4,274
baseline (limit 4,701). Gate 4 took 86 lines out of it and did not go under the baseline,
so the baseline is unchanged — but a gate that lifts several more surfaces will, and the
rule is to lower it in the same commit.

## Capability restored: turning a Plane of Sky reward in (2026-08-18)

**A reorganisation cost a feature, and nothing caught it.** The widget's Sky card carried a
per-REWARD turn-in check. When that card became a launcher (2026-08-16) and the tracker was
rebuilt around a list and a detail pane, the per-ITEM ticks came across and the per-reward
one did not. `AppSettings.SkyQuestCompleted` kept being READ — by `QuestChecklistLayout`,
by both desktops and by EQBuddy Mobile — while the only thing left that could WRITE it was
the achievements import. A player who turned a reward in and had no achievements export to
paste could not say so: every piece ticked, the reward permanently "ready", the Sky counter
unable to move past it.

`UI.Shared/SkyCompleteToggle.cs` restores it, beside `EpicCompleteToggle` — the ASYMMETRY
between those two is what let it go missing. The old card's rules are kept verbatim because
they were right: turning in acquires every item in the reward and resolves any parked
auto-tick, and reopening leaves the item boxes alone (a mis-click costs one click to undo,
not six). `QuestChecklistGroup` now carries `CompletionKey` and `Completed`, so a view asks
the layout rather than parsing the note string, and Epic groups carry no key — its
completion is per class, and a turn-in button must not appear there by accident.

Surfaced as **"Mark turned in" / "Reopen"** on the reward heading in both Quest Trackers,
matching the General tab's own turn-in button from Gate 2 rather than inventing a control.

**Visually inspected 2026-08-18** — `shoot.ps1 -Shot sky-checklist` stages all three reward
states on one screen (turned in → "Reopen", every piece held → "Mark turned in", part
collected → neither) and `EQBUDDY_QUESTS=sky` opens straight onto the tab. Staging it found
a SECOND bug, in Core rather than in the new code: `ApplyDefaultSkyQuestChecklist` refreshed
a row's NPC, reward, item and source by Id but never its **ClassName** — and every surface
groups and filters by class, so a row whose class drifted from the catalog was invisible in
all of them while its tick sat in `settings.json`. Fixed and pinned.

**Worth generalising:** this went missing because the DATA survived the move and only the
WRITE path did not, which no test and no ratchet can see. When folding a surface, check
what still writes each setting it owned — a setting that only readers touch is the
signature.

## Debts and open threads

**Owed publicly, from replies posted 2026-08-17.** These are commitments, not ideas:

| # | Reporter | What | Where it belongs |
|---|---|---|---|
| #135 | bjstrange | **DONE in 1.91.0.** Replayed charm7.txt: an item clicky prints no cast line, so nothing recorded the landing and the wear-off had nothing to measure. The caster-only "Master" tell starts the clock now; his file gives "held 0:19". | closed |
| #182 | Ladylag | **DONE in 1.91.0, and my public diagnosis was WRONG.** The `.` rows are not a parser failure — see below. Name column, hover text and resize band all fixed. **The correction is posted.** | closed |
| #189 | wizen | **DONE in 1.91.0** — every window follows the widget's auto-hide now, by a deny-list so later windows follow too. The settings-across-updates half is still waiting on his `error.log` (trap 13); his paste showed no overwrite line. | half closed |
| #197 | wizen | **DONE in 1.91.0** — one shared list, six call sites; Windows had two formats and Avalonia already had three. | closed |
| #192 | wizen | Waiting on his exact forage line — if Legends writes "some", the regex misses it and that's a one-line fix. | Waiting on him |
| #202 | bjstrange | Mobile loot/watches card refresh loop. I checked the loot fingerprint and it has no clock in it, so my first hypothesis is dead. Four questions asked; waiting. | Waiting on him |
| #190 | wizen | **Approved:** tracked-quest chips — double-click opens the tracker with that quest selected, right-click dismisses. | Gate 6 |
| #191 | TheMegaSage | **Approved:** the mini bar's contents become configurable and removable. §8b's reserved widths are non-negotiable (#173). | Gate 6 |

**Still worth doing on Gate 3:** the fixture has no running timer in a catalogued zone, so
the progress bar is unit-tested but has never been *seen*. Seeding one named kill into
`tests/fixtures/eqlog_Testchar_fixture.txt` would close that.

---

## Findings worth not re-learning

- **A component nobody can reach gets rebuilt by hand.** Gate 2 built the chip primitive
  and left it private inside `QuestsWindow`; six hours later #198 hand-built two more. That
  is why gate 2b exists and why anything shared goes somewhere reachable immediately.
- **`Auto` columns lie in a header row.** A header has no buttons, so an `Auto` action
  column measures zero there and ~115 in a row — every label lands left of the column it
  names. Fixed lanes also stop rows reflowing when a button appears mid-edit.
- **A progress bar in one column is a sliver.** David, 2026-08-17: *"we have room between
  the columns."* Span it across the row.
- **#193's damage cannot be repaired.** Wildcard ticks went through the normal path and are
  indistinguishable from honest ones. The reply says so plainly; don't promise a cleanup.
- **Replay the reporter's actual log file.** A hand-condensed charm5.txt passed while the
  real one failed; same for charm6 and the #183 mez log. charm7 makes it seven for seven.
- **Look at the reporter's SCREENSHOT before believing your own diagnosis.** #182's rows
  reading `.` and `..` were called a parser bug in public — by me — and they were nothing
  of the kind: the name column was starved to its ellipsis by a stat line that took
  whatever width it liked, which the same screenshot proves, because "Damage shield" (short
  stat line) printed in full three rows below one that printed nothing. The correction is posted.
- **Check when a fix shipped before agreeing it is broken.** The same thread has me
  accepting "drag only works on the bottom edge" as a defect I had got wrong. Edge resize
  landed in 1.35.0 and works; the band was six pixels wide and unmarked, so the corner grip
  was the only findable way in. The honest fix was a wider band, not a new feature.
- **A host that hides itself is a second switch.** Gate 4's breakout strips were correct,
  selected and never once shown, because the `ContentControl` they hang in was declared
  collapsed in XAML. When you lift a surface into a class, its host gets no `Visibility`
  and no `Margin` — the lifted control carries both. Trap 15.
- **`EQBUDDY_EXPAND` takes card keys now**, not just `1`. A card's expanded state is not
  persisted, so before this the only way to photograph one card BODY was to open all
  thirteen and hope it fit above the fold. It didn't.

---

## Hard lines (see `CLAUDE.md` for the full set)

- Never measure other players. Values line, not technical.
- Releases wait for David's explicit go. Ask "want me to cut it?" — don't hand him a
  command block; that once had two people release two minutes apart.
- Curated catalogs are never auto-written; **learned** data is.
- eqlwiki is the tie-breaker; other sources where it's silent, marked as such.
- A `UI.Shared`/Core fix must reach **both** UIs in the same change — that is what carried
  #122 and #152 to Linux.
- Every player-noticeable change earns a `WhatsNew.json` entry in the release that ships
  it, crediting the reporter by name and discussion number.
