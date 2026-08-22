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
- **Column budgets: <the fixed widths this touches>** — for any plan that puts a new string
  into an existing surface. The Sky glance overflowed a fixed 150 px column and was found from
  a screenshot after it was built; measure before writing the string.
- **Guards run eight times** — a new test that guards a fix is not green until it has passed
  eight consecutive runs. `SettingsClobberTests` was flaky one run in three from the hour it
  shipped and would have passed any single review.
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.
- **Decided without asking:** the implementation calls the plan made that could have gone the
  other way, one line each — these go to `DECISIONS.md` when the item is taken.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`. Fable last-looks the
executed diff (H4) and answers in the same file; a defect found there is a V1 item for the
next loop, not a reopening of the plan.

---

## Avalonia theme bodies need a seam before a card and a window can both host them

- **Priority:** `ready` — no consequence-list decision in it. This is the blocker that stopped
  Inline themes PR 1 finishing, and it is written the day it was hit rather than carried.
  **Plan written by Fable 5, 2026-08-22 — see "Plan" below the stub. Short form: option (a),
  because (b) is an open upstream Avalonia bug and not ours to make safe.**
- **Class:** `V2`. Not V0–V1 by your own test: **no answer from David finishes it as V1.** The
  obvious fix (build a second instance for the card, as WPF does) is wrong for a reason only
  visible with the whole widget in view — the Avalonia surfaces are not objects, they are
  render code writing into ~40 MainWindow fields, so a "second instance" would be a second
  panel containing THE SAME field controls and would fail identically one level down.
- **Source:** Inline themes PR 1, executor, 2026-08-22. WPF half is on `main` (`a1157f2`).

### What happened, precisely

`WidgetRenderTests.ProgressCardFoldsTheAaLedgerBehindAToggle` fails with
**`System.ArgumentException: Attempt to call InvalidateArrange on wrong LayoutManager`**,
thrown from the test's own `Dispatcher.UIThread.RunJobs()`. 38 pass before the change, 37 pass
and 1 fails after. It is the crash your plan predicted in as many words — *"a control has one
visual parent; showing a body in the card and the window at once throws"* — arriving through a
route the `HandThemeBodyTo` funnel does not close.

The sequence is ordinary, not exotic: `WidgetRenderTests` sets `EQBUDDY_EXPAND=1`, the widget
expands every section (`MainWindow.cs:472`), the theme card therefore goes `Inline` and takes
`_progressTabBodies[Experience]`, and the test then opens the Progress window. **A player does
the same thing by expanding Progress and clicking ⧉.**

### What was tried, so the next attempt does not repeat it

Each of these was built and run, not reasoned about:

1. **The funnel itself** — card releases in `ThemeCardPanel.Render`, window releases through
   `IProgressHost.ProgressTabBody`. Correct and insufficient.
2. **`Presenter?.UpdateChild()` on release** — `ContentControl` detaches its old child on the
   next LAYOUT pass, not on assignment. Still failed.
3. **Posting the repaint at `Background` priority** (what the WPF twin does on `Expanded`).
   Still failed.
4. **Deferring `Sync()` off the pop-out path.** Still failed.
5. **Forcing a layout flush on release** — `ILayoutRoot` and `ILayoutRoot.LayoutManager` are
   `internal` in the Avalonia version we ship, so this route does not exist from our code.
6. **Isolation, to stop guessing:** reverting ONLY the `SectionLink` → `Section` swap makes the
   test pass with every other change in place. So the trigger is the card being expandable at
   all, not the strip, not the ⧉, and not the body composition — a run with the card given
   private throwaway bodies still failed.

### The shape of the fix, as far as the executor got

The WPF lane does not have this problem because its surfaces sit on `IWidgetCard` — "paint
yourself from this snapshot" — so each host builds its own instance and nothing is ever moved.
`ProgressWindow.xaml.cs` says so itself. The Avalonia lane never got that seam; `BuildProgressSection()`
and friends compose shared fields, and `RefreshExpandedSections` paints those fields directly.

So the question for the plan is which of these, and it is genuinely not the executor's call:

- **(a) Put the Avalonia Progress surfaces on the `IWidgetCard` seam** (a real refactor of a
  5,593-line file, and the one that makes the two lanes the same shape — and would pay for
  PR 2 and PR 3 as well as this one).
- **(b) Keep one instance and make the MOVE safe** with a public Avalonia API that actually
  exists in our version.
- **(c) Something else — e.g. the card hosts a projection rather than the surface.**

### Also in scope of whatever you decide

**MainWindow (WPF) GREW 4,424 → 4,504 in PR 1**, where the plan says each theme PR lowers the
baseline. Nothing moved out, because the Progress surfaces were already out. Headroom is now
**131 lines against ~80 a theme**, so PR 2 and PR 3 do not fit. The lift the plan assumed has
to be named: the most obvious candidate is the `EQBUDDY_EXPAND` dump block (~130 lines of pure
string-building that is a SUM, not a pixel, and therefore belongs in a file of its own).

### Already shipped (must not be fought)

The WPF inline card (`ThemeCardView`, `ProgressThemeCard`), `ThemeHost` and `InlineMode` from
PR 0, `WidgetMetrics.ThemeBodyMaxHeight`, `EQBUDDY_EXPAND=progress:raids`, and three committed
screenshots. **The Avalonia half was reverted rather than left half-built**, so `main` has one
UI with inline themes and one without — deliberate and reported, not drift.

### Checked

Read this session: `MainWindow.cs:472` (the expand loop), `:479` (the generic ExpandedChanged
subscription that is registered AFTER it, which is why the widget's own handler never saw the
initial expand and mine did), `BuildWealthSection`/`BuildProgressSection`, `RefreshExpandedSections`,
`ProgressWindow.Refresh`, `IProgressHost`, `SectionPanel`, and the Avalonia `DesignSystem`
strip/icon helpers.

### Plan — Fable 5, 2026-08-22

**Decision: (a). Put the Avalonia theme surfaces on the `IWidgetCard` seam, one theme at a
time, starting with Progress — and adopt the rule that on Avalonia a control NEVER moves
between two windows.** (b) is off the table for a reason you could not have seen from our
code, and (c) is (a) wearing a smaller hat.

#### What I read, and what it changes

1. **The exception is an open upstream bug, not a gap in the funnel.** Avalonia issues
   **#12753** (2023, "cross-window control reparenting should be supported" — kekekeks, still
   open), **#17906** (2025: a regression in 11.2.0, moving a UserControl between windows throws
   this exact message; fine in 11.1.5) and **#21267** (2026, Avalonia 12.0.x, same message in
   production). We ship 12.1.1. The mechanism in their source: `GetLayoutRoot()` and
   `GetLayoutManager()` both read `Visual.PresentationSource`, the field 11.2 introduced, and
   `LayoutManager.InvalidateArrange` throws when the control's presentation source is not the
   manager's owner. Your six attempts were all variations on making a cross-window move safe;
   **there is no sequence of public calls that does, and the people who own the layout engine
   have not found one in three years.** That is why #5 found the API internal: it is internal
   because re-parenting across roots is not a supported operation.
2. **The existing design is already on that path and working by accident.** `ShowProgressWindow`
   creates a NEW `ProgressWindow` each time the old one closes (`MainWindow.cs:3584`), and
   `IProgressHost.ProgressTabBody` hands the SAME `_progressTabBodies[...]` controls to the new
   window — a cross-window move on every reopen, today, on `main`, before Inline themes touched
   anything. No test closes and reopens the window, so nothing has ever checked it.
   **Hypothesis, labelled:** it survives because a closed window's presentation source is
   cleared on close, so the check passes by null; the inline card is the first time both roots
   are alive at once. Step 0 below tests the reopen path directly.
3. **Your own file already contains the right pattern twice.** `IProgressHost.ProgressMiniStars()`
   builds NEW star buttons per window and registers them; `BuildMotesSection(summary, list)`
   takes its controls as parameters "because two hosts draw motes now and a Control has one
   parent". The seam is that pattern applied to the whole theme instead of two corners of it.
4. **Your finding #6 ("throwaway bodies still failed") is the one thing this plan does not
   explain, and it is why Step 0 exists.** If a run with no shared body still threw, something
   else crossed a root — the reopen move above is the leading candidate (the test opens the
   window once, but `EQBUDDY_EXPAND=1` with an expandable card may have forced a second
   `Refresh`), and the stack trace will say in one line what we are both guessing at.
5. **Ratchet.** Avalonia `MainWindow.cs` is 5,593 against a 5,422 baseline (cap 5,964; 371 of
   headroom). The Progress rooms are ~17 fields, `BuildProgressSection` (~60 lines),
   `BuildWealthSection`/`BuildMoneySection`/`BuildMotesSection`, the paint block at ~2,300–2,380,
   `RenderRaids` (3,180–3,259, ~80 lines) and `ProgressMiniStars`. Lifting them is 250–350 lines
   out, which is the headroom PR 2 and PR 3 need on THIS lane and the lift the original plan
   assumed. On WPF the lift is the `EQBUDDY_EXPAND` dump block, as you proposed — see the
   amendment on the Inline themes item.

#### Architecture

**The seam (`EQBuddy.Avalonia/IWidgetCard.cs`, mirroring `EQBuddy/IWidgetCard.cs` name for
name):** `IWidgetCard { string Key; Control Body; void Render(StatsSnapshot) }`, `ICardContext`
with the same six members, and `ProgressSurfaceSet(Experience, Money, Motes, Faction, Raids)`.
`MainWindow.NewProgressSurfaces()` builds a fresh set on every call. **Nothing hands out a
`Control` it built earlier** — `IProgressHost.ProgressTabBody(tab)` is deleted, and its
replacement is `IProgressHost.NewProgressSurfaces()`.

**Four view classes** (`ProgressCardView`, `MoneyCardView` + `MotesCardView` composed as the
Wealth tab exactly as `ProgressWindow.xaml.cs:64–71` composes them on WPF, `FactionCardView`,
`RaidsCardView`), each owning the fields it paints today. `RaidsCardView` takes the ledger
accessor and `CopyAchievementsCmd` factory; `ProgressCardView` takes what WPF's does
(`settings`, the class source, the level accessor). The widget's `RefreshExpandedSections` loses
its Progress/Wealth/Faction branches and `RenderRaids` goes with its view.

**`ProgressWindow` builds its own set in its constructor and renders it on its tick** — reopen
is a new window with a new set; nothing moves. The mini stars stay as they are (already
per-window).

**`ThemeCardPanel`** (the twin of WPF's `ThemeCardView`, in its own file) builds ITS own set on
first expand through the same factory, and never releases anything because it never shares
anything. `HandThemeBodyTo` is deleted — a funnel for a move that must not happen is a place for
the next person to make it happen.

**The rule, as a trap (copy-ready for `CLAUDE.md`, number it when it lands):**

> **A control NEVER moves between two windows on Avalonia.** Re-parenting across `TopLevel`s
> throws `Attempt to call InvalidateArrange on wrong LayoutManager` — an open upstream bug since
> 11.2 (#12753, #17906, #21267), still present in 12.1.1, with no public API that makes it safe.
> The widget's theme bodies were handed to the Progress window by reference, which worked only
> because no test ever reopened the window; the inline card, the first host alive at the same
> time as the window, threw on the first run. Six attempts to sequence the hand-off failed
> because the operation is unsupported, not mis-sequenced.
> → **Every host builds its own instance through a factory** (`NewProgressSurfaces()` on both
> lanes), and no host interface returns a `Control` it did not just create. Guarded by
> `SurfaceOwnershipTests` (no `*TabBody(` accessor on any Avalonia host interface) and by the
> reopen/pop-out sequence test in `WidgetRenderTests`.

#### Risks and the traps they touch

- **Trap 20/26 (a fold loses a writer):** moving 17 fields out of `MainWindow` is exactly the
  event that drops a setting's last writer. List every control each view takes and every
  `_settings.X =` in the code that moves; `DeadSettingTests` will not see a writer that moved
  into a view that is never constructed — the card builds lazily, so the WINDOW must construct
  its set eagerly for those writers to exist.
- **Trap 15:** `ThemeCardPanel`'s body host gets no `IsVisible` of its own; `SectionPanel`
  owns expansion, `ThemeHost` owns placement.
- **Trap 36:** `ThemeCardPanel` carries the body cap WITH a scroller and the wheel pass-through
  (`PointerWheelChanged`, the Avalonia shape of `PassWheelUpWhenItCannotScroll`).
- **The mini stars** stay window-only; the card shows none (as WPF). `_stars` removal on close
  is already right.
- **`ProgressTabShowing` and the paint gates** (`RenderRaids` returns unless the tab shows) move
  into `RaidsCardView.Render` as "render only when hosted" — or the host calls `Render` only for
  the visible tab, which is what WPF's `ThemeCardView` does. Pick the latter; it is one rule.
- **Two instances of `MotesCardView`** when the widget's own Motes card is shown AND Progress is
  open — the precedent is already in the file (`_cardMotesSummary` vs `_motesSummary`), so this
  is the existing behaviour made explicit.
- **Ratchet:** lower the Avalonia baseline in the same commit as the lift.

#### Decomposition

- **Step 0 — diagnostic, time-boxed to one hour, BEFORE any refactor.** (i) Re-apply the
  reverted attempt from your local reflog or stash if you still have it; if not, skip to (ii).
  Attach `AppDomain.CurrentDomain.FirstChanceException` in the failing test and print the
  `ArgumentException`'s stack — which control, which caller (`ContentPresenter.UpdateChild`,
  `SetVisualParent`, a `Refresh`). (ii) **Write the reopen test on `main` as it is:** open the
  Progress window, close it, reopen it, `RunJobs()`; then the same with a tab change in between.
  Record in `FABLE-FEEDBACK.md` whether `main` throws TODAY. Either answer changes nothing about
  the plan — it tells us whether this was a latent crash players could already reach.
- **PR A — the seam, Progress only, no inline card.** `IWidgetCard`/`ICardContext`/
  `ProgressSurfaceSet` on Avalonia; the four views; `NewProgressSurfaces()`; `ProgressWindow`
  builds its own; `ProgressTabBody` deleted; the widget's Progress paint branches deleted;
  `SurfaceOwnershipTests`; the reopen test from Step 0 now green by construction; Avalonia
  baseline lowered. **Every existing `WidgetRenderTests` case for Progress must pass
  unchanged** — that is the "tabs draw what the cards drew" claim carried across the seam.
  Trap entry into `CLAUDE.md`. No player-visible change; no What's-new.
- **PR B — Inline themes PR 1, Avalonia half**, now a port of the WPF card: `ThemeCardPanel`
  mirroring `ThemeCardView` line for line where Avalonia allows; `EQBUDDY_EXPAND=progress:raids`
  honoured; the expand → pop-out → close → expand sequence test; `WidgetSheetTests` shot with
  the prediction written first. Lands with the What's-new line the WPF half already has, as one
  entry for both lanes.
- **PR 2 and PR 3 of Inline themes each begin with their lane's lift** — Loot/Gear/Inventory
  bodies (`_lootTabBodies`) and Kills/Drops (`_creatureTabBodies`) onto the seam first, card
  second. The plan's "each PR lowers the baseline" is true again because the lift is real on
  the lane that needs it.

#### Verification

- `SurfaceOwnershipTests` (source scan: no Avalonia host interface method returns `Control`
  from a field); `ThemeHostTests` unchanged; the reopen sequence and the card/window sequence
  in `WidgetRenderTests`, each with `RunJobs()` between steps — **and run the new guards eight
  times before calling them green** (your flaky-guard lesson from 1.99.3, now a rule in the
  item shape).
- Both lanes' `EQBUDDY_EXPAND` facts agree on names (`progressInline`, `progressTab`,
  `progressWindowOpen`) so one E2E-style assertion reads both.
- A person clicks it on Linux or macOS once before the tag: expand Progress, pop out, close,
  expand, change tab in the window, close, expand. That is the sequence no test could reach
  before today.

#### Out of scope

Fixing the upstream bug or filing on it (we are not blocked on them once nothing moves);
changing WPF's seam (it is the model); the Loot and Creature lifts (they are PR 2/3's first
commits, not this item's); anything about the inline card's LOOK (Bevel has ruled; PR B ports
the WPF result).

#### Decided without asking (→ `DECISIONS.md`)

Option (a) over (b)/(c); the never-move rule as a trap with a source-scan guard; PR A before
the Avalonia card rather than with it; the window renders only the visible tab (WPF's rule)
rather than every tab every tick.

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

### Amendment — Fable 5, 2026-08-22, after PR 1 (WPF)

**The plan assumed a lift it had already banked.** "Each PR lowers the baseline" was written
for a MainWindow that still held the Progress surfaces; the Progress fold had already moved
them, so PR 1 had nothing to move and the WPF file grew 4,424 → 4,504 (131 of headroom against
~80 a theme). The executor's read is right. Corrected sequencing:

- **WPF:** PR 2's first commit lifts the `EQBUDDY_EXPAND` dump block (~130 lines of pure
  string-building — a sum, not a pixel) into `WidgetDump.cs`, a class taking `MainWindow` and
  reading `internal` accessors. It must NOT be a partial (the ratchet glob sums partials).
  Lower the baseline in the same commit. That buys PR 2 and PR 3.
- **Avalonia:** the seam item above IS the lift — Progress first (PR A), then Loot and Creature
  as the first commit of PR 2 and PR 3 respectively.
- **The Avalonia half of PR 1 is PR B of the seam item**, not a re-run of PR 1.

Also carried from PR 1: the window calls `ThemeHost.SelectTab` on its own tab change
(`ProgressWindow.TabChanged`, both lanes) — done on WPF, to be mirrored in PR B.

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
