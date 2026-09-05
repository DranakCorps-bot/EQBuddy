# The UI/UX rework — audit, system, and gate log

**Gate 1 (§1–§8): accepted 2026-08-17.**
**Gate 2 (§10): Quests, both UIs, built 2026-08-17.**
Deliverables per the brief: current-state audit · token proposal · component proposal ·
icon strategy · WPF/Avalonia parity strategy · migration order · known risks.

---

## 1. Current-state audit

Measured across the 231 `.cs`/`.xaml` files under `src/` (excluding `obj`/`bin`), plus a
read of the shipped screenshots.

### 1.1 What is already good — build on this, don't rebuild it

**Colour is already a token system, and a shared one.** `UI.Shared/ThemePalettes.cs`
holds 21 named keys × 8 themes (ParchmentBrass, BlueGrey, Grey, HighContrast, Redish,
Solarized, SolarizedDark, Turquoise) as data. WPF composes a `ResourceDictionary` from it
at runtime; Avalonia mutates brush singletons from the same table. `ThemePaletteTests`
fails the build if a palette is partial or a value doesn't parse.

This matters more than it sounds: **it is a working proof of the parity pattern this whole
effort needs** — tokens as framework-free *data* in `UI.Shared`, composed per framework,
with a test forbidding drift. The design system should extend that pattern to typography,
spacing and shape rather than invent a second mechanism.

`UI.Shared` already holds 40+ presentation modules (`Countdown`, `WidgetMetrics`,
`SpawnsViewModel`, `HistoryPresentation`, `GearChecklistPresentation`, `AlertColors`,
`MapColors`…). The separation the brief asks for — state/ViewModels untouched, presentation
improved — is largely already the architecture. **This is a presentation-token and
component gap, not an architectural one.**

### 1.2 Typography — no scale

| | |
|---|---|
| Distinct `FontSize` values | **13** — 7, 9, 9.5, 10, 10.5, 11, 11.5, 12, 12.5, 13, 14, 16, 17 |
| Total assignments | **612** |
| Named roles | **0** |

Half-point steps (9.5, 10.5, 11.5, 12.5) are the tell: sizes were nudged per control to
make a specific row fit, not chosen from a scale. 612 literal decisions is 612 chances to
disagree, and nothing can detect a disagreement.

### 1.3 Spacing — no scale

**174 distinct `Thickness` tuples.** The long tail is the problem, not the head: `(0,2,0,0)`,
`(0,3,0,0)`, `(0,4,0,0)`, `(0,6,0,0)`, `(0,8,0,0)`, `(0,10,0,0)`, `(0,12,0,0)` all exist as
"a bit of space above", chosen independently. `(20,2,0,0)` appears 14 times as an ad-hoc
indent.

### 1.4 Shape — 7 radii

`CornerRadius` takes **3, 4, 5, 6, 8, 10, 12** across 177 uses. Cards, chips, popups,
buttons and badges each ended up with their own geometry. Nothing distinguishes "this is a
card" from "this is a chip" except a number someone picked once.

### 1.5 Icons — 84 glyphs, and duplicates for one meaning

**84 distinct non-ASCII glyphs, 857 uses**, mixing four unrelated families: emoji
(🗺 📌 🎯 🐾 🐌 🕒 🔔 📍 🎒 🔍 📱 💀 💤 🔒), geometric shapes (▸ ▾ ▶ ▲ ▼ ▮), dingbats
(✓ ✔ ✕ ★ ✦ ⚑) and technical symbols (⧉ ⧗ ⟳ ↻ ⤴ ⤡ ⇣).

The same concept has more than one glyph:

| Meaning | Glyphs in use |
|---|---|
| done / confirm | `✓` ×62 and `✔` ×15 |
| favourite | `★` ×15 and `⭐` ×5 |
| refresh | `⟳` ×22 and `↻` ×4 |
| increase / decrease | `▲▼`, `⬆⬇`, `⇣` |
| expand | `▸`, `▾`, `▶` |

Emoji also render at a size and weight the app does not control, and vary by platform —
which is not hypothetical here: **PRs #148 and #166 exist because icon glyphs failed to
render at all in Wine prefixes.** Any icon strategy that depends on system fonts re-opens
that bug on Linux/macOS.

### 1.6 Information hierarchy — the Spawns window as the worked example

From `docs/screenshots/spawns-window.png`, which the brief singles out:

- The countdown (`4:21`) and the editable duration (`5m`) carry **near-identical visual
  weight**. The countdown is the glanceable value and the duration is configuration; they
  read as two equal columns.
- Every row shows **two empty input boxes** as visible rectangles. A status surface is
  wearing an editing surface's chrome, permanently.
- Three actions (`▶ 🔔 ✕`) sit at identical size with no grouping or hierarchy.
- Rows without a timer leave the countdown column blank — "unknown" is rendered as
  "nothing", so uncertainty and absence look the same.
- There is **no progress-toward-respawn** anywhere, so "due in 4:21" and "due in 18:31"
  look equally urgent.

The same pattern — box-inside-box, label and value at equal weight — recurs on the widget
cards and the older quest surfaces.

---

## 2. Token proposal

Tokens live in `UI.Shared` **as data**, exactly like `ThemePalettes`, and each UI composes
them into its own native resources. Names are conceptual; they map onto existing brush keys
so nothing has to be renamed on day one.

### 2.1 Colour — mostly a re-mapping, not a repaint

Existing keys already cover most of the brief's list. Proposed conceptual mapping:

| Concept | Today |
|---|---|
| WindowBackground | `BgBrush` |
| Surface | `PanelBrush` |
| SurfaceHover | `PanelHoverBrush` |
| SurfaceSelected | *(new — derived, see below)* |
| Divider | `BorderBrush` at reduced alpha (`ThemeTones` already derives hairlines) |
| TextPrimary / TextSecondary / TextMuted | `TextBrush` / `DimBrush` / *(new step)* |
| Accent | `AccentBrush` |
| Success / Warning / Danger / Info | `GoodBrush` / `WarnBrush` / `BadBrush` / `IncomingBrush` |

Gaps to add: **SurfaceRaised**, **SurfaceSelected**, and a third text step. All three should
be *derived* in `ThemeTones` from existing keys rather than added as 8 new hand-picked hex
values per theme — that is how hairlines and bar tracks are already done, and it keeps
`HighContrast` honest automatically.

**The accent discipline the brief asks for is a real change.** Gold is currently used for
section headings, values, borders, chips and icons alike. Proposed rule: accent means
*selected, primary action, or the single most important number on the surface*. Everything
else steps down to TextPrimary/TextSecondary.

### 2.2 Typography — 7 roles, replacing 13 sizes

| Role | Size / weight | Used for |
|---|---|---|
| `TitleWindow` | 14 SemiBold | Window title row |
| `TitleSection` | 12.5 SemiBold | Card and group headings |
| `Metric` | 16–17 Bold | The one number a surface exists to show |
| `Body` | 12 Regular | Rows, list content |
| `BodySecondary` | 11.5 Regular, TextSecondary | Detail lines (NPC · drop location) |
| `Caption` | 11 Regular, TextMuted | Filters, chips, counts |
| `Metadata` | 10 Regular, TextMuted | Footnotes, provenance, the accuracy contract |

That covers all 612 sites. The half-point sizes disappear; 7, 9 and 9.5 are absorbed into
`Metadata`/`Caption` (7pt is below the readable floor and should not survive the migration).

### 2.3 Spacing — a 6-step scale

`XXS 2 · XS 4 · S 6 · M 8 · L 12 · XL 16`

Chosen to absorb the existing head of the distribution (1, 4, 6, 8, 10, 12 dominate) with
minimum visual disturbance. `10` maps to `L 12` or `M 8` case by case; the 14 uses of
`(20,2,0,0)` become an explicit `Indent` token.

### 2.4 Shape — 4 radii, 3 heights

| Token | Value | Applies to |
|---|---|---|
| `RadiusPanel` | 10 | Windows, popups |
| `RadiusCard` | 6 | Cards, list rows, detail panels |
| `RadiusControl` | 6 | Buttons, inputs, combos |
| `RadiusPill` | 11 | Chips, badges, filter pills |
| `RowHeight` / `ControlHeight` / `IconButton` | 24 / 26 / 24 | list rows / inputs / icon actions |

---

## 3. Component proposal

Twelve primitives cover essentially every surface in the app. Each is proposed as
**a shared *spec* in `UI.Shared` + a thin native implementation per framework** (see §5).

| Component | Replaces (examples) |
|---|---|
| `EqCard` | The 14 widget card `Border`s, each currently hand-built |
| `EqSectionHeader` | Quest group headings, options group labels, breakout titles |
| `EqMetric` | dps/kills/loot/xp tiles, card summary values |
| `EqListRow` | Quest checklist rows, spawn rows, loot rows, gear rows |
| `EqChip` | Class chips, mode strip, filter pills |
| `EqStatusBadge` | `ready` / `in progress` / `done`, DUE, instance |
| `EqTimer` | Spawn countdowns, mez/charm chips, buff fades |
| `EqProgress` | Spawn progress-to-respawn, xp bar, quest completion |
| `EqIconButton` | Close, pin, expand, alert-bell, manual-start |
| `EqSearchBox` | Quest search, item lookup, history filter |
| `EqEmptyState` | "Nothing yet — loot a quest item…", empty checklists |
| `EqDetailPanel` | The proposed quest detail pane; later gear and drops |

**`EqTimer` and `EqProgress` are the highest-value pair** — they serve spawns, mez, charm,
buffs and quest readiness, which is most of what the brief calls "EQBuddy's strongest
real-time visual elements", and they are exactly what the Spawns window lacks today.

---

## 4. Icon strategy

**Recommendation: a small vector-path icon set, defined as path data in `UI.Shared`,
rendered by each framework's native `Path`. No icon font, no image assets, no dependency.**

Rationale, in priority order:

1. **Wine/CrossOver already broke on font-rendered icons twice** (#148, #166). A font-based
   set re-opens a bug the project has already paid for on the platforms that are its only
   uncontested ground.
2. Path geometry is data, so it lives beside `ThemePalettes` and gets the same
   anti-drift test treatment.
3. Vectors take the accent/text tokens as fill, so an icon can't be off-palette.
4. Size is controlled by us, not by a font's metrics — which is what makes 84 mixed glyphs
   look mismatched today.

**Scope: roughly 24 icons**, one per concept in the interaction vocabulary the brief lists
(close, pin, expand, collapse, navigate, guide, alert, search, filter, dismiss, more,
refresh, star, check, warning, timer, map, quest, gear, loot, spawn, charm, mez, buff).
Every one of the 84 current glyphs maps onto one of these or is dropped.

**Emoji are retained in exactly one place**: user-facing *text* where they are content
rather than UI (What's New entries, discussion templates). Not in controls.

---

## 5. WPF / Avalonia parity strategy

**Do not build shared XAML.** The brief's instinct is right and the repo has already proved
the failure mode twice: the Avalonia chip stacks shipped a hand-copied older version of the
WPF anchor and carried #122 *and* #152 to Linux and macOS after Windows had already paid for
both.

The pattern that works here is the one `ThemePalettes` already uses:

```
UI.Shared            (framework-free data + specs, unit-tested)
   ├── DesignTokens.cs        colours, type roles, spacing, radii, sizes
   ├── IconPaths.cs           path geometry per icon name
   └── ComponentSpecs.cs      per-component token composition
        ↓                                    ↓
WPF: ResourceDictionary          Avalonia: Styles + brush singletons
     + ControlTemplates                 + ControlThemes
```

Enforcement, so parity is checked rather than hoped for:

- A test asserting **every token key resolves in both UIs** — the direct analogue of
  `ThemePaletteTests`, which already does this for colour.
- A test asserting **no literal font size, radius or thickness** appears in migrated files
  (an allowlist that shrinks per gate). `ArchitectureTests` already does ratchet-style
  enforcement, so the mechanism exists.
- Avalonia migrates **in the same PR** as its WPF counterpart, never "a release behind".

---

## 6. Migration order

Adopting the brief's gates, with one change I'd argue for.

| Gate | Surface | Note |
|---|---|---|
| 1 | **This document** | — |
| 2 | **Quests** | Reference surface. Best choice: just rebuilt, so its *logic* is fresh and settled, and it exercises tabs, chips, search, filters, list rows, status badges and an empty state — 9 of the 12 primitives. |
| 3 | **Spawns + timers** ⟵ *moved up* | See below |
| 4 | Main widget | Cards, metrics, compact constraint |
| 5 | Mini mode + chips | The HUD vocabulary |
| 6 | Map | Heaviest surface; benefits from timers already being solved |
| 7 | Remaining windows | Gear, Drops, History, Travel, Options, breakouts |

**Why Spawns moves ahead of the widget:** `EqTimer` and `EqProgress` are needed by the
mini-mode chips, the map, and the widget's Watch/Buffs cards. Building them on the Spawns
window — where the current design is weakest and the improvement is most visible — means
the widget and chips gates *consume* finished primitives instead of inventing them under
the tightest space constraints in the app. It also front-loads the surface with a specific
brief ("should become one of EQBuddy's strongest real-time visual elements").

---

## 7. Known risks

**1 — On the widget, typography IS geometry.** This is the big one. Both widgets are
`SizeToContent`, so any change to font size, padding or spacing changes the *window size* of
a transparent, always-on-top window. On X11 that is a geometry change over a fullscreen
game, and it is exactly what cost KoboldCoterie his keyboard in #173. Gate 4 must treat
every metric change as a functional change: `WidgetMetrics` and `PerfReadout` exist because
of this, and reserved-size discipline has to survive the restyle.

**2 — The WPF layer has no unit tests.** Per `docs/TestPlan.md` §5 this is structural. So
every migrated surface needs facts pinned into the `EQBUDDY_EXPAND` dump and asserted from
`tests/EQBuddy.E2E` *before* it moves — the same discipline the brief's screenshot review
implies, made mechanical.

**3 — Eight themes, not one.** Every token change must hold in all eight, including
`HighContrast`. Deriving new tokens in `ThemeTones` rather than hand-picking per theme is
what keeps that tractable.

**4 — Screenshot review needs a fixture.** Reviewing real renders is required by the brief,
but the isolated profile shows zeros on every card and the windows are translucent, so a
naive capture is unusable. A seeded-session fixture (the `EQBUDDY_APPDATA` + shifted-log
recipe) and an opaque capture theme are prerequisites for Gate 2, not afterthoughts.

**5 — Scale and DPI.** Widget content sits under a UI-scale `LayoutTransform`; screen
coordinates and pre-scale units are different spaces, and mixing them breaks silently at
non-100% scale (#144). Any new component doing its own arithmetic must go through
`WidgetMetrics`.

**6 — Density is a feature.** EQBuddy's users read these surfaces mid-pull. Every spacing
increase must be justified against glanceability, not just tidiness. Where the two conflict,
the brief's own rule decides: primary values dominate, secondary context recedes — rather
than everything getting more room.

**7 — Scope creep into logic.** The brief forbids it and the codebase makes it tempting,
because presentation logic and state are genuinely close in places (`SpawnsViewModel` builds
display strings). Rule for this effort: a ViewModel may gain a *token or role name*; it may
not change what it computes.

---

## 8. Directional mockups — accepted, with three amendments

David supplied three ChatGPT mockups (Quests, mini mode, Spawns) on 2026-08-17 as
*directional* input. The direction is accepted and it agrees with §2–§3 closely enough to
treat as validation: named type roles, a real spacing rhythm, grouped actions, semantic
state badges, progress bars on timers, and one consistent icon family.

Three things in them cannot be built as drawn. Recording them here so nobody spends a gate
discovering it.

### 8a. Reward and quest ICONS are not sourceable — substitute slot silhouettes

The Quests mockup's centrepiece is a grid of per-item reward icons plus a per-quest type
icon (scroll, mushroom, castle, potion, egg). **EQBuddy cannot produce either.** The
shipped `ItemCatalog` carries `Name`, `StatsText`, `Slots`, `Skill`, `QuestFlagged` and
nothing else; there is no icon id anywhere in the codebase, and the 2026-08-15 spike
established that although the game ships the icon sheets, nothing maps an item to one (the
wiki's `lucy_img_ID` was disproved). Quests have no type taxonomy either.

Drawing them anyway would mean inventing an icon per item — confidently wrong art on a
surface whose entire value is being trustworthy.

**Substitute, using data we do have:** reward tiles become **slot silhouettes** from our own
vector set, driven by `Slots`/`Skill` (Primary, Secondary, Head, Chest, Ring, …). Same
visual rhythm and the same "+4" overflow, honest inputs, ~20 glyphs we already need for the
Gear surface. Quest rows take a **state-coloured left rule** instead of a type icon —
carrying `ready` / `in progress` / `done`, which is real and semantic, rather than a
decorative category the data does not have.

### 8b. Mini mode: adopt the hierarchy, not the footprint

The mini-mode AFTER stacks a caption under every value (`58` / `Kills`). The hierarchy is
right and should be adopted. The footprint is not: it makes the pill both wider and taller,
and mini mode's stated purpose is maximum information per pixel.

More seriously, **the widget is `SizeToContent`, so pill width is window geometry**. A
metric whose label or value changes width makes a transparent always-on-top window ask the
windowing system to resize — trap 12, and the direct cause of #173.

**Resolution:** take the value-dominant/label-subordinate treatment, but give every metric
cell a **reserved width and a fixed text shape**, exactly as `UI.Shared/PerfReadout.cs`
already does for the CPU/RAM readout. The pill then repaints without ever re-measuring.
Where a label costs a line it earns, prefer an icon plus tooltip over a second text row.

### 8c. Spawns: keep the free-text duration, drop the numeric spinners

The Spawns AFTER is otherwise the strongest of the three and matches the Gate 3 plan almost
exactly — progress toward respawn, a percentage, `—` for unknown with an empty track,
grouped actions, column headers, the help text as an info callout, labelled add-row fields.
All adopted.

But the duration field is drawn as a **numeric spinner**, and today it is free text parsed
by `SpawnDurationText`, which accepts `5m`, `90s`, `22m`, `3d 12h`. A spinner regresses a
capability players use for week-long raid targets. **Keep the text field** (add stepper
affordances that adjust the parsed value if we want the up/down target), and keep the
placeholder guidance the mockup adds, which is a genuine improvement.

### 8d. One note on the mockups' "BEFORE" panels

The Quests BEFORE shows a window with `Show Hidden` / `Show Completed` checkboxes and a
`Filters:` row. That surface no longer exists — it predates the 2026-08-16 consolidation.
The comparison overstates the delta slightly; it does not change the direction, but Gate 2
must be measured against the **current** tracker, per the brief's own warning.

## 9. What Gate 2 would deliver

Quests, rebuilt on the system above: header + tabs, prominent search, status filters, a
compact list with a detail panel, `EqListRow`/`EqStatusBadge`/`EqChip`/`EqEmptyState` in
their first real use, both UIs in one PR, existing tests green, new E2E facts pinned, and
reviewed screenshots — with functionality unchanged from today's tracker.

---

## 10. Gate 2 — Quests, as built (2026-08-17)

**Functionality is unchanged.** Same filters, same five modes, same three tabs, same ledger
calls, same undo, same search debounce, same render cap, same class inference. No
application logic was touched. What changed is the presentation, and one thing that is
presentation but reads as behaviour: **a column of self-contained cards became a LIST plus
a DETAIL PANE.**

### 10.1 Why the shape changed, and not just the paint

Every card carried its own rewards, meta line, item rows and five controls. Finding the one
quest that is ready meant reading fifty paragraphs — and on the "all" view the window built
sixty of those, each wiring its own wiki tooltips. The list answers *which quest* and the
pane answers *what about it*, which is the order the question actually gets asked in. It is
also what made room for the two things the surface never had: a status badge and a
state-coloured leading rule, both carrying readiness at a glance.

### 10.2 What landed

| Layer | File | Notes |
|---|---|---|
| Tokens | `UI.Shared/DesignTokens.cs` | 7 type roles, a 6-step spacing scale, 4 radii, 3 control sizes — framework-free data, exactly as `ThemePalettes` already was for colour |
| Icons | `UI.Shared/IconPaths.cs` | 42 paths on a 24×24 grid: the interaction vocabulary, plus 14 reward silhouettes and the item→silhouette mapper |
| State vocabulary | `UI.Shared/QuestPresentation.cs` | Badge, rule colour, ready summary, meta line, distance wording — decided once for both desktops |
| Capture | `UI.Shared/CaptureTheme.cs` | `EQBUDDY_OPAQUE=1` makes the window ground opaque, and nothing else |
| WPF composition | `EQBuddy/DesignSystem.cs`, `EQBuddy/Theme.xaml` | Token ResourceDictionary + the `Eq*` component styles |
| The surface | `EQBuddy/QuestsView.xaml{,.cs}` | Was `QuestsWindow.xaml{,.cs}` until E-3 PR 3 lifted the surface out of the window so the Evolved shell could host it too. The Gate 2 content is unchanged by that move; `QuestsWindow` is now a thin host and `QuestsRoom` is the shell's, and both files are on the ratchet |

*(An `EQBuddy.Avalonia/DesignSystem.cs` composed the same tokens against native controls, and
the rule for this gate was that both surfaces changed together, never "a release behind". That
lane was deleted on 2026-09-04; the tokens above are the part that was always the point.)*
| Fixture | `scripts/shoot.ps1` | Seeded session + opaque render + plain backdrop |

Nine of the twelve §3 primitives got their first real use: card, section header, list row,
chip, status badge, icon button, search box, empty state, detail panel. `EqTimer`,
`EqProgress` and `EqMetric` did not — they belong to Gate 3 (Spawns) and Gate 4 (the
widget), which is the order §6 argues for.

### 10.3 The amendments, honoured

- **§8a — no sourceable reward or quest-type icons.** Reward tiles carry **slot
  silhouettes** driven by the item's own `Slots`/`Skill`, with the weapon skill outranking
  the slot so a 2H Blunt is never drawn as a sword, and a neutral crate for anything the
  catalog does not place. Quest rows carry a **state-coloured left rule** instead of a type
  icon. Both are checked against all ~11k shipped items.
- **§8d — measured against the CURRENT tracker**, not the mockup's stale BEFORE panel. The
  `Show Hidden` / `Show Completed` row it shows has not existed since 2026-08-16, and the
  "+ I have this" quantity row the mockup's AFTER keeps was deliberately removed on
  2026-08-15; neither came back.

### 10.4 How this is kept from drifting

`DesignRatchetTests` names each migrated surface and fails the build if it grows a literal
font size, radius or spacing value, or draws with a glyph instead of a vector. **The list
only ever grows — add a surface in the same PR that migrates it.** That mechanism, not
good intentions, is what makes §2's "7 roles replacing 13 sizes" survive Gate 3.

### 10.5 What the screenshot review caught

Worth recording, because it is the argument for the review criterion itself. The first real
capture showed the class-inference note reading *"pick classes ab"* — clipped, not wrapped.
A `TextBlock` with `TextWrapping="Wrap"` inside a horizontal `StackPanel` never wraps: a
stack measures its children with infinite width. No unit test could see it and both UIs had
it. It is now a two-column `Grid` (`IconLine`) in both, and trap 14 in `CLAUDE.md`.

### 10.6 Next

Gate 3 is **Spawns + timers**, per §6 — and per §8c the duration field stays **free text**
(`SpawnDurationText` parses `5m`, `90s`, `3d 12h`; a numeric spinner regresses week-long
raid targets). §11 amends what comes after it.

---

## 11. Folding in 1.89.0's other work (2026-08-17)

Three contributions merged alongside Gate 2 — #198 loot provenance, #199 the mini-bar
double-click, #200 a (Disabled) alert sound — and six community commitments were made in
the same sitting. This section folds them into the plan rather than leaving them as
parallel work, which is how a design system ends up describing a product that has moved.

### 11.1 Where the numbers actually are

Re-measured across `src/` today (same method for each row, so the rows compare with each
other; the font-size figure is directly comparable to §1.2's 612):

| | Gate 1 | Now | |
|---|---|---|---|
| `FontSize` assignments | 612 | **550** | Gate 2 removed ~60, all from one window |
| Distinct font sizes | 13 | **13** | none retired yet — the tail lives in un-migrated surfaces |
| Distinct literal radii | 7 | **11** | *worse*, and see below |
| Icon glyphs | 84 / 857 | **80 / 781** | |

**One gate has not dented this, and was never going to.** That is the expected shape — the
system exists, and 1 of 7 surfaces uses it. The number worth watching is not the total but
whether the *un-migrated* total grows, which is what §11.2 is about.

### 11.2 New UI arrived off-system, the same day the system did

`#198` added a view filter to the Loot card and its breakout:

```xml
<TextBlock x:Name="LootViewAll" Text="all" FontSize="10" Cursor="Hand"
           Tag="all" MouseLeftButtonDown="OnLootView" Margin="0,0,6,0"/>
```

That is, precisely, the pattern Gate 2 deleted from the Quests window six hours earlier —
a segmented control built from bare `TextBlock`s with a `Tag`, a click handler, a literal
size and a literal margin, coloured by a hand-written `ApplyVisual`. **There are now 16 of
these across `MainWindow.xaml` and `BreakoutWindow.xaml`**, plus the Avalonia sort bars,
and Gate 2 built a seventeenth as a real primitive and then hid it inside one window.

This is not a criticism of the contribution — it is the correct way to build a filter row
*in the codebase as it was*. It is evidence for the thing the whole effort is about: **a
component nobody can reach gets rebuilt by hand, and the ratchet only guards files already
on the list.** Two consequences:

1. **`Chip` comes out of `QuestsWindow` and becomes a shared component** (`EqChip` +
   `EqSegmentedStrip` from §3) in both UIs, before Gate 3 needs a third copy.
2. **A surface joins `DesignRatchetTests.Migrated` when it is migrated, not before** — but
   the *reverse* now matters too: a PR adding a new segmented strip should be pointed at
   the primitive in review. That is a process note, not a test.

### 11.3 What the merged work changes about the gate order

| Merged | Lands on | Effect on the plan |
|---|---|---|
| **#198** loot provenance | Loot card + Loot breakout | Adds a filter strip to two surfaces. Pulls Loot **forward** — it is now the second-biggest concentration of hand-built strips after the widget |
| **#199** mini-bar double-click | Mini bar + Options | Establishes the chip gesture Gate 5 was going to have to invent. **Adopt it as the convention** rather than designing another |
| **#200** (Disabled) alert sound | `OptionsViewModel` | Confirms the shared-viewmodel route for Options; Gate 7 composes rather than rewrites |

### 11.4 Community commitments, placed

Made publicly on 2026-08-17, so these are owed, not optional:

| # | Reporter | Gate |
|---|---|---|
| **#190** tracked-quest chips (double-click opens, right-click dismisses) | wizen | **5** — same chip vocabulary and the #199 gesture |
| **#191** configurable mini bar, removable metrics | TheMegaSage | **5** — with §8b's reserved widths, non-negotiable (#173) |
| **#182** breakout resize affordance + full name on hover | Ladylag | **7**, but the `.`-name half is a **parser bug and not a gate** — fix it now |
| **#189** Quest Tracker doesn't hide with the widget | wizen | **Not a gate** — a missing wiring, fix it now |
| **#197** audio picker filter too narrow | wizen | **7** (Options), or sooner; it is one string |
| **#135** Puppet Strings charms | bjstrange | **Not a gate** — charm catalog has no entry for item clickies |

**Four of those are not design work at all.** Keeping them out of the gates is the point:
a rework that absorbs every open bug stops being a rework.

### 11.5 Amended order

| Gate | Surface | Change from §6 |
|---|---|---|
| 2 | Quests | done |
| **2b** | **Lift `EqChip` / `EqSegmentedStrip` out of `QuestsWindow`** | **done** — `UI.Shared/ChipStyle.cs` + one control per UI; the Quests window now spends it. Render verified byte-for-byte unchanged |
| 3 | Spawns + timers | **built** — `UI.Shared/TimerView.cs` is `EqTimer` + `EqProgress`; both windows rebuilt on it. reviewed; see §11.6 |
| **4** | **Loot card + Loot breakout** | **built** — see §11.7; moved up because #198 concentrated the debt here |
| **5** | **Main widget** | was 4 — staged. **5a** §11.8, **5b** §11.9 (seam + three batches), **5c done** §11.10: all four widget files on the ratchet. **5d** (`Theme.xaml` templates) is what remains |
| 6 | Mini mode + chips | carries #190, #191, #199's gesture |
| 7 | Map | unchanged |
| 8 | Remaining windows | Gear, Drops, History, Travel, Options (+#197), breakouts (+#182) |

The one structural change is **2b**: a shared chip is worth more than any single gate,
because every gate after it spends the primitive instead of minting one.

### 11.6 Gate 3, as built — and the one thing still open

`UI.Shared/TimerView.cs` is the §3 `EqTimer`/`EqProgress` pair, and it answers all four of
the Gate 1 findings about this window at once, because they were one defect: **a countdown
that is only a string has no state.** A timer now has one, and the state decides the words,
the ink, and whether there is a bar at all — so a caller cannot draw an amber countdown
with a green bar, and cannot render "we don't know" as an empty cell.

- Progress toward respawn, with a percentage, so "due in 4:21" and "due in 18:31" no longer
  look equally urgent. The bar's **star weights are the fraction** rather than a width
  computed from `ActualWidth`, which would be wrong on the first layout pass, wrong again
  after a Ctrl+wheel zoom, and wrong differently under the UI-scale transform (trap 1).
- `—` and an **empty track** for a kill with no known respawn. Unknown and absent are now
  different states, and `TimerViewTests` holds them apart.
- Column headers, grouped actions behind a divider, the help text as an info callout, and
  labelled add-row fields — all the mockup's, all adopted.
- Rows are cards, so a due one is findable among forty by its edge.
- **§8c honoured: the duration is still free text.** `SpawnDurationText` parses `5m`, `90s`,
  `22m` and `3d 12h`; the mockup's numeric spinner would regress week-long raid targets.
  The placeholder guidance it added is adopted.

**The screenshot review is now possible, and it immediately earned itself again.**
`EQBUDDY_SPAWNS=1` (or a zone name) joins the hook family, so `scripts/shoot.ps1` can open
a window that deliberately hides itself. The first two captures showed two real defects no
test could see:

1. **The progress bar was penned inside the 150px timer column.** David, looking at the
   released window: *"we have room between the columns."* Right — the bar now spans the
   whole card beneath the columns, which is where the room is and what a bar is for.
2. **Every column header sat 115px left of the column it named.** The actions column was
   `Auto`, so it measured zero in the header (which has no buttons) and ~115 in a row. It
   is a fixed lane now, which also stops a row reflowing under the player's cursor the
   moment a timer starts and a Clear button appears.

**Still worth doing:** the fixture has no running timer in a catalogued zone, so the bar
itself is unit-tested but has not been seen. Seeding one named kill into the fixture log
would close that.

**Previously open:** `scripts/shoot.ps1` cannot capture this window,
because the window deliberately stays hidden until a countdown exists (David, 2026-08-02:
"a tracker parked on screen all session is noise") and the fixture log's kills do not start
one. Every other satellite has an `EQBUDDY_*` hook to open it; Spawns does not. **Add
`EQBUDDY_SPAWNS=1` to MainWindow's hook family and re-shoot** — small, and the review is an
acceptance criterion, not a nicety: the Gate 2 wrapping bug was found this way and by
nothing else.

---

## 11.7 Gate 4, as built — Loot (2026-08-17)

**Four surfaces, one set of decisions.** The Loot card and its breakout on Windows, the
Loot card on Linux/macOS, and the rules all three read. The two hand-built strips #198
added are now the app's `EqChip`/`EqSegmentedStrip`, which was most of the gate as
predicted — but converting them exposed the thing worth recording:

### The strips were the symptom; the duplicated rules were the disease

`LootRows` already owned row ORDER and was already shared. Everything *around* the rows
was not. **Which strips are up, which chip is lit, whether "recent" is worth offering, and
what an empty slice says** were derived twice from the same four snapshot lists — once in
`MainWindow.RenderLoot`, once in `BreakoutWindow.UpdateLoot` — and the copies had already
drifted: the breakout's chips carried no hover copy at all, and the legacy `"made"` view
alias was spelled inline in both. That is trap 4 (one entry, two sources for one fact) in
a surface small enough that nobody noticed.

`UI.Shared/LootPresentation.cs` is now the one source, and because it is framework-free it
is *tested* — `LootPresentationTests`, 34 cases — which none of it was before. The WPF
layer has no test project (docs/TestPlan.md §5), so moving a rule into UI.Shared is the
only way it gets covered at all.

| Layer | File | Notes |
|---|---|---|
| Decisions | `EQBuddy.UI.Shared/LootPresentation.cs` | Strip options + tooltips, view/sort normalization, strip visibility, empty-slice wording, both headers, the target heading |
| WPF card | `EQBuddy/LootCardView.cs` | Lifted out of `MainWindow.xaml`, the way `QuestChecklistView` was |
| WPF breakout | `EQBuddy/LootBreakoutView.cs` | Lifted out of `BreakoutWindow`, which serves six kinds |
| Icon | `IconPaths["Target"]` | The 🎯 that was baked into a heading STRING |
| Token | `DesignTokens.IconInline` | 12 — an icon inside a line of text. `IconButtonSize` (24) would make every loot row a third taller, and on the widget row height is window height |
| Token | `DesignTokens.IconInlineHit` | 16 — the TARGET of an inline icon that is clickable, which is bigger than the icon drawn in it. A vector only hit-tests where it is painted, so the map-pin badge that replaced an emoji had gaps you could click through (#211). Fits inside one line of body text, so the target grows and the row does not |
| Milestone | `EQBuddy/MainWindow.xaml` on the ratchet | The FIRST widget file to join (Gate 5c). 80 literal sizes became tokens; the repeated tuples got names in `DesignSystem.Tokens()` rather than 31 one-offs. The widget went 342×643 → 338×635 — snapping to the scale, verified against a before/after shot |
| Decision | `UI.Shared/BreakoutPresentation.cs` | What a breakout window calls itself and which vector it wears. Keyed by STRING, not `BreakoutKind` — that enum is declared separately per UI and the two disagree (WPF has Watch and Loot; Avalonia does not). A shared enum would have to pick a side, and that gap is a missing feature rather than a labelling one |
| Decision | `UI.Shared/MiniBarPresentation.cs` | The minimized bar's cells — order, icon name, formatted value. Both widgets carried this table by hand, identically, comments and all. Says what a cell CONTAINS and never how wide it is: reserved widths belong to the bar that draws them (#173), and arrive with #191 |
| Control | `DesignSystem.InlineIconButton` | THE clickable inline icon, both UIs. A real button, so it has a transparent ground, a hover, and keyboard reach — never a bare `Icon()` with a `Cursor` and a handler |

### The Avalonia card was a release behind, and that is the interesting part

This is the one place in Gate 4 where behaviour changes. #198 gave the Windows card a
show filter, a sort strip and inline provenance on 2026-08-17; the Avalonia card still
listed `s.Loot` raw, with merges in a separate "Created by merging" block and no way to
tell a foraged root from a corpse drop. **The shared row builder existed the whole time
and this UI simply never called it** — the same shape as the chip stacks carrying #122 and
#152 to Linux after Windows had already paid for both. Both filters, the provenance tags,
the timeline sort and the empty-slice wording arrive there in this change, reading the same
two settings, so a profile shared between a Windows and a Linux machine behaves the same.

### Why the card HEADER was left alone

Thirteen cards wear the same `Section` expander and the same emoji-and-count header.
Migrating one of them would read as a bug rather than as a migration, so Gate 5 changes
them together. The same argument keeps the breakout's title row, `Target|Session` toggle
and size grip out of this gate: six kinds share them, and the last of the six is Gate 8.
**A gate migrates a surface, not everything a surface touches.**

### What the screenshot review caught, again

Two new shots exist so this gate could be reviewed at all — and a card body could not be
photographed before, because expansion is not persisted:

- **`EQBUDDY_EXPAND` now takes card keys** (`EQBUDDY_EXPAND=loot`), alongside the existing
  `=1`. It is the same move Gate 3 made with `EQBUDDY_SPAWNS`, and `scripts/shoot.ps1`
  gained `loot-card` for it.
- **`loot-breakout` needed no hook at all** — that window shows whenever the widget is
  minimized and its stat is starred, and both are plain settings.

The first breakout capture came back with **no filter strips on it**. They were built,
selected and painted; the `ContentControl` they hang in was declared `Visibility="Collapsed"`
in XAML and nothing ever set it back. **A control that hides itself inside a host that also
hides itself has two switches for one state, and only one of them is ever wired.** Nothing
about that is visible in a diff, in a unit test, or in a build — the same category as the
Gate 2 clipping and the Gate 3 header offset, and the third gate running to find its own
bug this way.

---

## 11.8 Gate 5a, as built — the widget's shared vocabulary (2026-08-18)

**Gate 5 does not fit in one change, and pretending otherwise would be the wrong call.**
Measured at the start: **473 ratchet violations** across `MainWindow.xaml` (732 lines),
`MainWindow.xaml.cs` (4,552) and the Avalonia `MainWindow.cs` (5,165) — 127 literal font
sizes, 174 spacing tuples, 167 glyphs. Gate 2 restructured one window; this is fourteen
cards, the chrome, the mini bar and two UIs, on the surface a player looks at all session.
The ratchet is per-FILE and all-or-nothing, so a partial migration earns no entry — which
means the gate has to be staged by *vocabulary*, finishing each shared thing everywhere it
appears rather than finishing one card at a time.

**5a is the two pieces that are shared by every card**, and it took the count to **427**.

### The card headings

Fourteen headings, each a single TextBlock whose text began with an emoji:
`Text="&#x1F480; Kills" FontSize="13"`. Two design decisions typed fourteen times, on the
one surface that is always on screen — and emoji are exactly what failed to render under
Wine in #148 and #166, on the Linux and macOS builds that are EQBuddy's only uncontested
ground. A card header rendering as a hollow box is the first thing a player sees.

- `OverlaySections.Icon(key)` maps card → icon for **both** UIs, beside the catalog that
  already mapped card → title. Names are SHAPES ("Skull"), not cards ("Kills"), so a card
  can be renamed without stranding an icon.
- Seven new paths in `IconPaths`: Swords, Heal, Skull, Sparkle, Group, Coin, Scales.
- WPF gets `EqCardTitle`, a control rather than a Style — a heading has two variables and a
  Style cannot take arguments. XAML now says `<local:EqCardTitle Icon="Skull" Text="Kills"/>`.
- Avalonia's headings already funnelled through one `Header(...)`, so it was one change.
- `DesignSystemTests` now fails if a card names an icon that doesn't exist.

### The sort strips

"sort: total dps hits avg" — bare TextBlocks with a `Tag`, a shared `ParseSort` and a
hand-written `SetSortVisual`, three times in the WPF XAML and once in Avalonia's
`SortHeader`. They are `EqSegmentedStrip` now, and **what** they offer comes from
`UI.Shared/SortStrip.cs`, which also fixes something that was quietly wrong: healing counts
CASTS and rates in HPS, and both UIs derived that from a substring test on the heading text
("does the title contain 'Heal'?") in two separate places.

Damage-taken deliberately has no rate column: incoming damage per second of *your* combat
time is a number with no meaning, and offering it invites reading it as somebody's DPS
on you.

### What the screenshot review caught — twice in one gate

1. **The strips overlapped their own headings.** Each sat in a ONE-CELL Grid with the
   section label, both aligned to opposite edges — fine for four small words, and a
   collision once they became pills. Two-column Grid, `*` and `Auto`, exactly as trap 14's
   worked example does for icons beside text.
2. **Then the heading was trimmed to "Damage b…"** — because four chips plus a "sort:"
   caption do not fit beside a sixteen-character heading in a 342px window. The caption
   went: a strip that sits beside a heading naming its own list does not need to announce
   that it is a sort. **A caption earns its place when two strips share a row** (the Loot
   card's show/sort), and `SortStrip.Caption` says so where both UIs read it.

### What is left, and the order it should go in

427 violations. `MainWindow.xaml.cs` cannot join `DesignRatchetTests.Migrated` until every
one of them is gone, so the remaining stages are:

- **5b — the card bodies.** The biggest block, and the one that wants surfaces LIFTED into
  their own files the way `LootCardView` was, since that is what also buys hotspot headroom.
- **5c — the chrome**: title bar, KPI strip, mini bar. Carries #191 (TheMegaSage, approved)
  and must respect §8b's reserved widths (#173).
- **5d — `Theme.xaml`'s templates**: the ⭐ star toggle and the ▸ expander chevron are
  glyphs inside shared ControlTemplates, so they belong to no single card.

---

## 11.9 Gate 5b — the card seam, proved on one card (2026-08-18)

**Lifting files was moving lines without moving dependencies.** `QuestChecklistView`,
`LootCardView` and `LootBreakoutView` each took `MainWindow` as a constructor argument and
reached back through it, and `MainWindow` now carries **61 internal members**, most of them
there for exactly that. Repeating that another thirteen times ends with a small host class
and an enormous service surface — and the line ratchet would be perfectly happy about it,
because the lines really did move.

So 5b introduces two seams before converting anything else:

- **`IWidgetCard`** — key, body, `Render(snapshot)`. The host orders cards by
  `SectionOrder`, hides them by `HiddenSections` and renders the expanded ones; a fifteenth
  card touches the host nowhere.
- **`ICardContext`** — the six things a card may ask the widget for, implemented
  **explicitly** by `MainWindow` so none of them becomes public API. This is the half that
  matters: a card depends on six methods rather than on 4,552 lines, and can be exercised
  against a fake. **If this interface starts growing, that is the signal that something is
  being pushed into cards which should have gone to UI.Shared.**

**Proved on the Kills card, chosen because it asks the widget for nothing** — no item
popups, no wiki lookups, no repaints. If a card could not be built and tested this way,
that is worth learning for the price of one card rather than fourteen.

`UI.Shared/KillsPresentation.cs` holds what the card SAYS, and `KillsPresentationTests`
asserts it with no window at all — the first time any card's content has been testable
(docs/TestPlan.md §5). It immediately paid for itself: the farming block's indentation was
**six literal spaces prefixed to the item name**, which a proportional font renders
differently at every zoom and which nothing could assert. It is a flag and a real margin
from the spacing scale now.

**Definition of done for the rest of 5b:** `MainWindow.xaml.cs` under ~800 lines of host
logic, each card ≤300, both ratchets green, every card testable headless.

---

## 11.10 Gate 5c, finished — all four widget files on the ratchet (2026-08-19)

`MainWindow.xaml`, both `BreakoutWindow` files and finally **`MainWindow.xaml.cs`** are on
`DesignRatchetTests.Migrated`. §11.8 said the code-behind probably could not join, and the
reason it was wrong is worth keeping.

### The glyph count was mostly not glyphs

~74 were counted. **56 were in COMMENTS**, where the rule's argument does not reach — a
glyph in a comment never renders, so it cannot render at the wrong size or fail to render
at all. Exempting them removed most of the number and, more usefully, stopped the real
offences hiding among them.

Of what was left, the tempting concession was to exempt STRING LITERALS too, on the
grounds that CLAUDE.md permits emoji in user-facing text. Measuring killed that: the
largest single group of quoted glyphs was not prose but **controls that happen to be
quoted** — `AppTheme.IconButton("⧉", …)`, the mini-bar icon table, expander chevrons, a
menu header. Exempting strings would have exempted the rule's own target.

What survived was a small editorial set — text that NAMES a control. Those were
**reworded**, not exempted, because a tooltip that draws the glyph it is explaining
("click the 🗺 to see its quests") draws a box on exactly the prefixes where the
explanation matters most, and that one did it twice in one sentence.

**The decision is recorded in `DesignRatchetTests`' own doc comment.** It is the kind of
rule that gets relitigated every time it is inconvenient.

### Three conversions worth more than the count

1. **The buff-suggestion tick and cross were click-handled TextBlocks** — #211 waiting to
   happen. A TextBlock hit-tests across its whole layout rect and a vector only where it is
   painted, so a naive swap ships two controls with holes in them, on a pair where a missed
   click either adds a buff you did not want or fails to silence a suggestion you are tired
   of. `DesignSystem.InlineIconButton`, and keyboard-reachable for the first time.
2. **`FillList`'s quest-badge branch was dead, in that same shape.** Nothing has passed
   `questBadges` since the Loot card moved onto `EqCardRows`, which draws that badge itself.
   Deleted rather than converted: dead code carrying a bug already paid for is worse than
   no code, because the next caller inherits it.
3. **`SpawnChip.Icon` was a glyph, and all three chip windows printed it into a TextBlock.**
   So on the Wine prefixes of #148/#166 the fight-side stack distinguished its three kinds
   with three identical boxes — on the surface a player watches mid-pull, on the platform
   this build exists to serve. It is an `IconPaths` name now, drawn in its own column by
   `SpawnChipsWindow`, `MezChipsWindow` and both Avalonia twins.

   **The snail did not survive as a snail.** A spiral shell legible at 12px is more drawing
   than `IconPaths` should carry, and the chip's own label already says what it is
   ("Slowed 55% · disease 1"). Slow is an hourglass, mez a crescent, a spawn the stopwatch
   it already was. Meaning lives in the words; the icon separates the kinds.

### The Watch card's strip was a shared decision, not a paint job

Both UIs held the same four `(mode, label)` tuples inline and compared them to a STRING
setting. A key spelled differently in one lane lights no chip at all — a segmented control
offering four options with none selected, which is the silent no-op with the switch on the
other side. `UI.Shared/SortStrip.ForWatchRules` owns them, both UIs render it through their
own `EqSegmentedStrip`, and `SortStripTests` asserts every key is one the render code
branches on and that the stored default selects one.

### Two things the screenshot review needed staging for

Neither the Watch card's strip nor the Raids surface's boss rows could be photographed at all:
the strip appears only above two or more rules, and the Raids body only once something is
defeated. `shoot.ps1` gains **`tracked-card`** (three rules the fixture actually matches,
one of them with three kinds under it so the fold shows) and **`raids-card`** (a seeded
`raid-kills.json` with a witnessed tiered clear, an achievements-only clear with no badge,
and the open bosses under them).

**A shot name is a filename.** The Watch shot was nearly called `watch-card`, which would
have silently overwritten `docs/screenshots/watch-card.png` — a hand-taken illustration
that `docs/WatchListGuide.md` embeds — with the fixture's three rules.
