# Fable inbox

Plans for Claude, not a work order. **Claude: take a `ready` item, then delete it**
(or leave only what is still planned).

EQBuddy is the incubation lab. We refine the finished state here. The organization
iterates the same way as the software (observe → diagnose → change → verify).

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

## Item shape

- **Priority:** `ready` (plan written; Claude may take it) · `needs-david: <the decision>`
  (names ONE consequence-list decision; waits for his answer, never for a generic "approve") ·
  `someday`. David may still write `approved` as an explicit mark; it means `ready`.
- **Class:** `V2` or `V3` (if you cannot say why it is not V0–V1, it does not go here)
- **Source:** discussion/issue, Bevel/Scribe item, or David's words
- **Plan:** architecture, risks, decomposition, verification, what is out of scope
- **Bevel pre-design: yes / no, because…** — required on any plan with a presentation PR.
  Fable plans the architecture; Bevel judges whether the player can still do the job. The
  executor treated a plan as the design pass once (2026-08-22) and should not have had to guess.
- **Shot offline: yes / no** — for any staged screenshot. `shoot.ps1` is NOT offline by
  default, so a "not read yet" prediction for an unseeded wiki page is wrong before it runs.
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.
- **Decided without asking:** the implementation calls the plan made that could have gone the
  other way, one line each — these go to `DECISIONS.md` when the item is taken.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`. Fable last-looks the
executed diff (H4) and answers in the same file; a defect found there is a V1 item for the
next loop, not a reopening of the plan.

---

## Inline themes — expand in place, pop out on request

- **Priority:** `ready` — David answered the one question that was his (2026-08-22, asked
  with the question tool): **build it as Bevel ruled it — expand in place, pop out on
  request; the widget stays the home; the theme windows stay for the second monitor.**
  Plan by Fable 5, 2026-08-22. One theme per PR, both UIs in each.
- **Class:** `V2` — four themes × two UIs, a host-ownership rule a compiler cannot see (a
  body has one visible parent), a retired breakout, and it reverses the *direction* of four
  folds that were themselves signed decisions. Not V1 because the host rule has to be right
  for all four before the first ships, and a wrong one is trap 15 on every card at once.
- **Source:** David, 2026-08-21 (*"expandable sub-categories under them with an option to
  pop out the window"*); #228 (daetien-lab: *"I simply want to track my mote drops in the
  main window"*; joeymavity: *"Motes are buried and seem to move around"*);
  `docs/proposals/InlineThemes.md`; Bevel's ruling 2026-08-21 (tab strip; split rule; host
  rule; pop-out collapses the card; collapsed by default; name pills by the old card titles;
  default tab is the room that moves while you play).
- **Bevel pre-design: YES, before PR 1's screenshots** — specifically the expanded card's
  height per theme at 100 % and 125 % scale, and the one-line bodies of the Glance tabs below.
  The shape is already ruled; this pass is about what it looks like on the thing that sits
  over the game.
- **Shot offline: no** for Kills & Drops (the Drops tab reads the wiki — seed every fixture
  creature's mob cache as `wiki-pack` does); **yes** for the other three.

### What I read, and what it changes

1. **The launchers are four `SectionLink` buttons whose KEYS are already the theme keys**
   (`kills`, `loot`, `quests`, `progress` — `MainWindow.xaml:457/512/534/603`, `SectionMap()`
   at `MainWindow.xaml.cs:649`; Avalonia `_sections[...]` at `MainWindow.cs:999–1060`). The
   cards keep their keys, so **there is no settings migration in this item** — no
   `HiddenSections`/`SectionOrder` fold, nothing for `AbsorbedTitles` to change. That is the
   single biggest reason this is tractable.
2. **The two UIs own theme bodies DIFFERENTLY, and the plan has to say so or the Avalonia
   lane ships a crash.** WPF: each window builds its own instances (`NewProgressSurfaces()`,
   `NewGearCard()`, `new LootCardView`, `new DropsCardView`, `QuestsWindow` its own) — two
   hosts, two instances, no conflict. Avalonia: the widget BUILDS the bodies once
   (`_progressTabBodies`, `_lootTabBodies`, `_creatureTabBodies`) and the window takes them
   through `IProgressHost.ProgressTabBody(tab)` and sets `_body.Content = …`
   (`ProgressWindow.cs:268`). A control has one visual parent; showing a body in the card
   and the window at once throws. The one-owner rule below is therefore not a UX nicety on
   Avalonia, it is the thing that keeps the app up.
3. **`SectionScroll.MaxHeight` already caps the whole card stack** (`WidgetMetrics.
   SectionMaxHeight`, E2E-pinned as `sectionMaxH`), so an expanded theme cannot run the
   widget off the screen — it scrolls inside the cap like any tall card. Per-theme height
   is a Bevel question, not an engineering one.
4. **`BreakoutKind.Progress` exists on WPF only** (`BreakoutWindow.xaml.cs:14`; Avalonia's
   enum has no `Progress`), opened from the `xp` star (`MainWindow.xaml.cs:3616`). Bevel's
   ruling folds it into the pop-out. `scripts/shoot.ps1`'s `mini-bar` shot enumerates
   `BreakoutKind` BY HAND (trap 30) and `OptionsCardsView.BuildBreakouts` enumerates it by
   reflection — both need a look when the member goes.
5. **The phone is the prototype and needs nothing.** `index.html`'s `.qtabs` card-with-tabs
   reads the same Core tab keys (`index.html:1158–1180`). Parity is already by shared module;
   this item brings the desktop TO the phone's shape, not the reverse.
6. **E2E already pins the launcher LINE** (`EndToEndTests.cs:91–97, 196–224, 519–522`:
   "the launcher should summarise the theme"). Keep the summary line verbatim as the
   collapsed header and those assertions keep passing; they become the regression guard
   that the glance survived the expander.
7. **`QuestChecklistView` is one control hosting Epic and Sky** (`MainWindow.xaml.cs` ctor,
   `_quests`); how `QuestsWindow` hosts it versus the General search is not re-read this
   session — **hypothesis:** the General tab is a search box plus a detail pane with its own
   `DetailScroll`, which is exactly Bevel's "do not shrink-wrap a full window" case.

### Architecture

**Core (recipe step 1 — name it where the phone and both desktops can read it).**
Each theme's surface definition (`ProgressSurface`, `LootSurface`, `CreatureSurface`,
`QuestSurface`) gains `InlineMode InlineModeFor(tab)` → `Full | Glance`. Initial table:

| Theme | Full inline | Glance inline (one line + ⧉ into the window) |
|---|---|---|
| Progress | Experience · Wealth · Faction · Raids | — |
| Kills & Drops | Kills · Drops | — |
| Gear & Loot | Loot · Wishlist | Inventory (a long list with its own filter bar) |
| Quests | Epic 1.0 · Plane of Sky | General (search + detail pane; Bevel's host rule) |

Bevel's pre-design pass may move a tab between columns; the table lives in Core so the
move is one line and both desktops follow. Default tab per theme, per Bevel: the room that
moves while you play — Experience, Kills, Loot, and for Quests whichever of Epic/Sky the
player's class has rows in (else Epic).

**UI.Shared — `ThemeHost` (framework-free, unit-tested; the "sum, not pixel" rule).**
A state machine per theme: `Collapsed | Inline | Window`, with inputs `ToggleCard`,
`PopOut`, `WindowClosed`, `OpenWindow(tab?)` (the ⚙/hotkey/`EQBUDDY_*` openers), and a
`SelectedTab` kept for the session only. Invariants it enforces, and the tests assert:
- **One owner.** `PopOut` from `Inline` → `Window` and the card collapses. `ToggleCard` while
  in `Window` does NOT draw a second copy: it brings the window forward (Avalonia cannot
  show the body twice; WPF could, and must not, for the trap-15 reason). `WindowClosed` →
  `Collapsed`, never back to `Inline` — the player closed a thing; do not re-grow the widget.
- **The tab follows the player.** Pop-out opens the window on the card's selected tab;
  closing the window hands the window's tab back to the card for the next expand.
- **A Glance tab never paints a body.** Its inline content is the tab's one-line summary
  and the ⧉; `Render` is not called on the full view.
Both windows already expose `OpenX(tab)`; the state machine tells them when.

**WPF — `ThemeCardView` (one class, four instances; `EQBuddy/ThemeCardView.cs`).**
Replaces each `<Button Style=SectionLink>` with `<Expander x:Name="…Section"
Style="Section">` + `<ContentControl x:Name="…Body"/>`, the `MotesSection` shape, so
`SectionMap()` is unchanged. Header: the existing `EqCardTitle` + the existing summary
`TextBlock` (trap 12: the summary stays the star column and keeps trimming) + a
`DesignSystem.InlineIconButton("External", …)` ⧉ that sets `e.Handled = true` — the
Progress fold's lesson: a button nested in the expander header bubbles to the toggle.
Body: `EqSegmentedStrip` fed by the theme's existing `Tabs(...)` (so badges and labels are
the window's, and the strip WRAPS — trap 25) over a `ContentControl` holding the selected
tab's body. Bodies: **the card builds its own instances** exactly as the windows do
(`NewProgressSurfaces()`, `NewGearCard()`, `new LootCardView`, `new DropsCardView`,
`InventoryView`), built lazily on first expand; the `IWidgetCard` rule (a collapsed card
renders nothing) keeps a never-opened theme at zero cost. `ThemeCardView` takes
`ICardContext` + the factories, never `MainWindow` — the seam exists for this.

**Avalonia — `ThemeCardPanel` (`EQBuddy.Avalonia/ThemeCardPanel.cs`, beside
`SectionLinkPanel`).** A `SectionPanel` expander whose content is the strip + a body host.
Bodies are the widget's single instances; the hand-off is explicit: the host that loses
ownership sets its `Content = null` BEFORE the other sets it, in one method on the main
window (`HandThemeBodyTo(theme, Host.Card|Host.Window)`) that `ThemeHost`'s transitions
call. No other code path may assign a theme body — grep for `TabBody(` assignments and
route every one through it, or the second place is where the crash lives.

**Progress breakout.** `BreakoutKind.Progress` retires into the pop-out: the `xp` star while
minimized opens the Progress WINDOW on Experience (Bevel's "fold it into the theme's
pop-out"). `DisabledBreakouts` entries for `"Progress"` are ignored, not migrated — nothing
is lost, the window has its own position memory. Trap 30: remove `Progress` from the
`mini-bar` list in `shoot.ps1` in the same commit. Trap 20/26: list every control the
breakout carried and say where each went (its xp line → Experience tab, already there).

**Tour and Options (recipe step 7).** The tutorial page "Cards that open windows" is now
wrong; rewrite it to "Cards that expand — and can pop out". `OverlaySections.AbsorbedNote`
stays as is (the absorbed titles are still inside). Options → Cards & windows row tooltips
for the four keys say "expands in place; ⧉ opens its window".

**Mobile:** no change. `CompanionProjection` already emits the Core tab keys; if the
`InlineMode` table is added to Core it is NOT put on the wire (the phone has no windows).

### Risks and the traps they touch

- **Trap 15 (two switches for one state):** `ThemeHost` is the only switch. Neither the
  expander's `IsExpanded` nor the window's `IsVisible` may be consulted as truth; both are
  outputs. Assert it: a test that fires `WindowClosed` twice and `ToggleCard` during
  `Window` and checks no state goes to `Inline`.
- **Avalonia one-parent:** covered above; the `HandThemeBodyTo` funnel is the guard, and a
  `WidgetRenderTests` case must expand a theme, pop it out, close the window and expand it
  again without an exception — the sequence that throws if any assignment bypasses the funnel.
- **Trap 12 (timer-driven size on X11):** a body that grows on a CLOCK inside an expanded
  card changes measured size every tick. The Kills list grows per kill (player-driven, fine);
  Experience's xp line ticks every second — it was a card before the fold and is the same
  control, so this is existing behaviour, not new. Do not add anything clock-driven to the
  header.
- **Trap 14/25:** the tab strip wraps; the header is a two-column grid. Both already true in
  the windows; copy, do not hand-build (`CLAUDE.md`: never hand-build another pill).
- **Trap 16:** the ⧉ is an `InlineIconButton`, hit area `IconInlineHit`.
- **Trap 36/37:** the lifted bodies have no scrollers and some had pinned chrome in their
  windows (Quests' search box, Drops' orientation footer). Full-mode tabs get the body only;
  anything that was pinned window chrome is why a tab is Glance.
- **Trap 21:** `docs/screenshots/` already has `section-progress.png`, `progress-card.png`,
  `widget-expanded.png` embedded in docs. The new shots are `theme-inline-progress`,
  `theme-inline-kills`, `theme-inline-loot`, `theme-inline-quests`; `widget-expanded`
  (`EQBUDDY_EXPAND=1`) will now photograph four expanded themes — **predict it and re-shoot
  it deliberately**, it is in the README.
- **Trap 22/23:** `EQBUDDY_EXPAND=progress` must open the card INLINE (not the window) after
  this change; the E2E opener for the window stays `EQBUDDY_PROGRESS`. Write the prediction
  for each shot before running it.
- **Ratchet:** both MainWindows are near their caps (WPF 4,418 / 4,635; Avalonia 5,591 /
  5,964). The four launcher builds and their openers MOVE into `ThemeCardView` /
  `ThemeCardPanel`; each PR lowers the baseline in `ArchitectureTests` and
  `docs/Architecture.md`'s table in the same commit (`DocumentationTests` checks the table).
- **Trap 1:** the ⧉ and the strip sit under the UI-scale transform; nothing here does
  screen-pixel arithmetic, but shoot at 125 % once.
- **E2E launcher-line assertions** keep passing only if the collapsed header carries the
  exact summary the launcher did. If a PR changes that line, it is changing the glance, and
  #219 says a changed glance is a lost feature until proven otherwise.

### Decomposition (one theme per PR; both UIs in every PR; each leaves `main` shippable)

- **PR 0 — Core + UI.Shared, no UI.** `InlineMode` on the four surfaces with the table above;
  `ThemeHost` with `ThemeHostTests` covering every transition and both invariants; a
  `SurfaceParityTests` case that the phone's tab keys still equal Core's. `docs/TestPlan.md`
  §3 rows.
- **PR 1 — Progress, both UIs.** `ThemeCardView` / `ThemeCardPanel` born here. The breakout
  retirement and the `mini-bar` list. E2E: pin BEFORE the move — `progressInline=0/1`,
  `progressTab`, `progressTabs`, `progressWindowOpen` into `EQBUDDY_EXPAND`; assert expand →
  pop-out → close → card collapsed. `WidgetRenderTests` twin. Tutorial page. Baselines
  lowered. `WhatsNew.json` entry crediting daetien-lab and joeymavity (#228) and David's ask.
  **Bevel's pre-design pass lands between PR 0 and PR 1's screenshots.**
- **PR 2 — Kills & Drops and Gear & Loot.** Same class, two more instances; Inventory is
  the first Glance tab, so this PR proves the Glance body. Drops inline fetches the wiki —
  seed the fixture caches.
- **PR 3 — Quests.** The Glance General tab and the `QuestChecklistView` hosting question
  (item 7 above — verify before building; if the checklist cannot be hosted twice on WPF,
  build a second instance as the windows do). `EQBUDDY_EXPAND=quests` inline shot.
- **Each PR:** `FABLE-FEEDBACK.md` note; `DECISIONS.md` lines for anything the executor
  decided against this plan; Fable last-looks the diff before the release that carries it.

### Verification

- Unit: `ThemeHostTests` (every transition; one-owner; Glance never renders), `InlineMode`
  table test, `SurfaceParityTests`; Avalonia `WidgetRenderTests` expand/pop/close/expand.
- E2E (WPF has no unit tests): the facts above, asserted from `tests/EQBuddy.E2E`, written
  against the launcher BEFORE PR 1 changes it.
- Screenshots, predicted first: each `theme-inline-*` at 100 % and 125 % and once in
  Solarized; `widget-expanded` re-shot and the README checked. Prove the binary (trap 18).
- **The one check David can do himself, and it is the #228 job:** expand Progress on the
  widget over the game, read motes and xp without opening anything, pop it out, close the
  window, and see the card stay collapsed. Then the same with Kills & Drops mid-fight.
- Reporter confirmation on #228 after release — tell daetien-lab and joeymavity exactly
  what to click, and that Motes is also still its own card (#227) if they prefer that.

### Out of scope

The phone (it is the prototype); the World, Alerts and Live Meters themes (not built yet —
they will be born as expand-in-place cards when they land, which is the point of `ThemeHost`
being shared); retiring any theme window; shipping any theme expanded by default; the Motes
card (#227, separate); keyboard reach for the card stack (none of the cards have it);
per-theme height caps beyond `SectionScroll` (Bevel may ask for one; it is a follow-up).

### Already shipped (must not be fought)

The four theme windows and their tab strips (`EqSegmentedStrip` everywhere — never
hand-build another); every theme body on the `IWidgetCard` seam; `SectionScroll.MaxHeight`
and its E2E pin; `BreakoutKind` gating via `DisabledBreakouts` for the kinds that stay;
`AbsorbedTitles`/`AbsorbedNote` (#219); the Motes card's return (#227); the launcher summary
lines and the E2E assertions on them; the phone's card-with-tabs.

### Checked

Read this session: `docs/proposals/InlineThemes.md` in full; Bevel's ruling; `docs/Themes.md`
in full; `MainWindow.xaml:450–625` and `SectionMap()`; `MainWindow.xaml.cs` card seam,
`NewProgressSurfaces`, `NewGearCard`, the window openers; Avalonia `MainWindow.cs:990–1075`,
`AppTheme.cs` `SectionCard`/`SectionLinkPanel`, `IProgressHost.ProgressTabBody` and
`ProgressWindow.cs:255–275`; all four theme windows' strip/body wiring in both UIs;
`BreakoutKind` in both; `WidgetMetrics.SectionMaxHeight`; `EndToEndTests` launcher
assertions; `index.html` `.qtabs`; `shoot.ps1` shot table and `docs/screenshots/`;
`ArchitectureTests` baselines and current line counts. **Hypotheses, labelled:** item 7
(how `QuestsWindow` hosts `QuestChecklistView` and the General search) — verify in PR 3.

### Decided without asking (already in `DECISIONS.md`)

Ships collapsed, all four; one owner with "expand while the window is open brings the
window forward"; closing the window never re-expands the card; selected tab is
session-only; Progress breakout retires into the pop-out (Bevel's call, ratified); Glance
for Quests/General and Gear & Loot/Inventory; Progress goes first.

---

*No other items.*
