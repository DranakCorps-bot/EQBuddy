# SIGNED — Helm 2026-09-08 ~2:13 PM CT
# Owner amendment 2026-09-08 ~2:15 PM CT (on top of the SIGN)
Amendment: progression surface = **Guide** (interactive guide). Quests rolls into Guide — do **not** keep both Quests… and Guide… doors. Minimized menu = **Options… · World… · Mobile… · Guide…** only (Guide replaces Quests). **Cut** separate shell recovery labels **EQBuddy window…** / Open EQBuddy… — **Guide…** opens the Evolved shell to the Guide room and recovers OE-2 if the shell was closed. World stays a breakout (not folded into Guide). Soft land docs; Soft Opus implement after docs under Soft ≤3 (behind #442→#444→Desktop and early buff-alert fix). Play Console OFF. Not needs-david.

# Bevel faces — Evolved cog / Options IA rethink
**Owner lock 2026-09-08 ~2:09 PM CT.** Soft docs/channel first. No implement until Helm SIGNs. Soft ≤3. Play Console OFF. Not needs-david unless a real door.
**Owner amendment 2026-09-08 ~2:15 PM CT** (on top of Helm SIGN ~2:13 PM CT): Guide door, as above.

**Stance vs lock-6 (2026-09-07):** lock-6 was a *staleness* audit (tooltips/labels current). This pass is a *click-path / density* rethink. Lock-6 does not block these faces.

**Sources read:** `MainWindow.xaml` ContextMenu + MiniRoot; `OnEditHud` → `EnsureHudChips().ToggleEdit()`; `HudEditChip` Done copy; `OptionsWindow` tabs; `SettingsRoom` / `SettingsSurface` (Look · Alerts · HUD · Behavior); `SettingsAlertsView` / `SettingsHudView` / `SettingsBehaviorView` / `SettingsLookView`; signed I-11 / OE-2 / #208 Mobile sounds; v2 critique Edit HUD + Open EQBuddy.

---

## A. Current tree inventory

### A1. Widget chrome (shared Border — expanded *and* minimized)

**Right-click ContextMenu today (busy):**
1. Options…
2. Open EQBuddy… *(shell recovery — OE-2)*
3. EQBuddy Mobile (Beta)…
4. World…
5. Quests…
6. Session history…
7. Click-through (checkable)
8. Edit HUD… *(toggle; exit = choose again)*
9. Data & imports → Wiki pack / Import achievements / Copy /outputfile / Review archived log / Choose log folder / Auto-detect
10. Help → Website / Tutorial / Check updates / Send feedback

**Minimized bar (MiniRoot) chrome:** status dot · mini chips · Expand · Close. **No cog on mini.** Cog/gear + Mobile + Feedback live on **expanded** title bar only.

**Pain path (matches code):** minimized → Expand → right-click (or hunt) → Edit HUD… → same again to exit. Owner’s “wheel” ≈ gear/Settings + context density after expand.

### A2. OptionsWindow (gear / Options… — five tabs)

| Tab | Role |
|-----|------|
| Look | Theme, size, opacity, grid, cursor ring, … |
| Alerts & chips | AlertSurface families + sounds/voices; includes **Buffs** |
| Watch rules | Rule editor (also reachable under Alerts family) |
| Cards & windows | Same block as Settings **HUD** (`SettingsHudView`) — panels / mini dashboard / breakouts |
| Behavior | Tutorial, perf, Mobile (+ **Mobile sounds** #208), hide policies, hotkeys, regen, log truncate/archive, Setup |

### A3. Evolved Settings room (shell — four tabs, I-11)

Look · **Alerts** · **HUD** · Behavior.  
`cards` key → HUD. Watch rules fold under Alerts. OptionsWindow **not** retired (dual host).

### A4. Alert / buff placement (parallel note)

Buff timer **config** lives under **Options → Alerts & chips → Buffs** (and Settings → Alerts → Buffs). Desktop alert sound/voice on that Alerts surface; **Mobile sounds** under Behavior (signed #208).  
Owner: buff timer *alerts still incorrect* — accuracy is Soft; **findability:** keep Buffs under Alerts (not Behavior, not Edit HUD). Edit HUD mutes chip *families*; it must not own sound/when-to-fire.

---

## B. Proposed minimized context menu (owner working lock — adopt; ~2:15 PM CT amendment)

**Right-click minimized bar → only:**

1. **Options…**
2. **World…**
3. **Mobile…** (drop “(Beta)” / shorten “EQBuddy Mobile…” when Soft touches copy; optional)
4. **Guide…** *(replaces Quests…; interactive guide — opens the Evolved shell to the Guide room and recovers OE-2 if the shell was closed)*

**Nothing else on the minimized menu.** Do **not** keep both Quests… and Guide…. **Cut** Open EQBuddy… and **EQBuddy window…** as separate recovery labels. Strong challenge declined for extras: Click-through / Edit HUD / Session history / Data / Help all fail the ≤4-item test on the mini surface.

**≤3 clicks:** each of the four = 1 click from mini. Nested Options destinations ≤2 more inside tabs (still ≤3 total from mini for common settings). **Guide…** is 1 click to the shell (Guide room).

---

## C. Edit HUD enter / exit (direct face)

**Enter (pick one primary; Soft implements one):**
- **Primary:** on **expanded** chip row (or expanded title bar): persistent **Edit** control (pencil / “Edit HUD”). Visible without opening Options or the fat menu.
- **Secondary (ok):** right-click **expanded** bar → Edit HUD… (not on minimized menu).

**Exit:**
- **Done** control on the edit strip (replace “choose Edit HUD… again”).
- **Esc** also exits.
- Re-clicking Edit toggles off (keep as shortcut).

**Out:** maximize → cog → Edit HUD as the only path. Edit HUD stays out of Options tabs (placement mode ≠ settings).

---

## D. Rename map

| Today | Proposed | Why |
|-------|----------|-----|
| Quests… | **Guide…** | Progression surface is the interactive **Guide**. Quests rolls into Guide — one door, not two. |
| Open EQBuddy… | **Cut** (do not rename to EQBuddy window… / Open rooms…) | **Guide…** opens the Evolved shell to the Guide room and is the OE-2 recovery if the shell was closed. A second “open the app” label on a bar that already is EQBuddy is the thing the ~2:15 amendment cuts. |
| Options… (menu) | **Options…** keep | Matches OptionsWindow title; shell room stays “Settings”. Dual name is honest for dual host — do not rename OptionsWindow this cut. |
| Cards & windows (Options tab) | Align copy toward **HUD** when Soft next touches OptionsWindow chrome (I-11 already did in shell) | Finder tab is HUD panels, not a window launcher. |
| Edit HUD… | **Edit HUD** (ellipsis optional) | Mode, not a dialog. |
| EQBuddy Mobile (Beta)… | **Mobile…** | Soft copy polish; mini-menu label is Mobile…. |

**Shell recovery (required — OE-2), ~2:15 amendment:**
- **Guide…** on the **minimized** menu (and the expanded menu) opens the Evolved shell to the **Guide** room. If the shell was closed, that same click recovers it. No second label.
- **Cut** **EQBuddy window…** / Open EQBuddy… / Open rooms… from mini, expanded menu, and Options Behavior/header. Do not add a parallel recovery control.

No hotkey-only recovery (trap 59 / nothing bound by default). Guide… is a menu door, not a hotkey.

---

## E. Keep / cut / move — context menu & chrome

| Item | Verdict | Justification |
|------|---------|---------------|
| Options… | **Keep** on mini + expanded | Settings door; owner list. |
| World… | **Keep** on mini + expanded | Breakout; owner list. **Not** folded into Guide. |
| Guide… | **Keep / add** on mini + expanded | Replaces Quests…; interactive guide; opens shell to Guide room; OE-2 recovery. |
| Quests… | **Cut** as its own door | Rolls into Guide. Do not keep both Quests… and Guide…. |
| Mobile… | **Keep** on mini + expanded; title-bar Mobile btn **Keep** on expanded | Second-screen; owner list. |
| Open EQBuddy… / EQBuddy window… / Open rooms… | **Cut** everywhere | Guide… is the shell door. Separate recovery labels are cut. |
| Edit HUD… | **Cut** from mini; **Move** → expanded Edit control + expanded menu | Direct face; not a settings visit. |
| Click-through | **Cut** from mini; **Keep** on expanded menu only | Mid-pull toggle; rare on mini. |
| Session history… | **Cut** from mini; **Move** → Options Behavior or Help-adjacent / shell Live history door | Analysis, not breakout. |
| Data & imports submenu | **Cut** from mini; **Move** → Options Behavior (or Settings Behavior) section **Data** | Chores ≠ live chrome. |
| Help submenu | **Cut** from mini; **Move** → Options footer / Behavior | Discovery via Options ≤2 clicks. |
| Expanded title: Feedback / Mobile / Gear / Reset / Minimize / Close | **Keep** | Daily; Gear = Options. |
| Mini: Expand / Close | **Keep** | Expand is not Guide and is not a shell door. |

---

## F. Options IA after cut (both hosts — Soft docs first)

**Principle (I-11 stands):** Settings/Options configure; they are not a window launcher directory. Shell / breakout doors live on the mini menu: **World… / Mobile… / Guide…**. **Guide…** is the progression surface **and** the OE-2 shell recovery (Guide room). World stays a breakout — not folded into Guide.

| Area | Keep | Cut / avoid | Move / note |
|------|------|-------------|-------------|
| Look | Theme, size, opacity, grid, cursor | — | Unchanged |
| Alerts (+ Watch rules / Buffs / Spawns / Crowd) | Sound, voice, rules, **buff timer settings** | Do not park buff *fire* logic under Behavior | Buff accuracy = Soft bugfix; **placement stays Alerts → Buffs** |
| HUD / Cards & windows | What EQBuddy shows, mini dashboard stars, breakout toggles, retired list | Do not re-add window launchers | Options tab label → HUD when Soft touches chrome |
| Behavior | Mobile + **Mobile sounds**, hide policies, hotkeys, logs, tutorial, Data & imports, Help links | Do not absorb Edit HUD; do **not** add EQBuddy window… / Open EQBuddy… | Receives chrome cut from context menu |

**Watch rules:** stay under Alerts in shell; OptionsWindow may keep tab until Soft unifies chrome — no third home.

---

## G. Explicit non-goals this Soft ≤3

- No Play Console / no implement until SIGN.
- No retiring OptionsWindow this cut (dual host stays).
- No merging Mobile sounds into Alerts ( #208 Behavior lock stands).
- No putting World / Guide inside Options as the primary door.
- No keeping Quests… beside Guide… (Quests rolls in).
- No folding World into Guide (World stays a breakout).
- No reopening I-11 tab count debate (four in shell).
- No separate **EQBuddy window…** / Open rooms… recovery label.

---

## H. Soft acceptance (docs then code)

1. Minimized right-click shows only **Options… / World… / Mobile… / Guide…**. Guide replaces Quests. Do not keep both Quests… and Guide….
2. Edit HUD enter ≤1 click from expanded HUD; exit via Done or Esc (not maximize→menu→again as sole path).
3. “Open EQBuddy…” / **EQBuddy window…** / Open rooms… gone. **Guide…** opens the Evolved shell to the Guide room and recovers OE-2 if the shell was closed.
4. Buff settings still under Alerts → Buffs; channel note that incorrect buff *alerts* are Soft accuracy, not an IA move.
5. BEVEL.md standing note + HELM-FEEDBACK LIVE ASK closed on SIGN (this file carries the ~2:15 Guide amendment).

— Bevel
