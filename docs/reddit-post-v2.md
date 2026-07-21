# Reddit post draft v2 for r/EQLegends

**Suggested title:**

> EQBuddy 1.8 — the free, open-source EQ Legends session tracker grew up: watch rules with alerts, automatic session history, per-creature drop rates, fight-by-fight DPS (still just reading your /log file)

**Body:**

---

A few months back I posted EQBuddy, a little widget my family and I use to track our
EQ Legends sessions. The response was way beyond what I expected — thank you for every
log line you pasted (each one made the parser smarter), and a special shout-out to the
contributor who showed up with a whole Linux/Avalonia port PR. Since then it's been a
steady stream of releases, and 1.8 feels like a good moment to show what it's become.

**Still the same deal at heart:** an always-on-top card that reads your `/log` file
live — no injection, no memory reading, no accounts, no uploads. Kills, combat-aware
DPS, loot, money, XP, skill-ups, faction, deaths, zones, healing. Click any card to
drill in.

**What's new since the first post:**

- **Watch rules + alerts.** Tell it what you care about — a loot name (`mote`), a
  creature (`Ghoul`), a skill, your deaths, or level/AA milestones — and it counts
  matches, shows per-hour rates, pins chips to the mini dashboard, and fires a banner
  and/or sound the moment one lands. Focus-safe: nothing ever steals your game window.
  Pick from seven alert sounds or bring your own .wav/.mp3.
- **Automatic session history.** Every session saves itself to a local SQLite file —
  sessions end on a 60-minute break, a character switch, or exit, and checkpoint every
  5 minutes so a crash loses almost nothing. Browse, search anything (zone, loot,
  creature, your own notes and tags), export JSON, **Ctrl-click two sessions to compare
  rates side-by-side**, or import your old log files to backfill history.
- **Encounters & farming intelligence.** Recent fights with per-fight duration and DPS;
  per-creature farming stats — average fight length, coin, XP, and **your observed drop
  rates** (e.g. `High Quality Cat Pelt ×2 · 18%`). These are *your* numbers from *your*
  kills, not wiki drop tables.
- **Details!-style combat breakdown.** Damage by attack/spell/pet with share bars,
  per-ability DPS (damage ÷ the time each ability was actually in use), crit rates,
  and sortable columns where the bars follow the sort — same treatment for healing,
  and the same rows in every saved session. If you've used Details!, you know.
- **Stance breakdown.** If your class uses stances, damage/time/DPS are split per stance,
  with the current one shown in the Combat summary.
- **Recent-window and active-time rates.** "Last 15m" figures next to session averages,
  plus per-active-hour rates that ignore your AFK time. Camp markers (`Ctrl+Shift+K`)
  to mark when you settled into a spot.
- **Global hotkeys + click-through.** Hide/show, mini mode, and a click-through mode
  where your clicks pass straight through the widget to the game (border turns amber).
  Reorder or hide the overlay cards to taste — hidden cards keep collecting.
- **Quality-of-life under the hood:** MIT licensed, 100+ automated parser/aggregation
  tests running in CI, SHA-256-verified silent updates, and an in-progress
  Linux/Avalonia port maintained by a community contributor.

**Download:** `EQBuddySetup.exe` from
https://github.com/DranakCorps-bot/EQBuddy/releases/latest — no admin needed. Source
included; parser fixes go fastest when you paste the raw log lines.

**Honest caveats, still honest:**

- Windows only for the full experience (the Avalonia/Linux port currently trails the
  Windows app by a few releases).
- SmartScreen will warn on first install — self-signed cert, "More info → Run anyway,"
  or build from source.
- The parser knows every log format our characters have produced into the mid-20s. New
  class, new zone, weird message? Paste the line and it'll get added — that's how half
  of the current patterns got there.
- Invocations aren't tracked yet: the game doesn't appear to log them. If you know a
  chat line that fires when they activate, I'd love to see it.

Screenshots in the post: the expanded breakdown with recent fights and stance DPS, the
watch rules card with drop rates, mini mode catching an alert, and the session history
browser.

Happy hunting!

---

**Screenshot files to attach** (in `docs\screenshots\`):

Hero shots:

| File | Caption suggestion |
|---|---|
| `widget-expanded.png` | Full drill-down: damage per skill with averages, recent fights with per-fight DPS, damage by stance |
| `widget-tracked.png` | Watch rules with per-hour rates + per-creature farming with observed drop % |
| `widget-mini-alert.png` | Mini mode — starred stats, pinned watch chips, and an alert banner firing |
| `history-window.png` | Automatic session history: search, notes, tags, compare, import, export |

Per-section gallery (for an album or comment thread):

| File | Caption suggestion |
|---|---|
| `section-combat.png` | Combat: Details!-style damage breakdown — share bars with total, hits, average, per-ability DPS, and crit % per row — damage taken per mob, recent fights with DPS bars, stance breakdown |
| `section-healing.png` | Healing: HPS, heals cast per spell with share bars, averages, and per-spell HPS; who healed you; regen/hymn tick counts |
| `section-kills-loot.png` | Kills: per-creature counts and kills/hour, farming stats with observed drop % per item |
| `section-loot.png` | Loot: every item with counts, plus items created by merging |
| `section-tracked-money.png` | Tracked watch rules with per-hour rates; Money: corpse coin vs merchant sales, everything sold with prices |
| `section-progress.png` | Progress: XP/hour, AA, time-to-level estimate; faction standings; zones visited |
| `section-options.png` | Options: size/opacity sliders, watch-rule editor (Loot/Kill/SkillUp/Death/Milestone kinds), alert sound picker, overlay card reorder/hide |
