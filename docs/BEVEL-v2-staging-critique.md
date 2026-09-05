# EQBuddy v2 — Bevel staging UX critique

**Status:** Helm signed 11:55 AM CT Sep 4. Staging only. Not a hold. Not needs-david.
**Author:** Bevel (Grok).
**Not implement. Not a `FABLE.md` stub. #208 untouched.**

This is the signed product destination for EQBuddy v2's Windows UX. It judges whether a
player can still do the job that made them open the app. It does not commission work.
Fable plans when asked; Claude does not execute from this file.

`docs/Themes.md` stays the v1 fold recipe. Its unfinished **Live Meters** and **Alerts**
themes finish here as **Live** + **Settings → Alerts** + **HUD chips** — not as two more
windows launched from a widget cog.

---

## 0. Stance — small live HUD + one Windows shell

The interaction is fixed. Everything else is furniture.

> **Glance → expand live detail → full app for analysis.**

| Surface | Job | What earns it |
|---|---|---|
| **HUD** (always-on-top, small) | A deadline with an action, plus three numbers you can read without leaving the pull | Mez/charm, spawn-due, Watch fire, buff-expiring. Name · DPS · XP%/hr (or HPS). |
| **One Windows app shell** | Research, compare, configure, review — anything that needs a list, a map, a dump, or a decision | Home / Live / Progress / Gear / Quests / World / Settings |
| **Search** | Find a retained primary feature or a named thing without walking the IA | Global affordance. **Not** a permanent eighth tab. |
| **Optional mobile second screen** | Anything worth looking away from the game for | Map/camps/routes, tracked quests, gear/item lookup, live glance |

v1 treated the **widget as the app** and **Options as the window launcher**. That is the
debt. Cards stacked until every request had a home and no player could find the job. The
cog became the index. mjtrainor said it in #233 and it was already true before he said it:
a long-term user experiences only the disruption, and the release notes named the
destination without the origin.

v2 does not restyle that. It replaces the product shape.

- The HUD is not a miniaturised widget. It does not grow cards.
- The shell is the Alt+Tab target. It is a normal Windows app. Focus is honest.
- Search is how you skip the nav, not a domain of its own.
- Mobile stays a second screen, not a third product.

Consistency is a constraint. The win is the job.

---

## 1. v1 debt versus the commercial bar

The bar is not "what GINA / EQLTools / a browser parser ships." The bar is what a player
already believes a companion must do: stay out of the fight, answer one question without
archaeology, and never lie about what is empty or where a number came from.

| v1 debt | What it does today | Commercial bar | Why it fails the job |
|---|---|---|---|
| **Widget-as-app** | Overlay *is* the product. Cards stack. Windows launch from cards. | Overlay is glance. The app is analysis. | Mid-pull the player hunts a card. After play they hunt a cog. Same hunt, two costumes. |
| **Options-as-window-launcher** | Cog menu lists surfaces that have not been folded yet (`ROADMAP.md` says so out loud). | Settings are settings. Features live on the nav. | A feature you cannot find without Options is a feature you do not have. #219 is the worked example: he opened Cards & windows looking for Motes and found a deletion. |
| **Combat / Healing / breakouts as separate homes** | Three cards, four floats, one fight. | One Live surface. HUD carries the three numbers. | Seeing 412 instead of 438 does not change the next second. The *binary* "am I attacking / is my pet idle" does — that is a chip, not a meter. |
| **Watch / Buffs as cards** | Deadline config wearing a live card. | Deadline chips on the HUD. Rules in Settings → Alerts. | Overlay test: is there something the player must do, and a moment by which they must do it? The card fails it. The chip passes it. |
| **Research lists on overlay chips** | Spawn lists, watch lists, camp research competing with mez. | Chips = deadlines only. Lists live in World / Live / Search. | Overlay must be small enough to ignore. A list is a room you open. |
| **Motes as own-card gravity** | Folded into Progress; restore pressure (`#250`) treats it as a live instrument that lost its glance. | Wealth / Progress, with HUD carrying a rate only if it is a deadline. | Own-card restore is for a live instrument you watch *while playing*. A mote rate you review after the pull is Progress. Do not drag `#250` into Phase 2. |
| **Faction as restore-a-card** | Folded under Progress. `#251` already locked: no Faction card restore. | Advanced under Progress. | A room you open on purpose stays a room. |
| **Kills & Drops as one theme answering two jobs** | Creature research on the widget *and* "is this camp worth it?" | Split by job: session kills → Live; camp worth-it → World; lookup → Search; what dropped for *you* → Gear. | One question, four honest homes, beats one card that is none of them well. |
| **Implementation vocabulary on screen** | Card keys, breakouts, theme body, launcher, cog, overlay section. | Player words. | A player who has to learn our architecture to use the product is paying a trust tax we invented. |
| **Unexplained empties** | `0`, silence, or a task with no route. | Inventory-dump voice: what is missing, the command, what happens next. | Silent no-ops are broken. An empty that does not name the next action is a no-op with a blank stare. |
| **Loot modals** | Ordinary loot interrupts. | Toast. Modal only if the player must decide *now*. | A modal during a pull is the overlay capturing the game. That is the opposite of the HUD job. |
| **Alt+Tab / focus dishonest** | Transparent always-on-top *is* the app; the real windows hide behind it. | HUD stays overlay. Shell is a normal window. | `#271` already started telling the truth. Phase 2 finishes it: the shell is what Windows thinks the app is. |
| **Provenance as decoration** | Badges that do not change a decision. | One sentence where trust would change what you do. | Six badges everywhere is the same as none. Honesty is a scarce resource. |

v1's themes pass was the right *organizing* move after a fast build-out. It is not the v2
product. Quests, Gear, World, and Progress earned keep-and-unify because the *job* survived
the fold. Combat, Healing, Watch, Buffs, Kills & Drops, Options-as-launcher, and the
widget-as-app did not.

---

## 2. Information architecture — Keep / Merge / Replace

One table. Old name on the left so a player (and a release note) can find the origin.

| Current surface | Verdict | Destination | Why |
|---|---|---|---|
| Combat card + Damage breakout | **Merge** | **Live** (session meters) + HUD DPS | One fight. The number on the HUD; the board in the shell. |
| Healing card + Healing breakout | **Merge** | **Live** + HUD HPS when healing dominates ~30s | Same rule, other role. HUD swaps the third number; it does not grow a second meter. |
| Pet breakout | **Merge** | **Live** | Pet idle is a chip if it is a deadline. Pet DPS is a Live board. |
| Fight timeline | **Merge** | **Live** | Analysis. Not an overlay. |
| Watch card | **Replace** | HUD chips (fire) + **Settings → Alerts** (rules) | Config is not a live card. |
| Buffs card | **Replace** | HUD chips (expiring) + **Settings → Alerts** | Same. Expiring is the deadline; the list is settings. |
| Spawn chips / mez chips | **Keep (host)** | **HUD Edit mode** | They already earn the overlay. Edit them on the HUD. No research list on a chip. |
| Kills & Drops | **Replace (split by job)** | **Live** (session kills) · **World** (camp worth it) · **Search** (lookup) · **Gear** (what dropped for you) | Creature research is not a meter and not a camp map. Stop making it a third thing. |
| Quests (General / Epic / Sky) | **Keep → unify** | **Quests** tab | Already the template. One definition, one shell room. |
| Gear & Loot | **Keep → unify** | **Gear** tab | Bags, wishlist, item lookup, what you picked up. |
| World (Map / Camps / Path / Travels) | **Keep → unify** | **World** tab | The chain's place and route. |
| Progress (Experience / Wealth / Faction / Raids) | **Reshape** | Experience · Wealth (Motes lives here) · **Faction = Advanced**. **Raids leave.** | Personal progression only. See door 3. |
| Raids card / Progress → Raids | **Move** | **Live** (session / report) | A raid clear is a session fact, not a career room. Import report stays with the session that produced it. |
| Faction | **Merge** | **Advanced under Progress** | Room you open on purpose. No card restore (`#251` stands). |
| Motes card | **Merge** | **Progress** (Wealth) | Already folded. Phase 2 does not reopen `#250`. |
| Options / cog launcher | **Replace** | **Settings** (settings only) | The ⚙ is Options. It is not a window index. |
| History window | **Merge** | **Progress** (career) + **Live** (this session) | History studio depth stays desktop-only. |
| Session picker | **Merge** | **Home** (recent session) / **Live** | Identity and "what did I just do," not a hidden window. |
| Alerts theme (planned, `Themes.md`) | **Finish as** | **Settings → Alerts** + HUD chips | Configuration consolidates. Chips stay overlay. No new Alerts *window* as a theme launcher. |
| Live Meters theme (planned, `Themes.md`) | **Finish as** | **Live** tab + HUD metrics | Damage / Healing / Pet / Encounters live in the shell. The HUD carries the glance. |

**Search** is how you jump any row of that table without walking it. It is not a domain.

**Home** is a door, not a dashboard-customization surface. Phase 2 contents are locked
below. Recommendations wait.

---

## 3. HUD — collapsed, expanded, Edit, chips, toasts

The HUD is the overlay. It has two sizes and one edit mode. It does not have cards.

### Collapsed

Always visible, always ignorable.

- Character **name**
- **DPS**
- **XP% / hr**

When healing dominates for ~30 seconds, the third number is **HPS** instead of XP%/hr.
One swap, not a second meter. Collapse again the moment combat-as-damage returns.

No lists. No research. No "open this card." Width is reserved; a timer must not change
measured size (trap 12 still binds).

### Expanded

Same HUD, more of the live moment — still not the app.

- **Class trio** (inferred classes in play)
- **Metrics** (the glance plus the one or two numbers that explain it)
- **Deadline chips only** — mez/charm, spawn-due, Watch fire, buff-expiring
- **Open EQBuddy** — the one control that leaves the overlay for the shell

If it is not a deadline and not one of those metrics, it does not earn expanded HUD.

### Edit HUD — on the HUD

You edit the HUD on the HUD. You do not open Settings to decide which chip is showing
during a pull, and you do not open a breakout to hide a chip. Edit mode is how mez /
spawn / Watch chips are placed, muted, and dismissed.

Settings → Alerts owns *rules*: volume, sound, what fires. The HUD owns *what is on
screen right now*.

### Chip earn rule

A chip earns the HUD if and only if:

1. There is something the player must do, and
2. There is a moment by which they must do it.

| Earns a chip | Does not earn a chip |
|---|---|
| Mez / charm breaking | A watch *list* |
| Spawn due now | A camp research table |
| Watch rule fired | Session DPS board |
| Buff expiring | Quest catalog, loot history, faction |

No research lists on chips. A chip that opens a list has become a window.

### Toasts, not modals

Ordinary loot is a **toast**. It does not take focus. It does not stop a pull.

A modal is for a decision the player must make *now* that the HUD cannot carry (consent,
destructive confirm). Loot is neither. A loot modal during combat is the overlay
capturing the game — the defect `ALERT-006` already named, arriving as a dialog.

---

## 4. Empty states, terminology, provenance

### Empty voice — promote the inventory-dump pattern everywhere

The Inventory / Gear empty is the voice. Use it as the house style, not as a special case:

> No inventory dump found yet — in game, type `/outputfile inventory` and this fills in
> on its own.

That sentence does four jobs: names what is missing, names the action, names where
(in game), names what happens next (EQBuddy picks it up). Surfaces that already do this
well (Gear, Quests scan-bags) are the precedent. Surfaces that show `0`, swallow the
command, or name a task with no route are the debt.

Every empty in the shell and on the HUD must:

- Say **what is missing** (dump, log, map folder, no inferred class, nothing due).
- Offer the **next action** the device can actually keep (⧉ on desktop; selectable
  command + "on your PC" on the phone — trap 35).
- Never present a control that opens nothing (no chevron on "Nothing new at N").

An unexplained empty is a silent no-op. Phase 2 does not ship one.

### Terminology ban — no implementation vocabulary on screen

These words are ours. They do not appear in the HUD, the shell, Settings copy, empties,
toasts, or What's-new player text:

| Ban | Say instead |
|---|---|
| card / card key / launcher card | the thing the player is looking at, by its job name |
| breakout | window, or nothing — if it still exists it is a Live panel or a HUD chip |
| theme / theme body | the room: Live, Progress, Gear, Quests, World |
| overlay section / mini-stat | HUD / the number |
| mini pill | the HUD, or the chip by its job — the DPS chip, the mez chip |
| cog menu / Cards & windows (as a *finder*) | Settings, or the nav item |
| widget (as the name of the product) | EQBuddy, or the HUD |
| IWidgetCard, AbsorbedTitles, SectionScroll, dump of internals | never |

*The **mini pill** row was added 2026-09-05 by Helm's vocabulary ruling **(b)** on PR #323:
"mini pill" is ours, **"chip" is not** — "HUD chip" stays the replacement noun the
breakout row points at, and neither "chip" nor that column is re-worded. The row is
pinned by `ShellTerminologyTests.Ban`, which fails in both directions if the two drift.*

What's-new still names the **old place and the new place** when a surface moves
("X is now Y"). That is a player sentence, not an implementation sentence.

### Provenance — where trust changes a decision

Honesty is scarce. Put it where a player would do something different if the number
were a guess.

**Show provenance when:**

- A have-count might be dump, log, or both (quest Turn-ins: `from your inventory dump,
  {age}` / `· plus loot since` / `from your log — hand-ins aren't in the log`).
- Sky leftover / ownership would change whether they vendor or keep.
- A wiki-vs-other source would change whether they trust a drop rate.
- A sample is too thin to label (`SuggestRarity` already refuses under ten kills).

**Do not** mint six badges on every row. A badge that does not change a decision is
decoration wearing honesty's clothes, and it trains the player to ignore the ones that
matter.

Phone: numbers first; provenance on desktop where the sentence fits. Do not port a
badge strip the device cannot honour.

---

## 5. Mobile priority and desktop-only

Mobile is the second screen. It is not a shrunk shell and it is not Linux-on-a-phone.

### Priority (build and keep in this order)

1. **World — map, camps, routes.** The thing worth looking away for. Uncontested ground.
2. **Tracked quests.** What am I working on; what can I turn in.
3. **Gear / item lookup.** What is this; do I have it; where does it go.
4. **Live glance.** The same three numbers as the HUD, not a Live *studio*.

Parity is by shared module, not by feature list. If a job exists on two surfaces, the
decision lives in Core / UI.Shared and both call it.

### Desktop-only (do not port; do not apologise for)

| Stays on Windows | Why the phone cannot keep the promise |
|---|---|
| **Edit HUD** | The HUD is on the game monitor. Editing it from the phone is a lie about where the chips are. |
| **Settings depth** | Volume, paths, janitor, theme chrome, alert rules. A second-screen glance is not a control panel. |
| **History studio** | Compare, filter, career charts. That is after-play on a desk. |
| **ZoneShare apply** | Applies files on the PC that owns the maps. Phone can *see* a zone; it cannot be the apply surface. |
| **Full exaltation lab** | Socketed compare, shopping-list HTML, locker depth. Lookup yes; lab no. |

A phone affordance the device cannot honour is not parity (trap 35). Selectable
`/outputfile inventory` plus "on your PC" is the shape. A ⧉ that copies to the phone
clipboard is a no-op.

---

## 6. Three doors — Helm locked (not needs-david)

These are Bevel assumptions Helm signed. They are not consequence-list doors. Do not
page David. Do not write `needs-david:` on them. Do not reopen them in Phase 2 scope
fights.

### 1. Home recommendations wait Phase 5

Phase 2 Home is **not** a coach and **not** a recommendation engine.

Phase 2 Home carries:

- **Identity** — who is playing (name, server, inferred classes)
- **Readiness** — dump age / log health / "scan bags" if the shell needs a dump
- **Recent session** — what you just did, one screen
- **Deep links** — into Live / Progress / Gear / Quests / World

"What should I work on next" is Phase 5. Building it in Phase 2 would make Home a
dashboard-customization surface, which is on the refuse list.

### 2. LEGACY one-time notice — Bevel voice-pass once

One notice. Bevel writes the voice **once**. Scribe / Helm ship that copy with
**LEGACY-002**. No second voice pass, no per-surface "legacy" chrome, no permanent
banner. The notice exists so a v1 player can find the new home. It is not a theme.

### 3. Raids host = Live; Progress = personal progression only

**Raids** (session clears, the import report, what happened this sitting) live on
**Live**.

**Progress** is personal progression: experience, wealth (including motes), faction
as Advanced. It is not a raid desk.

A raid clear is a session fact. Putting it under Progress taught the IA that
"retrospective" means "everything that is not a card." That is how Faction and Raids
ended up as siblings. They are not. Faction is a career number you open on purpose.
Raids is a report from the sitting you are in — or just left.

---

## 7. Non-goals — Bevel will refuse

These are not "later." They are not Phase 2. Filing them as v2 shell work will be
sent back.

| Refuse | Why |
|---|---|
| **Linux / macOS parity as a Phase 2 gate** | Avalonia tracks a few releases behind on purpose. The v2 destination is a Windows HUD + Windows shell. Do not hold the shell on toolkit twins. Shared decisions still go through Core / UI.Shared so a later port does not invent a fourth copy. |
| **Party rankings / measuring other players** | Values line. Not a feature request. Decline for v2. (Community forks of final 1.x/legacy remain a LEGACY matter only — v2/Evolved is proprietary / permission-required, not MIT.) |
| **Automation / cloud accounts** | Log-only, local, no phone-home. |
| **Standalone Tradeskills or Factions domains** | Faction is Advanced under Progress. Tradeskills is not a seventh primary. Do not mint rooms to match a competitor's sidebar. |
| **Competitor feature parity** | Overlay + DPS is table stakes and we already have them. The uncontested ground is the chain and the second screen. Copying a raid meter is how we lose the values line. |
| **Floating-widget proliferation** | Breakouts were the v1 answer to "the widget is too small." The v2 answer is HUD + shell. Do not grow a new float for a job the shell already owns. |
| **Dashboard-customization-as-goal** | Stars, card order, and per-card restore taught players to assemble a product we should have designed. Home is identity + readiness, not a widget kit. |
| **UI framework rewrite** | WPF is the Windows shell. Avalonia is the twin. Neither is the work. |
| **#208 as a v2 blocker** | Mobile sounds stay held. Talking to the reporter is not the hold; starting the work is. v2 does not open it and does not wait on it. |
| **Dragging `#250` / 320-cap / `#208` into Phase 2 shell scope** | Two tracks stay two tracks. Theme-body 320 and Motes/SectionScroll are signed on their own locks. The shell does not pick them up to "get them out of the way." |

---

## 8. Phase 2 product gate

Phase 2 is done when a player can do the jobs below **without being told how we used
to name the rooms.** This is a product gate, not a ratchet.

1. **Find every retained primary feature without cog / Options archaeology.** Home,
   Live, Progress, Gear, Quests, World, Settings — plus Search. If the only door is
   Settings, the feature is not retained, it is buried.
2. **HUD usable in combat.** Collapsed three-number glance. Expanded class trio +
   deadline chips + Open EQBuddy. No layout jump on a timer. No focus steal. Edit HUD
   on the HUD.
3. **Shell nav complete.** Seven rooms, one Windows app, honest Alt+Tab. Search is
   the skip, not a missing eighth tab.
4. **Settings ≠ launcher.** ⚙ opens settings. It does not list windows that have a
   nav item.
5. **No unexplained empties.** Inventory-dump voice (or the same shape) on every
   first-run and no-data surface. The next action is on screen.
6. **No loot modals.** Ordinary loot toasts. Focus stays with the game.
7. **Windows Alt+Tab / focus honest.** The HUD is overlay. The shell is the app
   Windows tabs to. A click that looks like it should focus the shell, does.

`Themes.md` Live Meters / Alerts, unfinished as theme windows, are accepted as
**finished** when Live exists, Settings → Alerts exists, and the HUD carries the
chips those themes would have configured. Do not build the theme windows first and
fold them later — that is how v1 paid #219 and #233.

---

## What this file is not

- **Not a hold.** Nothing here restrains a v1 fix, a signed Bevel lock already on
  `BEVEL.md`, or a Fable plan that does not claim this destination.
- **Not needs-david.** The three doors above are locked assumptions. The values line,
  the release go, and privacy stay David's; this critique does not touch them.
- **Not implement.** No `src/` from this document.
- **Not a `FABLE.md` item.** Do not stub Phase 2 from this landing. When Fable is
  asked for a v2 plan, this file is input.
- **#208 untouched.** Do not open mobile sounds. Do not describe this critique as
  lifting or waiting on #208.

Standing lock: `BEVEL.md` → *EQBuddy v2 UX destination*. Signed note:
`BEVEL-FEEDBACK.md` 2026-09-04.

— Bevel (Grok), Helm-signed 2026-09-04 11:55 AM CT

---

## Addendum — 2026-09-04 PM (staging pass #2, post-`v1.99.18`)

The body above stands unamended. One door has been overtaken by events and is retired here
rather than left describing a state that has stopped being true.

**§6 door 2 — LEGACY one-time notice: CLOSED, not waiting.** LEGACY-002 shipped in `v1.99.18`
(PR #282) and the reserved Bevel voice pass did not happen. The shipped copy in
`UI.Shared/LegacyPlatformUpdatePolicy` is **kept exactly as written** — it names the platform,
the reassurance and the destination in one line, it survives the 320 px `SizeToContent`
constraint, and it already refuses to point at `releases/latest` for the right reason. Rewriting
shipped player-facing text for no player benefit is the `#228` class. **There is no outstanding
voice pass on this notice and none should be scheduled.** The process lesson — Bevel pre-design
must be a line in the PR rather than a memory — is carried in the `BEVEL.md` entry below, §6.

Doors 1 (Home recommendations wait Phase 5) and 3 (Raids = Live; Progress = personal
progression) are **unchanged and still locked**.

Full pass, with the evidence: `BEVEL.md` → *Evolved staging IA pass #2 — against post-`v1.99.18`
main*. Its §1 (`must-fix`: the shipped first-run tour and its assets describe the pre-fold
product) and §2 (README sells deleted windows and a restore that does not exist) are findings
about what Evolved **inherits**; neither reopens the final v1 bag.

— Bevel (Grok), 2026-09-04 PM. Not a hold. Not needs-david.
