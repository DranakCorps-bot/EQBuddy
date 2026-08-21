# EQBuddy — handoff

**Don't re-derive the codebase.** `CLAUDE.md` loads automatically and carries the commands,
the non-negotiable rules, the where-things-live index, the trap list (38) and the
surface-allocation rule. `docs/Architecture.md` and `docs/TestPlan.md` sit behind it, and
`DocumentationTests` fails the build if any go stale. Start with
`pwsh -NoProfile -File scripts/status.ps1`.

---

## 2026-08-21 (latest): a bug-fix sweep, and the V2 routing used for the first time

**1.99.1 is built and NOT released** — `Directory.Build.props` says 1.99.1 with a What's-new
entry. Gates: **2,269 unit + 264 Avalonia**. Waiting on David's go.

### What the sweep produced

- **#228 (joeymavity) — a cleared respawn timer stays cleared. FIXED.** The previous handoff
  could only say "something re-creates it"; it is `LogWatcher.Select`, which is a **full-file
  ingest**. Every kill line replays through `Apply`, and `Upsert` had nothing to consult, so
  it rebuilt the timer from the very kill that had just been dismissed. "Randomly" is the
  restart. `Clear` now records WHICH kill was dismissed and persists it in a sibling file.
  Trap 20's family again: state removed, decision not kept.
  **Two mistakes worth remembering from doing it:** the dismissal must key on the kill's own
  timestamp (a replay hands you that same `KilledAt`; the click time lets an earlier kill walk
  back in), and it must AGE OUT on when the player decided, not on the kill — pruning on the
  kill time discards a dismissal of an old kill the instant it is made. Its own test caught
  that.
- **#101 (Frankthetankk) — the automatic achievements import obeys the auto-grant guard.**
  It always did; nothing said so. Now `OutputfileAutoImportTests` runs the AUTOMATIC path
  against wizen's three-way dump. Verified by disabling the guard and watching it fail.
- **#173 (KoboldCoterie) — closed.** Force-disabling block compositing on KWin was his own
  fix; the thread produced trap 12 along the way.
- **#109 follow-up and #226 — investigated, NOT built. Both are `FABLE.md` stubs.**

### The V2 routing, used for real — read this before taking either stub

Two items are in `FABLE.md`, both `waiting` on a Fable 5 plan and David's `approved`.
**Do not implement them without that.** Both carry confirmed causes, and both record where
the *obvious* fix is a trap:

1. **Per-page wiki re-check (#226).** Cause confirmed: `EqlWikiMobs.CacheLifetime` and
   `EqlWikiItems.CacheLifetime` are both `FromDays(7)`, so correcting a wiki page — the thing
   the ✦ marker *asks* the player to do — cannot clear the flag for a week. The `+N` tier
   theory is disproved. V2 because it puts on-demand network I/O behind a cache-only surface
   and could burst requests at a volunteer wiki.
2. **Plane of Sky spawn types (#109).** Sky **is** in `RaidTargets.json`; `The Spiroc Lord`
   and `Bazzt Zzzt` are listed while `The Spiroc Guardian` and `Bzzzt` are not — exactly the
   two in the screenshot. **Do not just add them:** that list also drives the Raids card and
   means "raid target you can clear", so it would list trash as raid bosses (trap 4). The
   domain is missing `chained` and `player-triggered` spawn types.
   **Waiting on the reporter** for the zone-enter line a personal Sky instance prints —
   `IsInstancedZoneName` only knows `"<zone> N (Adjective)"` and `"- Solo|Group"`, and if Sky
   matches neither, the zone-level rule can never fire there.

`FABLE-FEEDBACK.md` asks Fable to say where the V2 line actually sits. That question is open
and it matters more than either stub.

### Still open

- **#210** (liminalwarmth) — the Sky tracker's lost cross-class workflow.
- **`docs/proposals/InlineThemes.md`** — decided in shape, nothing built. `FABLE.md`-class.
- **#222's open question** — bjstrange has been told the pull is a snapshot request rather
  than the native reload, and invited to say if the difference is felt. Waiting on him or Bevel.
- Eight feature-request discussions await a reply: #217, #224, #208, #114, #159, #120, #94,
  #185. None are bugs; none have been answered this round.
- **`LogWatcher.FinishInitialIngest`** `ObjectDisposedException` at shutdown; `LanAddresses()`
  on Tailscale; `LogParser.cs` at 14 ratchet lines.

---

## 2026-08-21 (latest): 1.99.0 IS RELEASED, AND THE OPERATING MODEL CHANGED

`v1.99.0` is **shipped** — signed as `CN=FlossworksCross-Stitch`, verified and timestamped,
on OneDrive and as a GitHub release. Gates at the tag: **2,266 unit + 264 Avalonia + 18 E2E**.
The Quests bug that was holding it is fixed. Four threads answered (#226, #227, #222, #228).

### READ FIRST — how work is routed now

**David added a business/operating architecture on 2026-08-21.** The parts that bind you are
in `CLAUDE.md` under *How work is routed* and *The inboxes inform you*. In one line each:

- **V0–V1 (most things) — you plan and implement.** Inbox: `SCRIBE.md`.
- **V2–V3 — Fable 5 plans → David marks `approved` on `FABLE.md` → you execute.**
  `FABLE.md` and `FABLE-FEEDBACK.md` are new and currently empty.
- **When you judge something V2/V3 mid-session, STOP before implementing it**, stub
  `FABLE.md` with why it is not V0–V1, and carry on with V0–V1 work. That was David's
  explicit answer to a direct question — finishing it anyway and labelling it V2 in the
  summary is the option that guarantees the handoff never gets tested.
- **The three inboxes inform, they never trigger.** What authorises work is David asking in
  session. `approved` is his mark and never yours. Anything running unattended must not take
  work from those files at all.

**The business documents are deliberately not in this repo and must not be.** EQBuddy is
public; the operating docs live elsewhere and David scrubbed the private repo's NAME out of
`FABLE.md` within an hour of adding it (445fc56). Do not add a link, a repo name, spend
figures, or legal/DBA posture to any public file here. If you need the operating docs, ask
David — do not paste them in.

**Today's Quests fix would have been a `FABLE.md` item under the new rule** (it changed the
client/server sticky-payload contract). It shipped because David explicitly said ship. The
next one like it goes to Fable first.

### What landed today (after the previous handoff)

- **EQBuddy Mobile's Quests surface could never load once you added it** — `d9fc809`.
  Two halves, and only the first was in the previous handoff's lead:
  1. `ForClient`'s memo was a claim about the DEVICE; the page's is a claim about the LAST
     PAYLOAD. `CompanionClientState.HeldQuests`/`HeldMap` fix that. **The map had the
     identical hole** — drop it from the picks, re-add it in the same zone, blank map.
  2. **The repaint gate was the second half, and reasoning did not find it — the harness
     did.** `setCatalog` is a side effect of a PAINT, and the gate excluded `catalog` from
     its key (#202), so a panel painted without a catalog could never be filled on that page
     load *by any server*. Presence is in the key now; content still is not.
  Both reproduced in `scripts/mobile-harness.ps1` driving the shipped page through the real
  ⚙ picker. **Trap 38** records it.
- **`CLAUDE.md`: the routing and trust sections above** — `5d2922d`.
- **#226 diagnosed, not fixed** (below), and `SCRIBE.md`/`SCRIBE-FEEDBACK.md` updated.

### DO FIRST — the two open bugs, both with the cause already found

1. **The wiki ✦ flags are stale for up to seven days** (#226, Frankthetankk + LeBigNasty).
   **Cause confirmed, fix not written.** `EqlWikiMobs.CacheLifetime` and
   `EqlWikiItems.CacheLifetime` are both `TimeSpan.FromDays(7)`, so a wiki correction cannot
   reach a flag on a machine that has viewed the page recently. The `+N` tier theory is
   **disproved** — `WikiContribution.Classify` folds both sides through
   `QuestCatalog.BaseItemName`, and LeBigNasty's screenshot has tiered items on both sides of
   the flag. The fix is a **per-page re-check**: on a flagged row, and before the pack window
   exports. A re-check button was queued in #65 and never built. Both threads are answered
   with all of this, and LeBigNasty was asked whether he corrected those pages himself.
2. **Respawn timers re-open after being cleared** (#228, joeymavity). `SpawnTimers.Clear`
   genuinely removes the entry, so something re-creates it. Not started. The thread now asks
   what people were doing in the minute before it came back.

### Also open

- **`docs/proposals/InlineThemes.md`** — shape decided (tab strip, Bevel's split and host
  rules). Open questions 4 and 5 remain; nothing is built. **This is a `FABLE.md`-class item
  under the new routing** — it is cross-cutting and touches both UIs.
- **#210** (liminalwarmth) — the Sky tracker's lost cross-class workflow. Still open.
- **`LogWatcher.FinishInitialIngest`** throws `ObjectDisposedException` on a timer at
  shutdown. Low severity, not chased.
- **`LanAddresses()` on a Tailscale machine** — the QR advertises `BoundAddresses[0]` only;
  a `100.x` QR is unreachable from a phone. Worth confirming it picks the Wi-Fi address.
- **`LogParser.cs` has 14 ratchet lines left** — the next file that will need one.

### The voices

**Bevel** (`BEVEL.md` / `BEVEL-FEEDBACK.md`) is **product/UX** — it has now said so. Read it
before designing anything. One open question from #222 is still waiting on it or David
(whether the pull should be the native reload after all); bjstrange has been told and invited
to say if the difference is felt rather than theoretical.

**Scribe** (`SCRIBE.md` / `SCRIBE-FEEDBACK.md`) got its **first confirmed mechanism** today —
the 7-day cache — and it got there by citing what #65 had *established* rather than guessing.
That is written up in the feedback file as the thing to do more of.

**Fable** (`FABLE.md` / `FABLE-FEEDBACK.md`) is new and empty. There is no Fable Grok Bot.

**Read the last comment's signature before replying to any thread.** Scribe, Bevel and you
all post as `DranakCorps-bot`, and David replies in his own words from it too.

### FRESH PASS — David asked for this explicitly

`pwsh -NoProfile -File scripts/status.ps1` first. Then check `BEVEL.md`, `SCRIBE.md` and
`FABLE.md` for items filed since this was written. `docs/screenshots/` is current as of
2026-08-21; trap 21 (a shot name IS a filename) still bites.

### Standing

Post GitHub replies for finished work without asking, signed `— Dranak (Claude Code)`.
**Releases wait for David's explicit go at that moment.** Both UIs in the same change. David
is Windows-only; never hold a release to verify Avalonia. When a decision is his, use the
question tool — he has said twice that a question buried in prose is a question that does
not get answered, and the three questions asked today all changed what happened next.

---

## 2026-08-21 (earlier): 1.99.0 was READY BUT FOR ONE OPEN BUG

`main` pushed at `4788eae`. `Directory.Build.props` says **1.99.0** with a full What's-new
entry. Gates: **2,262 unit + 264 Avalonia + 18 E2E**, all green. Installed on David's
machine and field-tested by him through the day.

**David said "I think we're good to push live what we have now" — and then found the bug
below, minutes later, before it could be released.** He has not re-confirmed since. Treat
the go as REAL but conditional on the Quests bug: shipping EQBuddy Mobile with a dead
surface is the one thing that would undo a release built mostly of trust repairs.

### DO FIRST — EQBuddy Mobile's Quests surface never loads

**David, 2026-08-21, on his phone:** *"Quests does not work though. The window loads, I can
type, but it's stuck on 'Waiting for the quest catalog from the PC'."* Every other surface
works; Mobile is otherwise healthy on his machine now.

**The mechanism is understood; the fix is not written.** Do not restart the investigation.

- `index.html` `drawGeneral()` shows that message whenever its local `catalog` is falsy.
  `setCatalog()` is the only thing that fills it.
- The catalog is **sent once per device and then withheld**:
  `CompanionSnapshot.ForClient` (≈line 120) strips `Catalog` to null when the snapshot's
  `CatalogStamp` equals the per-client `state.QuestCatalogStamp`, and records the stamp the
  first time it sends it.
- The page compensates with a sticky re-attach (`index.html` ≈line 650): if a push has no
  catalog but the stamp matches, it copies the catalog off the PREVIOUS payload.

**The hypothesis, and it implicates something I changed today.** That sticky re-attach needs
a previous payload to copy from. A page RELOAD has none — the JS `catalog` variable is gone.
If the per-client state survives the reconnect, the PC believes the device already has the
catalog, strips it forever, and the page waits forever.

Two things now reload or re-subscribe that page:
1. **The new pull-to-refresh sends `{"kind":"subscribe"}`** (2026-08-21, Bevel's review), and
   `subscribe` replies with `ForClient(client, snap)` — which strips the catalog if the
   stamp was already recorded.
2. **The version-mismatch self-reload** (trap 32) does a genuine `location.reload()`.

→ **Start by checking whether the per-client `QuestCatalogStamp` survives a reconnect or a
re-subscribe.** If it does, the "already sent" memo is scoped to the device when it should
be scoped to the CONNECTION. The likely fix is to clear the stamp when a client subscribes,
so the next payload carries the catalog. That is the smallest change that cannot leave a
page waiting forever.

→ **Reproduce with the harness before and after** — `scripts/mobile-harness.ps1` wraps the
shipped `index.html` with a stubbed socket, so a payload with `catalog: null` and a matching
stamp can be replayed without a phone.

→ **And write the test at the projection/snapshot layer, not the page.** The decision lives
in `CompanionSnapshot.ForClient`, which is pure and already unit-tested.

### What landed today (all on `main`, none released)

- **Kills & Drops theme**, both builds — `CreatureWindow`, `DropsCardView`, `DropsWindow`
  deleted. The Kills card is the door; `Drops by creature…` is off the cog.
- **The 1.98.1 parity gap closed** — Linux/macOS have the Inventory tab; the gear checklist
  lifted out of `EQBuddy.Avalonia/MainWindow.cs` first (baseline 5,127 → 5,422).
- **Motes is a card again** (#227/#228), hidden by default, restored from Options. And the
  defect under it: Options could not reach three of the ten mini-dashboard switches at all,
  because the folds moved their stars into windows. Cards & windows lists all ten now.
- **#226** — creature names are links on the Drops tab and the wiki pack. The app had been
  telling people to click something that had no handler.
- **#222** — one-card pull-to-refresh on Mobile, then revised twice on Bevel's review: it
  asks the PC for a snapshot rather than reloading, and the map gets a reserved chrome pull.
- **#228 mez swing** — a shorter reading now needs corroborating, a longer one does not.
  David chose that trade knowingly; it costs chain-mez artifacts one extra cycle to heal.
- **EQBuddy Mobile had NEVER used the desktop theme** — 563 logged failures nobody had read.
  `PaletteApplied` broadcasts derived tones and `CompanionTheme.Project` derived them again.
- **A refused port is no longer a dead end.** 47998 is unbindable on David's machine by ANY
  process (proved from bare PowerShell) while every table shows it free — a kernel
  reservation. The app now falls back to a port Windows will give and says so.
- **The four theme windows resize** and remember their size.

### The new voices

**Bevel** (`BEVEL.md` in, `BEVEL-FEEDBACK.md` out) is product/UX. Its first review was
excellent: it agreed with my conclusion on inline themes and threw away my reasoning
(*"consistency is a constraint, not the win. The win is the job."*), and it caught two real
misses in #222 that I had already pushed. **Read `BEVEL.md` before designing anything.**

**One open question is waiting on it or David**: #222 diverges from what bjstrange literally
asked for (parity with the native gesture, which is a reload). Written up in
`BEVEL-FEEDBACK.md`; either leave it or take the gesture over in both layouts.

**Scribe** (`SCRIBE.md` / `SCRIBE-FEEDBACK.md`) is community input and still excellent at it.
Its `Place:` guesses have now been wrong five times running — always labelled as
hypotheses, and its verbatim quotes are what actually find the bugs.

**David has asked Grok Bot for a graphics-designer bot** as well; the case for it is written
in `SCRIBE-FEEDBACK.md` (seven of 37 traps were found ONLY by looking at a picture).

### Also open

1. **The respawn-timer dig** (#228, joeymavity): *"respawn timers randomly re-open after
   they've been cleared."* `SpawnTimers.Clear` genuinely removes the entry, so something
   re-creates it. Not started.
2. **#227 has never been answered** — typical-usual-chaos asked for the Motes card, it is
   built, and nobody has told him. Post when the release goes out, with #226, #222, #228.
3. **`docs/proposals/InlineThemes.md`** — shape decided (tab strip, plus Bevel's split and
   host rules). Open questions 4 and 5 remain; nothing is built.
4. **`LogWatcher.FinishInitialIngest`** throws `ObjectDisposedException` on a timer in
   David's log — a shutdown race, low severity, not chased.
5. **`LanAddresses()` ranking on a Tailscale machine** — David's PC has Wi-Fi (10.0.0.84)
   and Tailscale (100.118.30.124). The QR advertises `BoundAddresses[0]` only. Worth
   confirming it picks the Wi-Fi address; a `100.x` QR is unreachable from a phone.
6. **`LogParser.cs` has 14 ratchet lines** and `OptionsWindow.xaml.cs` has 156 after today's
   lift. LogParser is the next file that will need one.

### FRESH PASS — David asked for this explicitly

Before taking new work, sweep for what has drifted rather than trusting this file:

- `pwsh -NoProfile -File scripts/status.ps1` — version, uncommitted work, hotspot headroom,
  open PRs and issues, and every discussion whose last comment is not ours.
- **Read the last comment's signature before replying to any thread.** Three of us now post
  as `DranakCorps-bot`: Scribe, Bevel and you. David also replies in his own words from that
  account — his 03:18 reply on #228 was a product statement I nearly missed.
- Check `BEVEL.md` and `SCRIBE.md` for items filed since this was written; take one, delete
  it, and write the feedback note.
- `docs/screenshots/` is current as of today. If a surface changed, re-shoot it —
  `scripts/shoot.ps1 -List` names them, and trap 21 (a shot name IS a filename) still bites.

### Standing

Post GitHub replies for finished work without asking, signed `— Dranak (Claude Code)`.
**Releases wait for David's explicit go at that moment.** Both UIs in the same change. David
is Windows-only; never hold a release to verify Avalonia. When a decision is his, use the
question tool — he has said twice that a question buried in prose is a question that does
not get answered.

---

## 2026-08-21 (earlier): the agreed plan (Kills & Drops + parity)

`main` pushed at `395c972`. `Directory.Build.props` says **1.99.0** with its What's-new
entry. Gates: **2,254 unit + 264 Avalonia + 18 E2E**, all green, E2E run three times to
prove the race below is gone.

**David's plan of 2026-08-20 was "Kills and Drops too, verify, and then ship everything
along with the Avalonia parity". Both halves are in.** What is left is the third step, and
it is his: **`pwsh -NoProfile -File scripts/release.ps1 -Tag v1.99.0`, on his explicit go
at that moment.** The 1.98.1 go does not carry forward.

### What landed

**1. The parity gap is closed.** Linux and macOS have the Inventory tab.
- The gear checklist came out of `EQBuddy.Avalonia/MainWindow.cs` into `GearCardView.cs`
  first, because that file had THREE ratchet lines left. Baseline lowered 5,127 → 5,422 in
  the same commit; `CopyCommandButton` moved to `DesignSystem` so a lifted surface does not
  have to copy it.
- `EQBuddy.Avalonia/InventoryView.cs` replaces `GearLockerWindow` + `InventoryWindow`, both
  deleted, both cog entries gone. It takes DELEGATES where the WPF twin takes `MainWindow`
  — deliberate: this build has no E2E, so a surface that can only be built from a live
  widget has no cover at all.
- `WidgetRenderTests` now demands EVERY tab in `LootSurface.Hosted` is offered, not merely
  that the offered ones open. The old assertion tolerated a missing tab on purpose, and
  that tolerance is what let the build ship a release behind.

**2. Kills & Drops, both builds at once.** `CreatureWindow` in each UI, `DropsCardView` in
each, `DropsWindow` deleted from both. The Kills card is the door; `Drops by creature…` is
off the cog. Card key stays `kills`, so no settings migration and nobody's card slot moved.
The mini star went into the window with the header (traps 20/26).

### Bugs that came out of it, all of which shipped fixed

- **Windows: the Inventory tab redrew once a second**, re-scanning the game folder and
  clearing the panel under the player's cursor — so a long inventory could not be read past
  its first screen. Both builds now paint it on arrival, on Refresh, and when a new dump
  lands.
- **Windows: filtering Drops to something that matched nothing kept the stale rows.** The
  signature hashed only rows and reset to `""`, so an empty result collided with the reset
  sentinel. Avalonia had already fixed it.
- **Both: the drop-row badges were click-handled EMOJI** — boxes under Wine, and #211's
  hit-testing hole waiting for anyone who converted them naively. `InlineIconButton` now.
- **E2E had a latent race**: a theme window opens at `ApplicationIdle` AFTER `Launch()`
  returns, so a row baseline read in that gap is `-1` and the later wait hangs on `-1 + 1`.
  Flaky one run in three; the loot test had carried it silently since its own fold.
- **Trap 37, and only the screenshot found it**: a lifted view's PINNED chrome stops being
  pinned. The Drops footer — the only in-app pointer to where the wiki pack went (#217) —
  ended up under thirteen creatures of rows.

**3. Motes is a card again, and Options can reach the mini dashboard** (David, 2026-08-21,
answering #228 and Scribe's item). Two asks with one root cause.

- The card comes back through `HiddenSections` plus the eye in Options — no new setting.
  `MigrateMotesCard` hides it ONCE for existing profiles; `MotesCardOffered` is what makes
  showing it stick. `Progress → Wealth` keeps its Motes block, and Motes came OUT of that
  card's "…are tabs in here now" note, because naming a card two rows above it in the same
  list sends someone into the window looking for something that is right there.
- **The defect underneath, which nobody had reported:** Options could not reach three
  mini-dashboard switches AT ALL. A stat's switch is the star on its card header, and the
  three folds moved five stars into windows; Options could only reach a star through the
  BREAKOUT box for its kind, which exists for six of the ten. Motes, coin and kills were
  switchable only by opening the very windows people were complaining about. Cards &
  windows lists all ten now, as the same setting. Trap 20 one level out.
- `OptionsWindow.xaml.cs` went past its ratchet writing that list, so the whole Cards &
  windows tab lifted into `OptionsCardsView.cs`: 1,670 → 1,546 against a baseline of 1,547
  that did NOT move.

**#228 HAS NOT BEEN ANSWERED, deliberately.** Scribe replied to it on 2026-08-20 from the
same bot account, and CLAUDE.md's rule is to read the last comment's signature before
replying — one account answering one person twice in two voices is the #215 mistake. Post
when the release actually ships, not before: the fix is real but unreleased, and "this is
fixed" about a build nobody can install is the kind of thing that costs trust. Same for
Scribe's standalone-Motes item, which can be deleted from `SCRIBE.md` then.

### Do first next time

1. **`OptionsWindow.xaml.cs` has 32 ratchet lines and `LogParser.cs` has 14.** Those are
   the two tight ones now; the Avalonia widget has 442 and the WPF one 273. The Options
   window is the next lift, and `MezDurationsView.cs` is the worked example of lifting out
   of it.
2. **`GearCardView`'s 320px `MaxHeight`** — a card-sized cap now living in an 880px window,
   on BOTH builds. It is the one remaining child scroller in the Gear & Loot window (trap
   36's neighbourhood); it gets away with it because the hard cap gives it real overflow.
3. **The Raids surface stores its auto-import outcome and never renders it** —
   `ImportReportView` exists and is wired only to Gear.
4. **Items** as a Gear & Loot tab — still named-but-unhosted, and it is where #174's
   approved features are meant to land.
5. **EQBuddy Mobile has no Kills & Drops surface.** The theme is desktop-and-phone by the
   surface rule, and the phone half was not built. `CompanionProjection` is the place; the
   badges and the launcher line already come from `CreatureTheme`, so the phone can read
   the same numbers rather than hand-rolling a fourth copy (#210's lesson).
6. **EQBuddy Mobile "Couldn't listen on port 47998"** (David, 2026-08-20). **Not
   reproducible.** Worth checking whether the companion listener sets `SO_REUSEADDR` — the
   error blames another program when the usual culprit is our own just-exited copy.

### David's direction — recorded in ROADMAP.md

**The gear button should BE Options**, not a menu containing Options: *"I would like the
gear to eventually be the path to options not click gear then click options from a list of
things."* Every remaining cog entry is named there with the theme that claims it. Two came
off it today. Travel route and zone maps are the World theme, which also takes Spawn timers
and the drop-camp marker.

### Waiting on reporters — do not chase, do not close

`#218`, `#221`, `#101`/`#193`. `#202` is answered; close it if bjstrange confirms.

**Scribe:** `SCRIBE.md` has items untouched — #225 (window position resets on update), #224
(voice for interrupted/resisted), #222 (mobile pull-refresh with one card, must-fix), #223
(waiting on David, do not ping), #208 (Wayland chip placement, must-fix), #153 (custom alert
volume — needs a fact, not another guess), and #228 corroborating the motes ask. Take one,
delete it, and note what helped in `SCRIBE-FEEDBACK.md`.

### Standing

Post GitHub replies for finished work without asking, signed `— Dranak (Claude Code)`.
**Releases wait for David's explicit go.** Both UIs in the same change — the two folds this
session are both cases where shipping one lane alone would have deleted a card on the other.
David is Windows-only; never hold a release to verify Avalonia. When a decision is his, use
the question tool.

---

## 2026-08-21 (earlier): 1.98.1 shipped, and the parity gap it left

`main` clean at `eef2f50`, tag `v1.98.1`. **7 assets, signature Valid,
`CN=FlossworksCross-Stitch`, timestamped**, OneDrive updated, installed locally. Gates:
**2,256 unit + 257 Avalonia + 17 E2E**.

**Nothing is half-finished, and one thing is deliberately incomplete** — read "the parity
gap" below before touching the Avalonia widget.

### What 1.98.1 shipped

- **`/outputfile` dumps import themselves.** The game announces every dump it writes, by
  name, in the log EQBuddy already tails (`Outputfile Complete: <file>`) and nothing was
  listening. `Core/OutputfileAutoImport.cs` owns it; the report line and its Undo are on
  the Gear tab. Both widgets.
- **Gear & Loot finished on Windows.** Three tabs — **Loot · Wishlist · Inventory**. "Gear"
  was renamed because it held a wishlist; Gear Locker and Inventory merged into one tab
  with a by-slot / by-bag pivot, because they read the same file. Window is 880px landscape.
- **The cog lost three entries** — Inventory, Gear Locker, and Gear & Loot itself. The
  widget card is the door, matching Quests and Progress.
- **EQBuddy Mobile** shows in-game commands as selectable text (a phone clipboard cannot
  reach the game on the PC — David's call).

### THE PARITY GAP — do this first

**Linux and macOS have everything from 1.98.1 EXCEPT the Inventory tab.** They keep the
separate Gear Locker and Inventory windows and their menu entries, so nothing regressed,
and the release notes say so in as many words. But:

**`LootSurface.Hosted` is SHARED Core vocabulary and already lists Inventory.** The Avalonia
widget builds its strip from it and looks bodies up in a dictionary, so before this was
caught its third chip rendered perfectly and threw `KeyNotFoundException` on click. It is
guarded now — that build offers only tabs it can draw, and
`WidgetRenderTests.EveryTabTheWindowOffersCanBeOpened` selects every offered tab and demands
a body. **The guard is not the fix.** The fix is the view.

1. **Lift the gear checklist out of `EQBuddy.Avalonia/MainWindow.cs` FIRST.** ~25 ratchet
   lines left; the lift frees ~275 (`BuildGearSection`, `RenderGearChecklist`, `GearRow`,
   the auto-check marks) — exactly what WPF lifted into `GearCardView`. **No E2E on that
   build**, so pin behaviour in `WidgetRenderTests` BEFORE moving anything; three pins
   already exist.
2. **Then the Inventory tab twin**: fold Avalonia's `GearLockerWindow` + `InventoryWindow`
   into one view with the two pivots. `EQBuddy/InventoryView.cs` is the worked example.
   Drop the two menu entries when the tab exists, as WPF did.

### THEN: Kills & Drops

**Step 1 is DONE, tested and deliberately not called yet** — `Core/CreatureSurface.cs`
(`09507d0`), the same way `LootSurface` landed before its window. David's grouping, and it
corrected mine: *"Kills isn't a meter though. we don't track kills per second but we track
damage per second, healing per second. Kills and Drops should be … Kills and Drops ;)"*
Both tabs are about the CREATURE — what died, and what it dropped at what rate.

Remaining: **lift `DropsWindow`'s body into a view — its body is in XAML, so this is a
XAML-to-code conversion, not the straight code move `GearLockerView` was** — then a
`CreaturesWindow` in both UIs, then the fold. **The fold switches on in Core and hits BOTH
widgets, so the Avalonia twin must exist BEFORE it is switched on**, or the Kills card
vanishes on Linux with nothing to replace it. That is the lesson the Gear & Loot fold paid
two days for, and the parity gap above is the same lesson arriving again.

Drops has already left `LootTab`.

### Traps this session paid for — read 34–36 before lifting anything

- **36 — a lifted view must build its own `Body` and NOT bring a `ScrollViewer`.** A child
  scroller is measured with infinite height, never scrolls, and still swallows the wheel.
  Cost David a working mouse wheel; no test or screenshot can see it.
- **A `DockPanel`'s fill child gets what the docked children leave**, and they take what
  they ask for — three `Dock.Right` buttons starved a status line into a 30px column and
  stretched the buttons into 380px slabs.
- **34 — a guard that forbids the wrong thing cannot see a missing thing.** Pair every
  "no X may do Y" with a curated list of "these must do Y". `GameCommandsTests` caught three
  real moves in one day.
- **The pattern under all of them:** every bug David found tonight photographs and diffs as
  correct. He found them by USING the window. Ship him a build.

### Smaller, outstanding

1. **`GearCardView`'s 320px `MaxHeight`** — a card-sized cap now in an 880px window.
2. **The Raids surface stores its auto-import outcome and never renders it** —
   `ImportReportView` exists and is wired only to Gear.
3. **Items** as a Gear & Loot tab — still named-but-unhosted.
4. **EQBuddy Mobile "Couldn't listen on port 47998"** (David, 2026-08-20). **Not
   reproducible**: one instance running, nothing listening on the port, and it is not in a
   Windows reserved range (`netsh int ipv4 show excludedportrange protocol=tcp`). Almost
   certainly `TIME_WAIT` from nine install-and-relaunch cycles that evening. **Worth
   checking whether the companion listener sets `SO_REUSEADDR`** — the error blames another
   program when the usual culprit is our own just-exited copy (trap 13's neighbourhood).

### David's direction — recorded in ROADMAP.md

**The gear button should BE Options**, not a menu containing Options: *"I would like the
gear to eventually be the path to options not click gear then click options from a list of
things."* Every remaining cog entry is named there with the theme that claims it. Kills is
not a meter (Live Meters = Combat + Healing). Travel route and zone maps are already the
World theme, which also takes Spawn timers and the drop-camp marker off the cog.

### Waiting on reporters — do not chase, do not close

`#218`, `#221`, `#101`/`#193`. `#202` is answered; close it if bjstrange confirms.

**Scribe:** `SCRIBE.md` has items untouched this session — #225 (window position resets on
update), #224 (voice for interrupted/resisted), #222 (mobile pull-refresh with one card,
must-fix), #223 (waiting on David, do not ping), #208 (Wayland chip placement, must-fix),
#153 (custom alert volume — needs a fact, not another guess). Take one, delete it, and note
what helped in `SCRIBE-FEEDBACK.md`.

### Standing

Post GitHub replies for finished work without asking, signed `— Dranak (Claude Code)`.
**Releases wait for David explicit go** — the 1.98.1 go does not carry forward. Both UIs
in the same change. David is Windows-only; never hold a release to verify Avalonia. When a
decision is his, use the question tool.

---

## 2026-08-20: Gear & Loot is DONE ON WINDOWS

**`main` pushed at `e382cd6`. `Directory.Build.props` says 1.98.1 with its What's-new
entry. NOT released — the family is still on 1.98.0 and David has not said ship.** He is
testing local installs (`scripts/install-local.ps1`, which now signs properly — see below).

### What the theme looks like now

Strip is **Loot · Wishlist · Inventory**, in an 880px landscape window.

- **`Gear` → `Wishlist`.** It held a wishlist and was labelled as though it held your gear:
  *"I guess I figured Gear would show me what gear I had."* Label only — the key is still
  `gear`, so no saved card position broke.
- **Gear Locker + Inventory → ONE `Inventory` tab with two pivots.** They read the same
  dump, so two tabs off one file made people wonder which was real. By slot (ranked, with
  ⬆/⬇ — the default, because it is the actionable question) or by bag. `InventoryByContainer`
  persists it. Both windows are deleted and **both cog entries are gone** at his request;
  `Gear & Loot…` keeps its own, so the room still has a door.
- **`/outputfile` dumps import themselves** — the game announces every dump it writes, by
  name, in the log we already tail, and nothing was listening. See the section below.

### Four bugs he found by USING it, none of which any gate could see

Worth reading as a set, because they are all the same shape — a behaviour that photographs
and diffs as correct:

1. **A `DockPanel`'s fill child gets what the docked children leave**, and they take what
   they ask for. Three `Dock.Right` buttons were ~440px of a 470px window, so the status
   wrapped ONE CHARACTER PER LINE and the buttons stretched into 380px slabs.
2. **A lifted view that brings its own `ScrollViewer` swallows the wheel** inside a host
   that already scrolls — trap 36, and the scrollbar looks perfectly correct in a picture.
3. **Two unrelated imports named side by side read as one sequence.** He copied the command,
   made the file, and followed the *other* sentence into Options.
4. **"Shopping list" was undefined jargon** with no in-app route: *"we have no idea what
   that is."*

### THE AGREED PLAN (David, 2026-08-20): Kills & Drops → Avalonia parity → SHIP

*"Let's do Kills and Drops too, verify, and then ship everything along with the Avalonia
parity."* One release carrying all of it. Do the steps in this order — each one is a
prerequisite for the next, not a preference.

**1. Kills & Drops — the small fold, do it first while it is cheap.**
`Core/CreatureSurface.cs` in the shape of `LootSurface`: two tabs, `Kills` and `Drops`,
keys `kills` and `drops`. Kills is already an `IWidgetCard`; `DropsWindow` needs its body
lifted the way `GearLockerWindow` was (that lift is the worked example, 2026-08-20 —
build its own `Body`, leave the window chrome behind, and **do not bring a `ScrollViewer`
with it**, trap 36). The Kills card becomes the launcher and keeps the slot; `Drops by
creature…` comes off the cog. **Both are about the CREATURE** — what died and what it
dropped — which is why this is a theme and not two cards.
→ Drops must also be **removed from `LootTab`**, where it is still named-but-unhosted. It
was never really Gear & Loot's: Drops is about the mob, not your bags.

**2. Avalonia parity — the debt, and the reason it is second.** That widget has the
toolbar fix and nothing else from today: no Inventory tab, no import report, no Wishlist
rename in its own surfaces. **`EQBuddy.Avalonia/MainWindow.cs` has ~25 ratchet lines**, so
the gear-checklist lift out of that file comes FIRST — ~275 contiguous lines
(`BuildGearSection`, `RenderGearChecklist`, `GearRow`, the auto-check marks), exactly what
WPF lifted into `GearCardView`. **No E2E on that build**, so pin the behaviour in
`WidgetRenderTests` BEFORE moving anything; `TheGearCardOffersTheByZonePivot` and
`TheGearCardHandsOverTheInventoryCommand` are two of those pins already.
Doing Kills & Drops first is deliberate: it adds nothing to that file, so it does not make
the ratchet worse while you are working toward it.

**3. Ship.** `pwsh -NoProfile -File scripts/release.ps1 -Tag v1.98.1` — but **only on
David's explicit go at that moment**; the go above is for the WORK, not the release, and
1.98.0's go did not carry forward either. Wait for the `Release assets` workflow before
telling anyone it shipped (it attaches the Linux/macOS builds a couple of minutes after
the tag, and `release.ps1` prints "published" before that finishes). The What's-new entry
for 1.98.1 already exists and covers the auto-import, the Wishlist rename, the landscape
window and the phone's selectable command — **add Kills & Drops and the Inventory merge
to it before releasing.**

### Also outstanding, smaller

1. **`GearCardView`'s 320px `MaxHeight`** — a card-sized cap now living in an 880px window
   (trap 36's second half). Flagged to David; he has not hit it.
2. **The Raids surface stores its auto-import outcome and never renders it.**
   `ImportReportView` exists and is wired only to Gear.
3. **Items** as a Gear & Loot tab — still named-but-unhosted.
4. Mobile: whether Inventory/Locker become phone screens is an open design question, not a
   bug. The phone's screen picker is not a widget card list.

---

## 2026-08-20: the command sweep is DONE and 1.98.1 is STAGED, NOT RELEASED

**`main` clean and pushed at `b8077a7`. `Directory.Build.props` says 1.98.1 and
`WhatsNew.json` has its entry — the release itself is waiting for David's go**, which he
has not given. The 1.98.0 go does not carry forward.

### What landed

David's ask, verbatim: *"the gear tab should give me the copy button for /outputfile
inventory … right now it's telling me to import it but not telling me how or giving me the
tool with which to do it. That needs to be applied for every instance of needing the user to
execute a command in game for an output file."*

Both halves of the Gear tab are fixed, on both desktops. The ⧉ copy is built **outside
`Render()` and outside the scroller**, so it belongs to the surface rather than to a state
of it — no branch can forget it, and it is on the populated tab as well as the empty one.
The empty state names both routes once each (the EQ Legends Tools export *and* `Options →
Cards & windows → Import gear list…`), from one string in `UI.Shared`, and the row that used
to repeat itself underneath is gone — E2E's `gearRows` pin moved 1 → 0 with it.

**The sweep found no other gap.** Gear Locker, Inventory, the Quests window, Raids, the
achievements menu and the map's `/loc` social all already had theirs, in both UIs. The gear
checklist was the only surface missing one, which is why the general fix matters more than
the fix.

### The general fix, and the two new traps

`GameCommandsTests` forbade a copy source from carrying its own literal — which says nothing
about a surface carrying **no copy source at all**. That is the hole this fell through, green
the whole time. `SurfacesNeedingACommand` is now a curated list, a reason per row, asserted
positively, written the way `DeadSettingTests.Known` is. **Verified by checking the two rows
for the broken surfaces fail on the pre-fix tree**, not merely that they pass on this one.
→ CLAUDE.md trap 34: *a guard that forbids the wrong thing cannot see a missing thing.*

**EQBuddy Mobile got selectable text, not a button — David's call, asked as its own
question.** A phone's clipboard cannot reach the game on the PC, so a ⧉ there is a silent
no-op wearing a working control's clothes. The command travels on the wire
(`CompanionCommandPrompt`) rather than being spelled in `index.html`; the two `/outputfile`
literals that were in the page are gone, and `GameCommandsTests` now forbids the page from
carrying one. Gear and Raids both, both states. → trap 35.

### Verified rather than assumed

Screenshots dark **and** Solarized with the contents predicted before the run (trap 23); a
new `gearloot-gear-empty` shot, because the empty state is the one David was looking at and
nothing photographed it; the **real shipped `index.html`** driven through
`mobile-harness.ps1` on a new `WriteGearSnapshot` fixture, populated and empty; binaries
grepped for the new strings before trusting any capture (trap 18). 2,236 unit + 256 Avalonia
+ 17 E2E.

### One number moved

**`EQBuddy.Avalonia/MainWindow.cs` spent 25 of its ~100 ratchet lines — 76 left**
(5,564 / 5,640). The gear-checklist lift is still the next move on that build and is now
more urgent, not less. Note it is also the surface that just changed, so the `WidgetRenderTests`
pin written for this change (`TheGearCardHandsOverTheInventoryCommand`) is part of the
before-the-move coverage CLAUDE.md asks for.

---

## 2026-08-20 (late): 1.98.0 IS LIVE AND SIGNED

`main` clean and pushed at the tag. **1.98.0 released, signed as
`CN=FlossworksCross-Stitch`, valid and timestamped**, on OneDrive and GitHub, installed
locally. All gates green: 2,220 unit + 255 Avalonia + 17 E2E.

**Nothing is half-finished.** Four things landed and every one is complete, released and
answered. Read the three short notes below and then pick from "What is actually next".

### 1. #202 is SOLVED, and it was never the phone

Three releases, two confident wrong diagnoses from here, and the answer was one missing
argument. **EQBuddy pushes to a paired device from two places** — `RefreshUi` once a second
and the 50 ms low-latency pump — **and they built their snapshots differently.**
`SessionStats.Snapshot()` passes no rules, and the rules block in `BuildSnapshotLocked` is
gated on `rules is not null`, so that overload returns a snapshot whose `Tracked` list is
EMPTY. The loot section is the only surface carrying the watch rows.

So the phone was told the watch list had emptied twenty times a second and refilled once a
second. **Its change detection was correct throughout — the data really was changing.**
That is why the 1.94.1 fingerprint fix could not help: it taught the page to ignore values
that drift on a CLOCK, and this one was not drifting, it was flipping.

Both widgets had it; Avalonia's pump was copied from WPF's. One builder each now
(`BuildSnapshot()` / `CurrentSnapshot()`), and `CompanionSnapshotArgumentTests` scans both
files' source so a third push site cannot pick the other overload. **I verified that guard
fails on the pre-fix tree**, not just that it passes on this one. Side effect: the memo is
keyed on the arguments, so agreeing made the fast path free instead of rebuilding
everything every 50 ms.

Replied on the thread. CLAUDE.md trap 33 — and its second half is the transferable part:
**the instrument found this, not the reasoning.** Two `?debug=1` screenshots from bjstrange,
nine seconds apart and exact mirror images, said in one line what three sessions of
hypothesis had not. Ship the diagnostic before the third theory.

### 2. Gear & Loot shipped on both widgets, in one commit

`docs/pending-gearloot-fold.patch` is applied and the file deleted. The Avalonia twin
(`GearLootWindow.cs` + `IGearLootHost`) was built first, so both builds folded together —
which is exactly what the two-day wait was for. Screenshots reviewed before commit, dark
and Solarized. David approved the visuals.

### 3. The MOVED badge (David asked for it directly)

*"please explicitly note, maybe in a different color, when things move from accessing one
way to another."* A `"MOVED: "` prefix on a `WhatsNew.json` highlight now renders as a badge
in `WarnBrush`/`WarnWashBrush` instead of a bullet. `Core/WhatsNewHighlight.cs` does the
split; both windows call it.

**The test asserts the COUNT of moves, not that some exist** (currently 3). That is
deliberate: the badge keeps its force only while it means one thing, so a release that
starts badging ordinary changes fails and has to be re-read. **When you add a move note,
update that number and say why in the commit.** Deliberately NOT tagged, as worked
examples: a new capability is not a relocation; a help affordance *about* moves is not one;
a control that was never visible was never somewhere else.

New shot: `whats-new`, seeded `LastSeenVersion = 1.96.1` so the popup renders two releases
and the badge is photographed BESIDE an ordinary bullet. A badge shot alone proves it
draws; beside a bullet it proves it reads as different.

### 4. Scribe — the capability question is ANSWERED, and CLAUDE.md was wrong

CLAUDE.md asserted Scribe "can run commands on that PC". David doubted it. **Scribe answered
in `SCRIBE-TESTING.md` within the hour and the truth is both:** its agent runs on a **Linux
VM with no checkout** and it will not clone one, but **David's Windows PC IS reachable
per-command**, each approved in the desktop app — `shoot.ps1 -List` ran there and returned
40 shot names.

So the Windows screenshot work was buildable all along, and **the shots never arrived
because of our instruction, not its behaviour**: `SCRIBE-TESTING.md` asked for output in
`dist/scribe-shots/`, and `dist/` is line 3 of `.gitignore`. It declined `docs/screenshots/`
because that is ours — correctly.

→ **Ask for findings as TEXT in `SCRIBE-TESTING.md`.** Every PC command costs David a click,
and an image cannot cross between us. Its channel work is excellent; its guesses about
source are still 4-for-4 wrong, so treat one as a place to look.

---

## What is actually next — pick one, they are independent

**0. ~~DAVID ASKED FOR THIS DIRECTLY~~ — DONE 2026-08-20, see the section at the top of this
file. Kept below because the reasoning is the durable part; nothing in it is a to-do.**

> *"the gear tab should give me the copy button for /outputfile inventory … right now it's
> telling me to import it but not telling me how or giving me the tool with which to do it.
> That needs to be applied for every instance of needing the user to execute a command in
> game for an output file."* — and, pointing at the pattern to copy: *"Raids has this
> implemented, for example."*

**The rule already exists and is already his** (`GameCommands`, David 2026-08-14: *"every
surface that names one offers a one-click ⧉ copy"*). What is new is that the rule is not
being ENFORCED where it matters, and the Gear tab is the proof.

**The worked example is `EQBuddy/RaidsCardView.cs`** — `CopyAchievementsCmd()`, which is
`Theming.WireCopyCommand(Theming.Button(""), GameCommands.OutputfileAchievements)` plus a
tooltip saying what to do with it, appended BOTH to the empty state and to the populated
one. Copy that shape; do not invent a second one.

**What is actually wrong on the Gear tab, and it is two things, not one:**

1. **No copy button.** `EQBuddy/GearCardView.cs` and the Avalonia gear section never
   reference `GameCommands` at all — the Gear Locker window next door does. The gear
   checklist auto-ticks from the inventory dump (`AutoCheckGearFromInventory`), which needs
   `/outputfile inventory`, and the surface never says so.
2. **The empty state names a task with no route.** It reads *"Import an EQ Legends Tools
   shopping-list HTML in Options."* — which is a DIFFERENT import from the `/outputfile`
   one, is a website export rather than a game command, and tells a player to go somewhere
   without saying where. **A copy button alone does not fix that sentence.** It needs to say
   both: where the shopping list comes from, and that `/outputfile inventory` is what makes
   the ticks happen by themselves.

**And the general fix, which is the half that stops this recurring.** `GameCommandsTests`
today only forbids a copy source carrying its own literal — it cannot notice a surface that
ASKS for an output file and offers nothing. That is exactly the hole the Gear tab fell
through. Add the positive assertion: a curated list of surfaces that require an in-game
command, each asserted to reference `GameCommands` and to wire a copy, with a reason per
entry the way `DeadSettingTests.Known` does. A list is code that cannot be type-checked, so
it has to be written down and reviewed — but it turns "we remembered" into "the build
remembered".

**Sweep every instance**, per his wording, in both UIs in the same change: gear checklist,
Sky/achievements import, inventory, gear locker, the map's `/loc` social. Check the
POPULATED states too, not just the empty ones — Raids puts the button in both, because the
player who needs it most is the one whose import went stale.

**One thing to ASK rather than assume:** EQBuddy Mobile. The phone's clipboard cannot paste
into the game on the PC, so a copy button there is a dead end — but showing the command as
selectable text is not. Use the question tool.

**1. The Avalonia widget has ~100 lines of ratchet room, and this is the real constraint.**
`EQBuddy.Avalonia/MainWindow.cs` is 5,539 against a 5,127 baseline (limit 5,640). Two folds
in a row COST it ~90 lines each rather than freeing any, because a fold moves surfaces and
leaves the doors. **The next theme on that build must be preceded by a lift, not followed by
one.** The candidate is named in the ratchet comment: the gear checklist, ~275 contiguous
lines (`BuildGearSection`, `RenderGearChecklist`, `GearRow`, the auto-check high-water
marks) — precisely what WPF already lifted into `GearCardView.cs`. **Caveat that makes it
harder here: no E2E suite on this build**, so CLAUDE.md's "pin the behaviour before the
move" has to be paid in `WidgetRenderTests`. Write the assertions first.

Two other files are tight and worth a glance: `OptionsWindow.xaml.cs` (32 left) and
`LogParser.cs` (25 left).

**2. Gear & Loot's second pass: Drops and Items as tabs 3 and 4.** `LootSurface` already
names all four; `Hosted` lists the two that are real. `DropsWindow` and `ItemInfoWindow`
exist, so this is a fold of windows rather than of cards — a different shape from the first
pass, and worth doing one at a time for the same reason.

**3. EQBuddy Mobile's Gear & Loot parity is an open DESIGN question, not a bug.** The phone
keeps `loot` and `gear` as two separately-selectable screens, and that is probably RIGHT —
a phone screen picker is not a widget card list, and folding two screens into one tabbed
screen would make the phone worse. CLAUDE.md says the phone is first-class in both
directions, not that it must copy the desktop's shape. **But nothing has decided this on
purpose yet**, and the labels ("Loot & watches", "Gear checklist") do not come from
`LootSurface.LabelFor`. Worth asking David rather than guessing.

**4. `ZoneShare.Export` re-exports imported-but-unverified timers** — an open question for
David, recorded in `docs/SpawnEvidence.md`. Unchanged.

**5. The spawn evidence store (Gate 2)** — designed in `docs/SpawnEvidence.md`, not built.

**6. Small things a repo sweep turned up on 2026-08-20 that are in nobody's inbox.** None is
a player-facing bug; all four are the kind of thing that only a deliberate scan finds. In
descending order of worth:

- **The build has 22 warnings and nothing gates them, so a real signal cannot be seen.**
  Two are the compiler telling us about dead state: `MainWindow._gearChecklistDirty` (WPF) is
  set in **eight** places and read in **none** — the Gear & Loot fold left the write path and
  took the reader, exactly the trap-20 shape, and it is harmless only because
  `GearLootWindow.MaybeRefresh` repaints the gear tab every second unconditionally. The
  Avalonia twin still reads its flag, so the two builds do different amounts of work for the
  same result. `_dmgOutSortDps` and `_healSortHps` (Avalonia) are never used at all — free
  lines on the file with 76 left. **`DeadSettingTests` covers settings; nothing covers
  fields, and the compiler already knows.**
- **A stale git worktree**, `.claude/worktrees/compassionate-euler-6085c2`, on branch
  `claude/compassionate-euler-6085c2` at `2b7c241` from 2026-08-07 ("Linux updater points at
  the tarball asset"). **Unmerged, but its content is superseded** — `UpdateOffer.cs` and
  `UpdateChecker.LinuxTarballName` are on `main` and have since grown a macOS fix the old
  commit never had. Nothing is lost by removing the worktree and the branch; left in place
  because deleting someone's branch is their call.
- **`ROADMAP.md`'s theme table said Gear & Loot was "next"** after it shipped in 1.98.0.
  Corrected in this commit, with the second pass (Drops, Items) named so the row is not
  wrong again the moment those land. CLAUDE.md says keep that table true because it is the
  one doc a non-engineer reads.
- **Three `IconPaths` geometries are drawn and never used**: `Hourglass`, `Scales`, `Tray`.
  `Hourglass` is deliberate — `1325b29`, *"One mark per meaning: slow stops wearing the
  respawn hourglass (David)"* — and is arguably the natural home for the slow-chip
  counter-type icon Frank asks for in `SCRIBE.md` (#94). The other two have no such story.
  Trap 29 says an unused `IconPaths` entry is worth a look for the same reason a
  written-never-read setting is.
- Clean results worth recording so nobody re-runs them: **no TODO/FIXME/HACK anywhere in
  `src`**, no leftover `.patch` files, no untracked stragglers, and **no trap-29 repeats** —
  every control declared `Visibility="Collapsed"` in XAML is un-hidden by something. One doc
  link has rotted: `docs/ImplementationPlan.md` cites `Core/SessionJournal.cs`, which does
  not exist.

### Waiting on reporters — do not chase, do not close

`#218`, `#221`, `#101`/`#193`. `#202` is answered and can be closed if bjstrange confirms.

### Standing

Post GitHub replies for finished work without asking, signed `— Dranak (Claude Code)`.
**Releases wait for David's explicit go** — he gave it for 1.98.0 and that go does not carry
forward. Both UIs in the same change. David is Windows-only; never hold a release to verify
Avalonia. When a decision is his to make, use the question tool rather than burying it in
prose.


---

## 2026-08-20 (night): why the Gear & Loot fold waited two days — SUPERSEDED, kept for the reason

**SUPERSEDED 2026-08-20 late: the fold shipped in 1.98.0, and `docs/pending-gearloot-fold.patch`
is applied and deleted. Nothing below is a to-do.** It is kept because the REASON the fold
waited is the durable part, and the next theme will meet it again.

The window was built, committed and live on `main` (`05de9e1`). **The fold was not**, and the
reason mattered more than the code: I built it, the Avalonia test suite failed, and the
failure was right.

**`MigrateLootSections` lives in Core, so it folds BOTH widgets — and only WPF has the
window.** Switching it on removes `gear` from `SectionOrder` for everyone, and the
Linux/macOS widget still renders its own Gear card from its own `SectionMap`. Result: that
card silently disappears on Linux with nothing to replace it. Renaming the shared
`OverlaySections.Catalog` entry did the same damage one level up — `WidgetRenderTests.
TheGearCardOffersTheByZonePivot` went to an empty collection immediately.

That is "both UIs in the same change" (CLAUDE.md) and "one theme per release, WITH its
mobile parity" (docs/Themes.md) being enforced by the tests rather than by me remembering.

**The whole fold is saved as `docs/pending-gearloot-fold.patch`** — 513 lines, applies to
`05de9e1`. It contains, all working and all verified before the revert:

- the widget's Loot card as a LAUNCHER (`SectionLink` button, `LootHeader` carrying
  `LootTheme.LauncherSummary`), and the Gear card removed from the XAML
- the `loot` star rehomed into `GearLootWindow` — it is the ONLY writer `MiniStats` has for
  "loot" and it also gates the Loot breakout, so losing it was trap 20 twice over
- `OverlaySections.Catalog`: `("loot", "Gear & Loot")`, `gear` removed
- `AbsorbedTitles["loot"] = ["Gear"]` **plus a real grammar fix** — the note read
  *"Gear are tabs in here now"*, because every previous fold absorbed several cards and
  this is the first to absorb one. `AbsorbedNote` now picks the verb.
- `MigrateLootSections()` switched on in `AppSettings.Load`
- the gear E2E facts moved into `GearLootWindow.DebugFacts()` and both tests repointed at
  it via `EQBUDDY_GEARLOOT=gear`, plus a new pin on the launcher line's length
- `ApplySectionLayout` crash found by E2E: a key in `OverlaySections.Catalog` with no entry
  in `MainWindow.SectionMap` throws on STARTUP for everybody. Worth hardening on its own.

**The order to land it in:** build the Avalonia `GearLootWindow` twin first, fold that
widget in the same commit, then apply this patch. Not the other way round.

---

## 2026-08-20 (evening): 1.97.0 SHIPPED, and the Gear & Loot theme is under way

**1.97.0 is live and signed** — 7 assets, `Get-AuthenticodeSignature` = `Valid`, issuer
`Microsoft ID Verified CS EOC CA 03`, timestamped, published hash matches the local
artifact, OneDrive updated, both workflows green. Shipped as a MINOR (David's call): the
entry had been staged as 1.96.2 for one fix and ended up carrying 14 highlights including
a whole new platform for EQBuddy Mobile.

⚠️ **One thing to remember about releasing:** immediately after publish the release had
only 4 assets — no Linux or macOS builds — on the release whose headline is Linux/macOS
support. Those come from the `Release assets` WORKFLOW, which starts when the tag lands and
takes a couple of minutes; `release.ps1` prints "published" before it finishes. **Wait for
that workflow and re-check the asset list before telling anyone it shipped.**

### Loot & Items: three steps done, the window is next

Chosen over Alerts by re-measuring (see ROADMAP.md, which now records why). Landed:

- **Step 1 + 5 — `Core/LootSurface.cs`** (`a3c7e57`). All four tabs named so the keys are
  settled once; only Loot and Gear `Hosted`, because a tab with nothing behind it reads as
  broken rather than not-yet-arrived. The fold is written and TESTED and deliberately
  **not called yet** — see below.
- **Step 3 — `UI.Shared/LootTheme.cs`** (`bd4d555`). Badges and the launcher line.
- **Gear on the two-host seam** (`bd4d555`). `GearCardView` builds its own body and
  implements `IWidgetCard`; the widget hosts it through a bare `ContentControl` and asks
  for instances via `MainWindow.NewGearCard()`.

⚠️ **`MigrateLootSections()` IS WRITTEN AND MUST NOT BE CALLED until the window ships.**
Wiring it into `AppSettings.Load` was the obvious next line and I did it, then checked what
it would do to a player *tomorrow*: cards missing from `SectionOrder` are appended rather
than hidden, so nothing would be lost — but every player who positioned their Gear card
would find it at the bottom of the widget, in exchange for a window that does not exist.
The comment in `AppSettings` says so at the call site. **The fold goes in with the window.**

### What is left in this theme

Step 4 (the window with its tab strip, both UIs), step 6 (mobile), step 7 (the absorbed
note + the tour), then wire the fold, pin E2E on the window, lower the ratchet, shoot it.

`ProgressWindow.xaml.cs` is the template to copy — `EqSegmentedStrip` for the tabs,
`NewProgressSurfaces()` for the per-host card instances, `DebugFacts()` for the E2E channel.
**Watch the tab strip: it must WRAP**, not sit in a horizontal `StackPanel` (trap 25 — the
Progress theme lost its fourth chip off the window that way).

⚠️ **`OptionsWindow.xaml.cs` has 32 lines of ratchet headroom.** It took the mez editor and
the breakout rewrite this week. Anything else landing there needs a lift, not a bump.

---

## 2026-08-20 (later): spawn-tracking quality pass — 3 fixes in, evidence store designed

David brought a ChatGPT-authored upgrade plan for spawn tracking and asked for a review
before implementing. The audit is in `104b1e2`'s message and in
[docs/SpawnEvidence.md](docs/SpawnEvidence.md); **read that file before touching spawn
learning**, it carries the decisions and the reasoning behind them.

**Landed (`104b1e2`), each proven by watching its tests fail against the old code first:**

1. **126 catalog named could never learn a timer.** `LearnFromRekill` returns before it
   starts when there is no current duration — and 126 shipped named have a blank respawn
   in a zone with no default (all 38 in High Keep, Princess Lenia among them; 47 in
   Western Wastes). Being listed without a number was strictly worse than being absent,
   since a DISCOVERED named measures its cycle on the second kill. They now do too.
2. **The same-stay rule**, scoped to the no-known-duration path only. **I had this wrong
   at first and said so:** a cross-stay gap is a TRUE upper bound, and where a duration
   exists `gap < d` already keeps it harmless. It only bites where the first accepted gap
   BECOMES the countdown. A test pins the deliberate non-application to the other path.
3. **An import is someone else's number and now says so.** Both an import and a re-kill
   set `Learned` and nothing else was recorded, so the Spawns window's tooltip literally
   read "your kills or an import" — it could not do better. Marked now, cleared by your
   own kills, and an import can no longer carry a stale `Sighted` flag.

**Decided with David, for the next gate (all in docs/SpawnEvidence.md):**

- The real prize is that **learning is a one-way ratchet with no path back** — 3 `Trusted`
  entries in 1,414 means 99.7% of the catalog has no recovery from one bad sample. The
  evidence store is what makes the minimum recomputable.
- **Not standard deviation** — the data is one-sided and SD is most sensitive to exactly
  the outlier it would be used to find. Robust spread instead: count within a tolerance of
  the tightest observation.
- **DUE stays at the earliest observed, with the typical shown beside it** (David's call).
- **Sightings are the clean measurement** — no reaction-time term — and are the only way to
  tell a variable spawn from an inattentive camper. Currently applied and discarded.
- **Camp-specific learning: no**, concluded from the code — `/loc` is too rare.

⚠️ **One open question for David** (in the doc's last section): `ZoneShare.Export` re-exports
timers you IMPORTED and never verified, which makes community data self-reinforcing without
new evidence. One-line change either way; it is a community call, not a technical one.

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
it churns on a freshly opened page, the cause is something else and this fix is still worth
having. **Replied and asked him exactly that** — the footer on his device, and whether
fully closing and reopening the page stops it. Do not close #202 on this; it is a
diagnosis waiting on one answer.
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
