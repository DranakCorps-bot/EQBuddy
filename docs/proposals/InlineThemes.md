# Proposal: themes expand in place, and pop out only if you want them to

**Status:** proposal, not a decision. Written 2026-08-21 for review by David, Bevel and
Scribe. Nothing here is built.

**David's ask, verbatim (2026-08-21):** *"I'm thinking we might want to revisit the themes
and instead of opening breakout windows, we have expandable sub-categories under them with
an option to pop out the window if you want to track on a separate screen."*

---

## The problem this is answering

Four card groups were folded into windows over five days: Quests (2026-08-16), Progress
(08-19), Gear & Loot (08-20), Kills & Drops (08-21). Each fold replaced N cards with ONE
card that is a door — click it, a window opens, the content is in tabs.

Two players said the same thing about it within a day of each other.

> "EQ Buddy is starting to get too complicated for its own good. I loved it as a simple
> tracker for motes and other loot, but now it is all pull out cards etc. I simply want to
> track my mote drops in the main window, but now it is hidden behind too much other junk
> that I don't care about." — daetien-lab, #228

> "The xp / hr & aa / hr, mote breakdown, respawn timer, and alerts were the main thing I
> used this for… Motes are buried and seem to move around, rather than being easy to access
> from the main window." — joeymavity, #228

The fix so far has been reactive and one surface at a time: #219 got the mote rate restored
to the Progress launcher line, and 2026-08-21 gave Motes its card back behind a setting.
**Both are patches on the same structural decision**, which is what makes this worth
revisiting rather than patching a third time.

---

## Three things in the repo that already argue for it

### 1. The app already does inline-plus-pop-out. It is called a breakout.

`BreakoutKind` is `{ Damage, Healing, Pet, Watch, Loot, Buffs, Progress }`. Every one of
those is a card that expands on the widget **and** can pop out to its own floating window,
gated per kind by `AppSettings.DisabledBreakouts` plus the card's ★. Players already
understand it; Options → Cards & windows already lists it.

**So the pattern David is describing is not new machinery. It is the machinery the meters
have had since 2026-08-06, applied to the themes** — which went a different way and got a
launcher instead.

### 2. EQBuddy Mobile already works exactly the way he is describing.

On the phone there are no pop-out windows. A theme is a CARD, and its sub-surfaces are a tab
strip inside that card (`.qtabs` in `index.html`, fed by `ProgressTheme.Tabs`,
`LootTheme.Tabs`, `QuestSurface.Tabs` — the same shared vocabulary the desktop windows use).

**The phone is therefore the working prototype of this proposal**, and it has never drawn a
complaint about being hard to reach. That is a real data point, not a rhetorical one.

### 3. Progress is currently BOTH, which is incoherent and nobody planned it.

`Progress` is in `BreakoutKind` *and* its widget card is a `SectionLink` launcher. So today
it has a pop-out breakout window AND a theme window, and the card itself cannot be expanded.
Whatever we decide, that needs resolving — and it only exists because the two patterns were
never reconciled.

---

## The shape I would build

**The card keeps its one-line summary when collapsed** — unchanged, that is the glance, and
#219 is what happens when it loses a number.

**Expanded, the card shows the theme's tab strip and the selected tab's body, inline.** The
same `EqSegmentedStrip` and the same body the window hosts, not a second rendering of it.
One tab open at a time, so an expanded theme is about as tall as one of the old cards — not
as tall as the four it replaced.

**A ⧉ pop-out control on the expanded card opens the existing theme window** and collapses
the inline copy. That window already exists and already works; it becomes the "put this on
my second monitor" answer rather than the only way in.

**Nothing new gets built for the window side.** `GearLootWindow`, `ProgressWindow`,
`CreatureWindow` and `QuestsWindow` stay exactly as they are.

### Why the tab strip inline, rather than nested expanders

"Expandable sub-categories" could mean each sub-surface is its own expander row under the
card — Loot, Wishlist and Inventory as three collapsible rows. That reads well and it is
closer to the pre-fold widget.

I would still argue for the tab strip, for one reason: **the window, the phone and the card
would then be three different shapes for one set of surfaces.** That is exactly the drift
`LootSurface` / `ProgressSurface` / `CreatureSurface` were created to prevent (#122, #152,
#184), and the fourth copy is where a surface goes missing on one of them. With the strip,
all three draw the same decision from the same Core vocabulary.

**This is the main thing worth a second opinion on.** If nested expanders are materially
nicer to use, that is a design judgement and it should beat my consistency argument.

---

## What it costs, honestly

- **Four themes × two UIs.** Quests, Progress, Gear & Loot, Kills & Drops on WPF and
  Avalonia. Each card changes from a `SectionLink` back to an expander whose body is built
  from the theme's tab list.
- **The bodies are already lifted**, which is what makes this tractable at all: every one of
  these surfaces is already an `IWidgetCard` or a view class with its own `Body`, because
  the folds forced that work. A year ago this would have been a rewrite.
- **A control has one parent.** A theme body cannot be in the card and the window at the
  same time; each host builds its own instance, as `NewGearCard` and `NewProgressSurfaces`
  already do.
- **Two switches for one state (trap 15).** "Is the card expanded" and "is the window open"
  must not both claim to own the surface. The pop-out has to collapse the card, and closing
  the window has to leave the card collapsed rather than silently re-expanded.
- **Widget height.** The widget is `SizeToContent` and always-on-top over the game. Trap 12
  is about a TIMER changing measured size, not a click, so expanding on click is fine — but
  the section scroller's height cap (`SectionScroll.MaxHeight`) is what keeps an expanded
  theme from running off the screen, and it needs checking per theme.
- **`DisabledBreakouts` and the stars** already model "may this pop out" per kind. Reusing
  them is cheaper than inventing a second mechanism, and avoids the settings-only-readers
  trap (20/26).

## What it does NOT change

The surface-allocation rule in `CLAUDE.md` still holds: none of these surfaces earns
**overlay** space by default. This proposal does not put Drops or the Gear Locker over the
running game — it makes the card openable in place *when the player asks*, which is the same
thing every meter card already does.

---

## Open questions

1. **Tab strip inline, or nested expanders?** (the one above — a design call, not mine.)
2. **Does the pop-out replace the inline view, or duplicate it?** I would collapse the card,
   so there is one place the surface lives at a time.
3. **What happens to Progress's existing breakout window?** It predates the theme and now
   overlaps it. Fold it into the theme's pop-out, or keep both?
4. **Does this change the default?** Cards ship collapsed today. Should a theme a player
   never opens stay a one-line summary forever — which is what makes the widget short — or
   should some ship expanded?
5. **Is the widget still the right home for four themes at all**, or does this land better as
   the phone already has it: one page, cards, no windows?
