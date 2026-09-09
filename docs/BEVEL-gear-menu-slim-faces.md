# Bevel faces — expanded gear menu slim (DRA-25) + Guide sidebar rename

**Owner LOCK ~7:01 PM CT (this file's Part A):** expanded shell gear must become
**Options… · World… · Mobile… · Guide…** ONLY — the same four rows the minimized bar
already shows (`UI.Shared/WidgetMenuPolicy.MiniRows`, shipped in #451). Leaving the gear
menu: **Session history…, Click-through, Edit HUD, Data & imports, Help.** Edit HUD stays
the title-bar pencil. Helm SIGN required before Soft Opus implements.

**Owner CORRECTED ~7:16 PM CT, from a live screenshot (this file's Part B):** window
chrome stays **`EQBuddy — Guide`** — do not touch it. The Evolved shell's **sidebar rail**
label, which currently also reads **"Guide,"** renames to **"Quest."** The mini/expanded
menu door **`Guide…`** is unaffected either way. A prior instruction to rename the window
title to "Quests" is **superseded** — do not implement that.

**Soft shots (this session, `20260908-owner-qa`):**
`C:\Users\david\source\EQBuddy\.claude\soft-captures\20260908-owner-qa\expanded-gear-menu-screen.png`,
`expanded-gear-full-menu.png`, `options-cards-live-desktop.png`.

**Scope:** faces/docs only. No `src/` edit in this pass, no WhatsNew, Play Console OFF.
Soft Opus implements after Helm SIGN, in its own PR.

---

## Part A — Expanded gear menu: keep 4, cut 5, and where the 5 go

### A0. What the shots show, read against `MainWindow.xaml`

`expanded-gear-full-menu.png` is the expanded widget's gear (⚙) menu today, live. It
matches the XAML exactly (`src/EQBuddy/MainWindow.xaml:36-122`), nine top-level rows:

1. Options…
2. World…
3. Mobile…
4. Guide…
5. **Session history…** (`Tag="expanded"`, line 83, `Click="OnHistory"`)
6. **Click-through (game clicks pass through)** (`Tag="expanded"`, line 85-87, checkable)
7. **Edit HUD** (`Tag="expanded"`, line 100, `Click="OnEditHud"`)
8. **Data & imports** (`Tag="expanded"`, line 103, six-item submenu)
9. **Help** (`Tag="expanded"`, line 115, four-item submenu)

Rows 1–4 are `WidgetMenuPolicy.MiniRows` — already the mini-bar's whole menu (#451,
Helm-signed). Rows 5–9 are the ones the owner lock cuts. This is a tighter cut than
`docs/BEVEL-cog-options-ia-faces.md` §E shipped: that pass kept Click-through and Edit HUD
on the **expanded** menu deliberately ("mid-pull toggle; rare on mini") — **this lock
supersedes that call.** Say so out loud rather than silently reversing it: the owner has
now decided expanded and minimized menus should match exactly, full stop, and the two
rows that pass exempted are the two this pass names first.

`options-cards-live-desktop.png` is the shot to hold the "no longer here" language
against — it's the existing Options → Cards & windows tab's **"No longer on the widget"**
section, which already does exactly the "X is now Y" naming this doc needs to add a menu
equivalent of. Nothing on this screen changes in Part A.

### A1. Row 6, Edit HUD — straight CUT, no new destination

**Already built and shipped** (`docs/BEVEL-cog-options-ia-faces.md` §C, Helm-signed): the
pencil button (`MainWindow.xaml:236-239`, `x:Name="EditHudBtn"`, `Click="OnEditHud"`) is a
≤1-click title-bar affordance for the exact same mode the menu row opens. The menu row at
line 100 is a live duplicate today, not a fallback — cut it outright. Nothing to design;
nothing to point players toward, because the better door already exists and is on screen
the whole time the widget is expanded.

### A2. Row 5, Click-through — moves to Options → Behavior

**Place:** `src/EQBuddy/SettingsBehaviorView.cs` (the Behavior tab's content builder) —
add beside the other window-operating toggles already there (hide-when-unfocused /
hide-when-not-running / hide-on-alt-tab, keep-above-overlays). `AppSettings`'s existing
click-through field is unaffected; only the control's *host* moves.

**The tension, named rather than ignored:** the row's own 1.67.1 doc comment
(`MainWindow.xaml:19-23`) argues click-through "stays, because flipping it mid-pull is
what it's FOR" — the same reasoning that just earned Edit HUD its pencil. I read this and
still recommend Options, for a cost reason specific to this control rather than a
disagreement with the reasoning: the title bar is already nine columns wide at 320px
(status dot, char label, perf label, feedback, mobile, pencil, gear, reset, minimize,
close) and has no drawn glyph for "cursor passes through" in `IconPaths.cs` today — a
matching new title-bar toggle is a new vector plus more crowding on the one surface that's
on screen every second, for a control the reporting so far describes as "flip once you've
placed the widget," not "flip every pull" the way Edit HUD's chip-arranging is. **If a
report says two clicks (Options… → Behavior) is actually too slow for how players use
this,** that's a real follow-up — a second title-bar toggle, sized and drawn properly —
but it is not something to invent speculatively in a slim pass. Put it at the TOP of
Behavior's list, above the hide-policies, so it's still the fastest thing to find there.

### A3. Row 8, Data & imports — splits by what the import feeds, not kept as one block

Reading each of the six sub-items against **"an import belongs on the surface its output
lives on"** (trap 43; already the reasoning `BEVEL.md`'s I-11 pass used to move the gear
checklist import out of Options and into the Gear room):

| Sub-item | Destination | Why |
|---|---|---|
| Wiki contribution pack… | **Options → Behavior**, new "Data" group | Output is eqlwiki edits across creatures/items broadly — no single room owns it |
| Review an archived log… | **Options → Behavior**, same "Data" group | Feeds Drops-by-Creature and the wiki pack against *that* session — same family as the pack, not session history |
| Choose log folder… | **Options → Behavior**, same "Data" group | Core app config, same shelf as Mobile pairing / hotkeys / regen override / auto-empty+archive already there |
| Auto-detect log folder | **Options → Behavior**, same "Data" group | Same as above |
| Import achievements… | **Guide room** (`QuestsView.xaml`/`.cs`) | Pre-marks Sky quest rewards and raid clears — the checklist it fills in lives in Guide, not Options |
| Copy /outputfile achievements | **Guide room**, beside Import achievements… | It exists only to feed the import above; splitting the two across two surfaces would strand the command a step away from what reads it |

**Place for the Guide pair:** `src/EQBuddy/QuestsView.xaml(.cs)` — the shared view both
`QuestsWindow` and the shell's Guide room render (`CLAUDE.md`'s own routing table: "Both
build their own instance"). Put both new actions near the checklist's own header so they
land on whichever host is open, without a second implementation.

**Place for the "Data" group:** `src/EQBuddy/SettingsBehaviorView.cs`, as one labeled
cluster (e.g. "Data") rather than four loose rows dropped among unrelated Behavior
controls — it's a real sub-shelf, the same shape `AlertSurface.AlertTab` already gives the
Alerts tab's four families.

### A4. Row 9, Help — one duplicate cut, three moved to a new Options footer

**Send feedback…** is a straight cut, same shape as Edit HUD: the title-bar envelope
button (`MainWindow.xaml:188-196`, `Click="OnFeedback"`) already opens it, ≤1 click,
always visible while expanded. The menu row is a dead second door to the same place.

**EQBuddy (open website), Quick tutorial…, Check for updates** don't belong to any one
Options tab — they're not Look, Alerts, Watch, HUD, or Behavior content, they're "about
the app," and parking them at the bottom of Behavior specifically would be arbitrary (why
there and not Look?). Recommend a small **persistent footer strip on `OptionsWindow`
itself**, below the tab content, visible regardless of which tab is selected — the same
shape most OS Settings apps and VS Code use for this exact class of link, which
`ShellPages.cs`'s own doc comment already cites as this app's precedent for splitting
"configures the tool" from everything else.

**Place:** `src/EQBuddy/OptionsWindow.xaml` — the window is a three-row `Grid` today
(Row 0 header + close, Row 1 tab strip, Row 2 `ScrollViewer` with the tab panels, ending
at line 144). Add a **Row 3** below the `ScrollViewer`: EQBuddy's website link, "Quick
tutorial…," "Check for updates," in one thin horizontal strip. Existing handlers
(`OnOpenWebsite`, `OnTutorial`, `OnCheckUpdates`) move with the controls; nothing about
what they DO changes.

### A5. Row 7, Session history… — the one with a real trap in it

**Place:** `src/EQBuddy/MainWindow.xaml.cs:3215-3226` (`_historyWindow`, `OnHistory`,
`new HistoryWindow(_repo, _settings)`).

**The destination:** the career half of this exact door was already built and Helm-signed
on 2026-09-05 — `src/EQBuddy.UI.Shared/HistoryPresentation.cs`'s own doc comments
(:358-383) name it: Progress room's **History tab** shows the browsable list of stored
sittings, cross-session ladders, from the same rows `HistoryWindow`'s studio uses. The
deeper jobs (compare, notes/tags, export, delete, import, the per-session graph and pull
list) stay in the studio (`HistoryWindow` itself) because they need
`SessionRepository.LoadSnapshot` and a second live connection to it is trap 13's shape
with a database instead of a settings file — that reasoning is untouched by this pass and
should not be re-litigated.

**What breaks if this cut ships without a fix, and why it's not optional:**
`HistoryPresentation.StudioPointer` (`HistoryPresentation.cs:455-458`) is the sentence
printed on the Progress → History tab today, and it says, verbatim: *"right-click the
EQBuddy widget and choose 'Session history…'."* That sentence is the studio's ONLY named
door once this cut ships — the context-menu row it names will no longer exist. Left
unedited, this is trap 20's exact shape (a sentence describing a control the player can no
longer find) landing in the SAME PR that removes the control it describes, which is a
lower bar to clear than trap 20 usually is — this one is foreseeable at design time, not
discovered later. **This is a MUST-FIX pairing, not a follow-up**, no different from
`RetiredCardsTests` refusing a "no longer on the widget" row that names a menu path that
isn't real.

**The fix:** replace the context-menu instruction with a real in-room control on the
Progress → History tab — a button/link under the career list (e.g. "Open the full History
studio…") that opens `HistoryWindow` the same way the cut row did
(`new HistoryWindow(_repo, _settings)`), and rewrite `StudioPointer`'s text to name that
button instead of a right-click path that's gone. This is the same shape as A3's import
moves: the door relocates onto the surface whose data it deepens, rather than surviving as
prose pointing at a menu that no longer has the row.

**Getting there from the widget:** Guide… → shell opens on the Guide room → click
Progress in the rail → History tab → the new studio button. Three clicks from the widget,
inside the ≤3-click budget the mini-menu's own faces doc already measured everything else
against.

---

## Part B — Guide sidebar rename: rail says "Quest," title stays "Guide," menu door untouched

### B0. The one function backing two facts that must now say different things

`ShellPages.Label(ShellPage page)` (`src/EQBuddy.UI.Shared/ShellPages.cs:214-224`)
currently returns `"Guide"` for `ShellPage.Quests`, and it is read by **six** call sites,
not one:

| Call site | What it draws | New answer |
|---|---|---|
| `ShellWindow.xaml.cs:360` (`Title = $"EQBuddy — {ShellPages.Label(_page)}"`) | The window's title bar | **Stays "Guide"** — owner's correction, verbatim |
| `RailRow.cs:49` (`_label = DesignSystem.Text(Role.Body, ShellPages.Label(page))`) | The rail row's own text | **Becomes "Quest"** |
| `RailRow.cs:39` (`ToolTip = $"{ShellPages.Label(page)} — {ShellPages.Describe(page)}"`) | The rail row's tooltip | **Becomes "Quest"** — the tooltip should read the same as the row it's attached to |
| `HomeReadout.cs:210` (Home room's deep-link cards) | The "jump to a room" card on Home | **Becomes "Quest"** — it's a nav affordance, same category as the rail, not the title bar |
| `ShellWindow.xaml.cs:538` (Ctrl+K palette's per-room entries) | `Index()`'s room-level palette rows | **Becomes "Quest"** — same reasoning as Home's deep links |
| `ShellWindow.xaml.cs:545` (palette's per-sub-tab entries, `"{Label} · {label}"`) | e.g. today's `"Guide · Sky Quest"` | **Becomes `"Quest · Sky Quest"`** |

**The fix is one new function, not six hand-edits.** Add
`ShellPages.RailLabel(ShellPage page)` that returns `"Quest"` for `ShellPage.Quests` and
falls through to `Label(page)` for every other page (the five other rooms don't diverge,
so there is no reason to duplicate their spellings a second time — that would be the same
"two hand-maintained lists" shape trap 55 already named for `AbsorbedTitles`). Point the
five nav call sites above at `RailLabel`; leave `ShellWindow.xaml.cs:360`'s `Title` line
reading `Label` exactly as it does today.

### B1. What does NOT change

- **`WidgetMenuPolicy.GuideRow = "Guide…"`** (`WidgetMenuPolicy.cs:56`) — the mini/expanded
  menu door's own label. The owner's correction is explicit that this stays "Guide…."
- **`WidgetMenuPolicy.GuideAddress => ShellPages.Key(ShellPage.Quests)`** — the wire
  address (`"quests"`). Unaffected either way; `Key()` is a third, separate function this
  rename does not touch, and it must not — `page:room` addresses are persisted
  (`ShellPages.cs`'s own rule, cited in `WidgetMenuPolicy.cs:58-62`).
- **`ShellPages.IconName(ShellPage.Quests) == "Quest"`** — already spelled "Quest" as the
  icon key; no relation to the label text, nothing to do here.
- The enum member `ShellPage.Quests` itself, and every `EQBUDDY_SHELL` / `EQBUDDY_EXPAND`
  dump fact keyed off it.

### B2. Tests that will need the new fact, named so Opus doesn't have to rediscover them

- `tests/EQBuddy.Tests/WidgetMenuTests.cs:125` — `Assert.Equal("Guide",
  ShellPages.Label(ShellPage.Quests))`. **Stays passing, unchanged** — this is exactly the
  fact B0 keeps pinned to `Label`. Do not "fix" this assertion to say "Quest"; that would
  be re-breaking the thing the owner just corrected.
- `tests/EQBuddy.Tests/ShellTerminologyTests.cs:141`
  (`EveryRailLabelAndRoomDescriptionIsPlayerVocabulary`) — today only walks `Label` and
  `Describe` per page. Extend it to also walk `RailLabel` per page, so "Quest" gets the
  same player-vocabulary hygiene check `Label`'s six other spellings already get.
- A new, small assertion belongs beside the two above: `RailLabel(ShellPage.Quests) ==
  "Quest"` and `RailLabel(<anything else>) == Label(<anything else>)`, the same
  round-trip shape `ShellNavigationTests` already uses for `Rooms`/`TabForKey`.

---

## Not this pass

- No `src/` edit — this document is the face; Soft Opus implements after Helm SIGN.
- No WhatsNew entry drafted here. Both parts are player-visible moves and each earns an
  "X is now Y" line in the release that ships them (the sentences above are written so
  Opus can lift them close to verbatim), but this pass is docs-only.
- No re-opening `docs/BEVEL-cog-options-ia-faces.md` §E's other calls (World stays a
  breakout, Guide is the one shell door, no `EQBuddy window…` recovery label) — only the
  two rows this lock names (Click-through, Session history) and the three more it adds
  (Edit HUD, Data & imports, Help) are in scope.
- No touching `CompanionSurfaces.cs` — grepped; the phone's screen picker does not read
  `ShellPages.Label` today, so Part B has no mobile-side counterpart to keep in sync.

— Bevel
